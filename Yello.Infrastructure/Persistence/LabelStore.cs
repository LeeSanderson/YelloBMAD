using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Yello.Domain;
using Yello.Domain.Labels;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Lists, defines and deletes Labels inside the request's Space-scoped unit of work. Story 2.4; FR-22.
/// </summary>
/// <remarks>
/// <para>
/// <b>No <c>Where</c> on <c>SpaceId</c>, anywhere.</b> Both isolation layers are already narrowed to the request's
/// Space (see <see cref="StatusDefinitionReader"/>). The two locking reads below are raw SQL composed onto the
/// <c>Label</c> and <c>TaskLabel</c> sets with <c>FromSql</c>, so EF Core's query filter still wraps them and the
/// security policy still filters them: either layer alone hides another Space's Label.
/// </para>
/// <para>
/// <b>A duplicate name is the unique index's to refuse</b> (open question 2): two definitions of one name racing each
/// other both pass any pre-check, so the insert's 2601/2627 is caught and answered as "taken", never as a
/// <c>500</c>.
/// </para>
/// <para>
/// <b>Deletion is serialised against application (AC3, AC10).</b> It reads the Label with a plain statement first, so
/// an id that is missing or another Space's is not found before any lock is taken (story 3.4, AC10: a locking point read
/// on a missing key locks the next key, which can be another Space's). It then locks the Label row under
/// <c>UPDLOCK</c> (no <c>HOLDLOCK</c>, so a key deleted meanwhile is not range-locked), takes the Label's applications lock (<see cref="ApplicationLocks.LabelApplications"/>, named
/// for the request's Space), probes the Label's applications under <c>READCOMMITTEDLOCK</c> (which waits on an
/// uncommitted change to them but locks no range), and deletes only when there are none. An
/// application takes the same Label lock and then the same applications lock (<see cref="TaskStore"/>), so both writers
/// take Label, then the applications lock - one order, so neither can hold what the other waits on. Until story 3.4 the
/// probe was a serialisable range read over <c>TaskLabel</c> that named no Space, and locked another Space's first key
/// at the edge of its range. <b>Both Label reads take the clustered index (<c>INDEX(1)</c>)</b>, so they lock the
/// same key: left to the optimiser, the application's count is covered by <c>AK_Label_SpaceId_Id</c> and the
/// deletion's read by the clustered key, the two U locks never meet, and the deletion's <c>DELETE</c> then deadlocks
/// with the application's insert - measured by the story's race case, which found it. The <c>NoAction</c> reference is the backstop: if it ever refuses (547), the answer
/// is still "in use", never a <c>500</c>.
/// </para>
/// <para>
/// <b>Saves its own effect</b>, as every store does (<see cref="IdempotencyStore"/>).
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
/// <param name="identifiers">Produces the Label's identity (AR-34).</param>
/// <param name="clock">Stamps the definition instant.</param>
internal sealed class LabelStore(YelloDbContext dbContext, IIdentifierGenerator identifiers, TimeProvider clock)
    : ILabelStore
{
    private const int DuplicateKeyRowErrorNumber = 2601;

    private const int UniqueConstraintViolationErrorNumber = 2627;

    private const int ReferenceConflictErrorNumber = 547;

    /// <inheritdoc />
    public async Task<IReadOnlyList<Label>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Labels
            .AsNoTracking()
            .OrderBy(label => label.Name)
            .ThenBy(label => label.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<Label?> CreateAsync(Guid spaceId, string name, string colour, CancellationToken cancellationToken)
    {
        var label = new Label
        {
            Id = identifiers.Next(),
            SpaceId = spaceId,
            Name = name,
            Colour = colour,
            CreatedAt = clock.GetUtcNow(),
        };

        dbContext.Labels.Add(label);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (HasError(exception, DuplicateKeyRowErrorNumber, UniqueConstraintViolationErrorNumber))
        {
            // Detached so nothing is left pending for the idempotency record's save to flush.
            dbContext.Entry(label).State = EntityState.Detached;

            return null;
        }

        return label;
    }

    /// <inheritdoc />
    public async Task<LabelDeletion> DeleteAsync(Guid labelId, CancellationToken cancellationToken)
    {
        // A plain read under both isolation layers first: an id that is missing or another Space's takes no lock at all.
        var visible = await dbContext.Labels
            .AsNoTracking()
            .AnyAsync(label => label.Id == labelId, cancellationToken)
            .ConfigureAwait(false);

        if (!visible)
        {
            return LabelDeletion.NotFound;
        }

        // The Label's own row, on the key an application locks too. Gone by now is not found. UPDLOCK without HOLDLOCK
        // (story 3.4 review): a found row's U lock is held to commit either way, and a row deleted between the plain read
        // and this one leaves a missing key, which HOLDLOCK would range-lock up to the next key - another Space's.
        var locked = await dbContext.Labels
            .FromSql($"SELECT * FROM dbo.[Label] WITH (UPDLOCK, INDEX(1)) WHERE Id = {labelId}")
            .AsNoTracking()
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!locked)
        {
            return LabelDeletion.NotFound;
        }

        // Every application of this Label takes the same lock, so the read below sees every one committed before it.
        await ApplicationLocks.TakeLabelApplicationsAsync(dbContext, labelId, cancellationToken).ConfigureAwait(false);

        // READCOMMITTEDLOCK, not a snapshot and not HOLDLOCK: it waits on this Space's uncommitted removal of an application
        // (a Task deletion's cascade, story 2.5 RACE-T4) and follows its outcome, but takes no range lock, so it reaches no
        // key past this Space's own (story 3.4, AC10). The query filter and the security policy both narrow it to the Space.
        // A locking read takes a shared lock on every row it reads, so the plan is pinned (story 3.4 review): a seek on
        // (SpaceId, LabelId) over the in-use probe's index reads this Space's rows only. Left to the optimiser, a scan
        // would read - and wait on - another Space's uncommitted application before the filters dropped it.
        var spaceId = dbContext.ScopedSpaceId
            ?? throw new InvalidOperationException("A Label is deleted only inside a Space-scoped unit of work.");

        var applied = await dbContext.Set<TaskLabel>()
            .FromSql($"SELECT * FROM dbo.[TaskLabel] WITH (READCOMMITTEDLOCK, FORCESEEK (IX_TaskLabel_SpaceId_LabelId_TaskId (SpaceId, LabelId))) WHERE SpaceId = {spaceId} AND LabelId = {labelId}")
            .AsNoTracking()
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);

        if (applied)
        {
            return LabelDeletion.InUse;
        }

        try
        {
            var deleted = await dbContext.Labels
                .Where(label => label.Id == labelId)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            return deleted == 0 ? LabelDeletion.NotFound : LabelDeletion.Deleted;
        }
        catch (SqlException exception) when (HasError(exception, ReferenceConflictErrorNumber))
        {
            return LabelDeletion.InUse;
        }
        catch (DbUpdateException exception) when (HasError(exception, ReferenceConflictErrorNumber))
        {
            return LabelDeletion.InUse;
        }
    }

    /// <summary>
    /// Matched on the error number, never on the message, which the server localises.
    /// </summary>
    private static bool HasError(Exception exception, params int[] numbers)
    {
        var sql = exception as SqlException ?? exception.InnerException as SqlException;

        return sql is not null && sql.Errors.Cast<SqlError>().Any(error => numbers.Contains(error.Number));
    }
}
