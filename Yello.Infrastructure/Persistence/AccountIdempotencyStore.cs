using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Yello.Application.Spaces;
using Yello.Domain;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Reads and writes Account-scoped <c>Idempotency-Key</c> records on the Space creation unit of work. AD-18 / AR-25;
/// story 3.1, open question 2 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="IdempotencyStore"/>'s twin, under the <c>AccountId</c> key.</b> Both isolation layers are narrowed
/// to the request's Account (the table's own policy, and its query filter on <see cref="AccountScope"/>), so a lookup
/// sees this Account's records and no other's. The same rules hold: expiry is read and never written by a timer;
/// expired records are deleted only inside their own Account's unit of work; the lookup is a locking read, so a retry
/// overlapping a first use's commit waits and replays; the record must be the only change its save flushes; and a
/// concurrent first use loses on the unique index.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
/// <param name="identifiers">Produces the record's identity.</param>
/// <param name="clock">Stamps the record and evaluates its expiry.</param>
internal sealed class AccountIdempotencyStore(YelloDbContext dbContext, IIdentifierGenerator identifiers, TimeProvider clock)
    : IAccountIdempotencyStore
{
    private const int UniqueIndexViolationErrorNumber = 2601;

    private const int UniqueConstraintViolationErrorNumber = 2627;

    /// <inheritdoc />
    public async Task<RecordedResponse?> FindAsync(Guid accountId, string key, CancellationToken cancellationToken)
    {
        var oldest = clock.GetUtcNow() - IAccountIdempotencyStore.Retention;

        return await dbContext.Set<AccountIdempotencyRecord>()
            .FromSql($"SELECT * FROM dbo.[AccountIdempotencyRecord] WITH (READCOMMITTEDLOCK)")
            .AsNoTracking()
            .Where(record => record.AccountId == accountId && record.Key == key && record.CreatedAt > oldest)
            .Select(record => new RecordedResponse(record.Fingerprint, record.StatusCode, record.ContentType, record.Location, record.Body))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> TryRecordAsync(Guid accountId, string key, RecordedResponse response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        // Split for S4457.
        return TryRecordCoreAsync(accountId, key, response, cancellationToken);
    }

    private async Task<bool> TryRecordCoreAsync(Guid accountId, string key, RecordedResponse response, CancellationToken cancellationToken)
    {
        EnsureNothingElseIsPending();

        var now = clock.GetUtcNow();
        var oldest = now - IAccountIdempotencyStore.Retention;

        // This Account's expired records only - the policy and the filter both see nothing else.
        await dbContext.Set<AccountIdempotencyRecord>()
            .Where(record => record.CreatedAt <= oldest)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var record = new AccountIdempotencyRecord
        {
            Id = identifiers.Next(),
            AccountId = accountId,
            Key = key,
            Fingerprint = response.Fingerprint,
            StatusCode = response.StatusCode,
            ContentType = response.ContentType,
            Location = response.Location,
            Body = response.Body,
            CreatedAt = now,
        };

        dbContext.Set<AccountIdempotencyRecord>().Add(record);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            dbContext.Entry(record).State = EntityState.Detached;

            return false;
        }

        return true;
    }

    /// <summary>
    /// Fails closed when the unit of work holds any unsaved change, as <see cref="IdempotencyStore"/> does.
    /// </summary>
    private void EnsureNothingElseIsPending()
    {
        var pending = dbContext.ChangeTracker.Entries()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(entry => entry.Metadata.ClrType.Name)
            .ToList();

        if (pending.Count > 0)
        {
            throw new InvalidOperationException(
                $"The idempotency record must be the only change it saves, and {string.Join(", ", pending)} "
                + "is still pending. A store saves its own effect before the record is taken.");
        }
    }

    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException
        && sqlException.Errors.Cast<SqlError>().Any(error =>
            error.Number is UniqueIndexViolationErrorNumber or UniqueConstraintViolationErrorNumber);
}
