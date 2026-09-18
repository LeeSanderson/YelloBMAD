using Microsoft.EntityFrameworkCore;
using Yello.Domain.Accounts;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Reads one Account's credential, and rewrites its hash when the work factor has moved. FR-2,
/// NFR-6.
/// </summary>
/// <remarks>
/// <para>
/// <b>No row-level security session context is set here, and that is correct rather than an
/// omission.</b> AD-2's <c>sp_set_session_context 'SpaceId'</c> scopes Space-scoped tables;
/// <c>Account</c> sits at the top of the ER and carries no policy at all - which
/// <c>deferred-work.md</c> records as an open question owned by the AD-24 amendment due before
/// Epic 3, not by this story. Setting a context here would need a Space, and sign-in has none: a
/// Session carries no Space (AC1), and the acting Space arrives per request in the
/// <c>{spaceId}</c> route segment, which story 1.5 builds.
/// </para>
/// <para>
/// <b>The read projects rather than loading the entity.</b> Two reasons, and the second is the one
/// that matters. It keeps the tracked graph out of a request that has nothing to save - and it
/// means the email address never leaves this class, so nothing downstream can put it on a Session,
/// a claim or a route by the ordinary route of it simply being available.
/// </para>
/// <para>
/// <b><see cref="RehashAsync"/> writes one column by id, and does not read the Account first.</b>
/// An <c>ExecuteUpdate</c> rather than a load-modify-save: there is no invariant to re-check - the
/// caller has already verified the password - and a concurrent sign-in writing the same upgraded
/// hash is a harmless last-writer-wins, since either value verifies the same password at the same
/// work factor.
/// </para>
/// </remarks>
/// <param name="dbContext">The unit of work this read and write go through.</param>
internal sealed class AccountCredentialStore(YelloDbContext dbContext) : IAccountCredentialStore
{
    /// <inheritdoc />
    public async Task<AccountCredential?> FindByEmailAddressAsync(
        string normalizedEmailAddress,
        CancellationToken cancellationToken)
    {
        // AsNoTracking: nothing on this path is saved through the change tracker, and a tracked
        // Account would be a second place the address lives for the life of the request.
        return await dbContext.Accounts
            .AsNoTracking()
            .Where(account => account.NormalizedEmailAddress == normalizedEmailAddress)
            .Select(account => new AccountCredential(account.Id, account.PasswordHash))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RehashAsync(
        Guid accountId,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        await dbContext.Accounts
            .Where(account => account.Id == accountId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(account => account.PasswordHash, passwordHash),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
