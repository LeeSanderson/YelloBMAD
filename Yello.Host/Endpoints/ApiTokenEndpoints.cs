using System.Text.Json;
using Yello.Application.ApiTokens.IssueApiToken;
using Yello.Application.ApiTokens.ListApiTokens;
using Yello.Application.ApiTokens.RevokeApiToken;
using Yello.Application.Spaces;
using Yello.Contracts;
using Yello.Contracts.ApiTokens;
using Yello.Contracts.Spaces;
using Yello.Domain.ApiTokens;
using Yello.Domain.Memberships;
using Yello.Host.ApiTokens;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// Issue, list and revoke API Tokens. FR-36 / CAP-36; AD-6, AD-24; story 1.8, AC1, AC4, AC7, AC9.
/// </summary>
/// <remarks>
/// <para>
/// <b>Issue is Space-scoped</b> (open question 1): the pipeline verifies the Membership, answers the
/// boundary's one <c>404</c> and records it for a Space the caller is not in, checks
/// <c>IssueApiTokenForOneself</c> against the matrix - every Role holds it, a Viewer included - and
/// opens the unit of work the row is written in. It is <see cref="ApiTokenAuthentication.SessionOnly"/>,
/// so a Token cannot issue a Token.
/// </para>
/// <para>
/// <b>List and revoke are Account settings</b>, AD-24's second Account-scoped surface: Session-only by
/// construction, and identity-only in what they return.
/// </para>
/// </remarks>
public static class ApiTokenEndpoints
{
    /// <summary>
    /// Registers <c>POST /api/v1/spaces/{spaceId}/api-tokens</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapIssueApiToken(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(ApiTokenRoutes.Issue, IssueAsync)
            .RequiresCapability(Capability.IssueApiTokenForOneself)
            .SessionOnly()
            .CarriesSecretInResponse(JsonNamingPolicy.CamelCase.ConvertName(nameof(IssuedApiTokenResponse.Token)));

    /// <summary>
    /// Registers <c>GET /api/v1/account/api-tokens</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapListApiTokens(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(ApiTokenRoutes.List, ListAsync)
            .AccountScoped(AccountScopedSurface.AccountSettings);

    /// <summary>
    /// Registers <c>DELETE /api/v1/account/api-tokens/{tokenId}</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapRevokeApiToken(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapDelete(ApiTokenRoutes.Revoke, RevokeAsync)
            .AccountScoped(AccountScopedSurface.AccountSettings);

    /// <summary>
    /// Issues a Token bound to the active Space.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">
    /// The request, whose services hold the context the pipeline resolved - read at execution, because
    /// minimal APIs bind arguments before the pipeline's filters have resolved anything. The body is
    /// read here too, not bound as a parameter: a body the framework failed to bind would be answered
    /// <c>400</c> or <c>415</c> before the pipeline ran, so a caller with no credential would not get
    /// the <c>401</c>, and a non-member would get neither the boundary <c>404</c> nor its record.
    /// </param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>201</c> with the Token and its plaintext - the only response that ever carries it - or
    /// <c>400 invalid-api-token</c> for a body that is not JSON naming a Token of 1-100 characters.
    /// </returns>
    public static async Task<IResult> IssueAsync(
        IssueApiTokenHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var request = await ReadRequestAsync(httpContext.Request, cancellationToken).ConfigureAwait(false);

        if (!ApiTokenName.TryNormalise(request?.Name, out var name))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: ProblemTypes.InvalidApiToken,
                title: "A Token's name is 1 to 100 characters.");
        }

        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();

        var issued = await handler.HandleAsync(context, name, cancellationToken).ConfigureAwait(false);

        if (issued is null)
        {
            // The Space was deleted after the pipeline resolved it (story 3.3): the boundary's 404, and the unit of work
            // rolls the Token back.
            return BoundaryProblems.NotAvailable();
        }

        // The only copy: nothing between here and the caller may keep it.
        httpContext.Response.Headers.CacheControl = "no-store";

        return Results.Json(
            new IssuedApiTokenResponse(
                issued.Token.Id,
                issued.Token.Name,
                new SpaceIdentityResponse(issued.Space.Id, issued.Space.Name),
                issued.Token.CreatedAt,
                issued.Token.Plaintext),
            statusCode: StatusCodes.Status201Created);
    }

    /// <summary>
    /// Reads the issue body, or <c>null</c> for one that is not JSON, does not parse, or names a charset
    /// the runtime does not know (an <see cref="InvalidOperationException"/>).
    /// </summary>
    private static async Task<IssueApiTokenRequest?> ReadRequestAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            return null;
        }

        try
        {
            return await request.ReadFromJsonAsync<IssueApiTokenRequest>(cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Lists the caller's Tokens.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="request">The Account-scoped context the pipeline established.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>200</c> with the Tokens, oldest first: identity only, no Role.</returns>
    public static async Task<IResult> ListAsync(
        ListApiTokensHandler handler,
        AccountScopedRequest request,
        CancellationToken cancellationToken)
    {
        var tokens = await handler.HandleAsync(request.Context, cancellationToken).ConfigureAwait(false);

        return Results.Ok(tokens
            .Select(token => new ApiTokenResponse(
                token.Id,
                token.Name,
                new SpaceIdentityResponse(token.Space.Id, token.Space.Name),
                token.CreatedAt))
            .ToList());
    }

    /// <summary>
    /// Revokes one of the caller's Tokens.
    /// </summary>
    /// <param name="tokenId">The Token, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="request">The Account-scoped context the pipeline established.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>204</c> whether or not the Token was the caller's.</returns>
    public static async Task<IResult> RevokeAsync(
        Guid tokenId,
        RevokeApiTokenHandler handler,
        AccountScopedRequest request,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(request.Context, tokenId, cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }
}
