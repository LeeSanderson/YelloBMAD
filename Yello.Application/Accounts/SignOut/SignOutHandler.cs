using Yello.Domain.Accounts;

namespace Yello.Application.Accounts.SignOut;

/// <summary>
/// Ends an Account's Sessions on the server - the half of sign-out a cookie cannot do by itself.
/// FR-2; story 1.7, open question 4 and Lee's decision (a) of 2026-10-24.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the server has to do anything.</b> Expiring the browser's cookie leaves every copy of the
/// ticket valid until its lifetime runs out, and FR-2 says an invalidated Session grants access to
/// nothing. Stamping the instant here is what every later request is checked against.
/// </para>
/// <para>
/// <b>Every device, not only this one</b> - the stamp is per Account. Recorded as the accepted cost
/// of an invalidation that needs no table of Sessions.
/// </para>
/// </remarks>
/// <param name="store">Where the instant is stored.</param>
/// <param name="clock">The clock, injected so a test controls it.</param>
public sealed class SignOutHandler(IAccountSessionStore store, TimeProvider clock)
{
    /// <summary>
    /// Ends every Session the Account holds now.
    /// </summary>
    /// <param name="accountId">The Account the presented Session names.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>A task that completes once the Sessions are ended.</returns>
    public Task HandleAsync(Guid accountId, CancellationToken cancellationToken) =>
        store.EndSessionsAsync(accountId, clock.GetUtcNow(), cancellationToken);
}
