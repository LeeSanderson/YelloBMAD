using Microsoft.AspNetCore.Authentication;
using Yello.Application.Accounts.SignOut;
using Yello.Contracts.Accounts;
using Yello.Host.Sessions;

namespace Yello.Host.Endpoints;

/// <summary>
/// <c>DELETE /api/v1/sessions</c> - sign-out. Story 1.7, open question 4; AC7, AC11; FR-2 and Lee's
/// decision (a) of 2026-10-24.
/// </summary>
/// <remarks>
/// A class of its own beside <see cref="SessionEndpoints"/> only to keep that one within S1200's
/// dependency limit; it is the same resource, and logs under the same category.
/// </remarks>
public static class SignOutEndpoints
{
    /// <summary>
    /// Registers <c>DELETE /api/v1/sessions</c> - sign-out.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapSignOut(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapDelete(SessionRoutes.Authenticate, SignOutAsync);

    /// <summary>
    /// Ends the caller's Session - on the server as well as in the browser - and answers <c>204</c>.
    /// </summary>
    /// <param name="handler">The sign-out slice, which ends the Account's Sessions.</param>
    /// <param name="httpContext">The request, whose cookie is expired.</param>
    /// <param name="logger">Structured output. Carries nothing about the Account.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>204</c>, always - with a Session or without one.</returns>
    /// <remarks>
    /// <para>
    /// <b>Ends the Session on the server, not only the browser's copy</b> (story 1.7, AC11; Lee's
    /// decision (a)). The Account's <c>SessionsValidAfter</c> is stamped, so every ticket issued
    /// before now is refused wherever it is presented next - a lifted copy, or another device. Then
    /// the cookie is expired with the attributes it was set with.
    /// </para>
    /// <para>
    /// <b>Idempotent, and that is open question 4's answer.</b> A caller with no Session is
    /// answered <c>204</c> rather than <c>401</c>: the state it asked for is the state it is in.
    /// Anti-forgery still applies; the group attaches it to every unsafe method, and the token is
    /// bound to the identity it was minted for. A caller whose ticket was already ended elsewhere
    /// is therefore unauthenticated by the time anti-forgery runs, and a token minted while it was
    /// signed in is refused with <c>400</c>. The client asks for a fresh token and sends again,
    /// which is answered <c>204</c> and expires the dead cookie.
    /// </para>
    /// </remarks>
    public static Task<IResult> SignOutAsync(
        SignOutHandler handler,
        HttpContext httpContext,
        ILogger<SessionEndpointMarker> logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(httpContext);

        // Split for S4457, as AuthenticateAsync is.
        return HandleSignOutAsync(handler, httpContext, logger, cancellationToken);
    }

    private static async Task<IResult> HandleSignOutAsync(
        SignOutHandler handler,
        HttpContext httpContext,
        ILogger<SessionEndpointMarker> logger,
        CancellationToken cancellationToken)
    {
        if (SessionPrincipal.AccountIdOf(httpContext.User) is { } accountId)
        {
            await handler.HandleAsync(accountId, cancellationToken).ConfigureAwait(false);
        }

        await httpContext.SignOutAsync(SessionCookiePolicy.SchemeName).ConfigureAwait(false);

        SessionLog.SignOutProcessed(logger);

        return Results.NoContent();
    }
}
