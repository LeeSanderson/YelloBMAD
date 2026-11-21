using Microsoft.AspNetCore.Authentication;
using Yello.Application.Accounts.DeleteAccount;
using Yello.Contracts;
using Yello.Contracts.Accounts;
using Yello.Contracts.Spaces;
using Yello.Host.Sessions;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// <c>DELETE /api/v1/account</c> - the signed-in Account deletes itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>Account settings, one of the two Account-scoped surfaces</b>: Session only (the Token scheme authenticates nothing
/// without <c>{spaceId}</c>), anti-forgery as for every unsafe method, and no <c>Idempotency-Key</c> - a replay after
/// success meets a dead Session, and a replay of a refusal reads the truth again.
/// </para>
/// <para>
/// <b>Neither refusal is an access refusal</b>: the caller acts on itself, so nothing is recorded, as for
/// <c>ownership-held</c>.
/// </para>
/// </remarks>
public static class AccountEndpoints
{
    /// <summary>
    /// Registers <c>DELETE /api/v1/account</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapDeleteAccount(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapDelete(AccountRoutes.Account, DeleteAsync)
            .AccountScoped(AccountScopedSurface.AccountSettings);

    /// <summary>
    /// Runs the deletion and signs the Session out only once the Account is gone.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="request">The Account-scoped context the pipeline established.</param>
    /// <param name="httpContext">The request, whose Session cookie is expired on success.</param>
    /// <param name="logger">Structured output: the Account's id, never its address or name.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>204</c> with the cookie cleared; <c>409 account-owns-spaces</c> naming every Space owned; or <c>409
    /// account-deletion-interrupted</c>.
    /// </returns>
    public static async Task<IResult> DeleteAsync(
        DeleteAccountHandler handler,
        AccountScopedRequest request,
        HttpContext httpContext,
        ILogger<SessionEndpointMarker> logger,
        CancellationToken cancellationToken)
    {
        var context = request.Context;
        var result = await handler.HandleAsync(context, cancellationToken).ConfigureAwait(false);

        switch (result.Outcome)
        {
            case AccountDeletionOutcome.Deleted:
                await httpContext.SignOutAsync(SessionCookiePolicy.SchemeName).ConfigureAwait(false);
                AccountLog.Deleted(logger, context.AccountId);

                return Results.NoContent();

            case AccountDeletionOutcome.OwnsSpaces:
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    type: ProblemTypes.AccountOwnsSpaces,
                    title: "The Account owns Spaces.",
                    extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["spaces"] = result.Owned.Select(space => new SpaceIdentityResponse(space.Id, space.Name)).ToList(),
                    });

            default:
                return Results.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    type: ProblemTypes.AccountDeletionInterrupted,
                    title: "The Account was not deleted.");
        }
    }
}
