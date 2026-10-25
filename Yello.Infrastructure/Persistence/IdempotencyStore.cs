using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Yello.Application.Spaces;
using Yello.Domain;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Reads and writes <c>Idempotency-Key</c> records on the request's Space-scoped unit of work. AD-18 /
/// AR-25; story 2.1, Task 7.
/// </summary>
/// <remarks>
/// <para>
/// <b>No <c>Where</c> on <c>SpaceId</c>.</b> Both isolation layers are already narrowed to the request's
/// Space, so a lookup sees this Space's records and no other's.
/// </para>
/// <para>
/// <b>Expiry is read, never written by a timer</b> (AD-10, AD-27), in the lookup's own statement - the
/// shape <see cref="SpacePresenceRegister"/> uses. Expired records are deleted when the same Space next
/// records one, inside that Space's own unit of work: the table is under the Space policy, so a purge
/// across Spaces would need the bypass AD-2 forbids.
/// </para>
/// <para>
/// <b>A concurrent first use loses on the unique index.</b> The second insert of a key waits on the
/// first's key lock; once the first commits it fails, and the caller rolls back its whole unit of work.
/// A duplicate-key error ends only the statement, so the failed entity is detached here.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
/// <param name="identifiers">Produces the record's identity.</param>
/// <param name="clock">Stamps the record and evaluates its expiry.</param>
internal sealed class IdempotencyStore(YelloDbContext dbContext, IIdentifierGenerator identifiers, TimeProvider clock)
    : IIdempotencyStore
{
    /// <summary>
    /// SQL Server's duplicate key in a unique index.
    /// </summary>
    private const int UniqueIndexViolationErrorNumber = 2601;

    /// <summary>
    /// SQL Server's duplicate key in a unique or primary key constraint.
    /// </summary>
    private const int UniqueConstraintViolationErrorNumber = 2627;

    /// <inheritdoc />
    public async Task<RecordedResponse?> FindAsync(Guid accountId, string key, CancellationToken cancellationToken)
    {
        var oldest = clock.GetUtcNow() - IIdempotencyStore.Retention;

        return await dbContext.Set<IdempotencyRecord>()
            .AsNoTracking()
            .Where(record => record.AccountId == accountId && record.Key == key && record.CreatedAt > oldest)
            .Select(record => new RecordedResponse(record.Fingerprint, record.StatusCode, record.ContentType, record.Location, record.Body))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> TryRecordAsync(
        Guid spaceId,
        Guid accountId,
        string key,
        RecordedResponse response,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        // Split for S4457.
        return TryRecordCoreAsync(spaceId, accountId, key, response, cancellationToken);
    }

    private async Task<bool> TryRecordCoreAsync(
        Guid spaceId,
        Guid accountId,
        string key,
        RecordedResponse response,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var oldest = now - IIdempotencyStore.Retention;

        // This Space's expired records only - the policy and the filter both see nothing else.
        await dbContext.Set<IdempotencyRecord>()
            .Where(record => record.CreatedAt <= oldest)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var record = new IdempotencyRecord
        {
            Id = identifiers.Next(),
            SpaceId = spaceId,
            AccountId = accountId,
            Key = key,
            Fingerprint = response.Fingerprint,
            StatusCode = response.StatusCode,
            ContentType = response.ContentType,
            Location = response.Location,
            Body = response.Body,
            CreatedAt = now,
        };

        dbContext.Set<IdempotencyRecord>().Add(record);

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
    /// Matched on the error number, never on the message, which the server localises.
    /// </summary>
    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException
        && sqlException.Errors.Cast<SqlError>().Any(error =>
            error.Number is UniqueIndexViolationErrorNumber or UniqueConstraintViolationErrorNumber);
}
