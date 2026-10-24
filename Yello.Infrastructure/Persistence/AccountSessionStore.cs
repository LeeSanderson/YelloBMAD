using Microsoft.EntityFrameworkCore;
using Yello.Domain.Accounts;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Stores and reads the instant an Account's Sessions were last ended. FR-2; story 1.7, Lee's
/// decision (a) of 2026-10-24.
/// </summary>
/// <remarks>
/// <para>
/// <b>No session context is set here</b>, for the reason <see cref="AccountCredentialStore"/> gives:
/// <c>Account</c> carries no policy, and the read happens before any Space is known - in the
/// authentication step of every request that presents a cookie.
/// </para>
/// <para>
/// <b>One row by primary key, projected to one column.</b> Nothing else about the Account - not the
/// address, not the hash - is read on this per-request path. The write is an <c>ExecuteUpdate</c> by
/// id, like <see cref="AccountCredentialStore.RehashAsync"/>, and it only ever moves the instant
/// forward: two sign-outs racing each other end everything issued before the later of them,
/// whichever commits last, and a clock that has stepped back cannot re-admit a ticket already
/// refused.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
internal sealed class AccountSessionStore(YelloDbContext dbContext) : IAccountSessionStore
{
    /// <inheritdoc />
    public async Task<SessionStanding?> FindAsync(Guid accountId, CancellationToken cancellationToken) =>
        await dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.Id == accountId)
            .Select(account => new SessionStanding(account.SessionsValidAfter))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task EndSessionsAsync(Guid accountId, DateTimeOffset at, CancellationToken cancellationToken) =>
        await dbContext.Accounts
            .Where(account => account.Id == accountId
                && (account.SessionsValidAfter == null || account.SessionsValidAfter < at))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(account => account.SessionsValidAfter, at),
                cancellationToken)
            .ConfigureAwait(false);
}
