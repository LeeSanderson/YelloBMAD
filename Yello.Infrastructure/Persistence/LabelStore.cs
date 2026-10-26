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
/// <b>Deletion is serialised against application (AC3, AC10).</b> It locks the Label row under
/// <c>UPDLOCK, HOLDLOCK</c>, then probes the Label's applications under the same hints over the
/// <c>(SpaceId, LabelId, TaskId)</c> index, and deletes only when there are none. An application takes the same
/// Label lock first (<see cref="TaskStore"/>), so both writers take Label, then TaskLabel - one order, so neither can
/// hold what the other waits on. <b>Both Label reads take the clustered index (<c>INDEX(1)</c>)</b>, so they lock the
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
        var visible = await dbContext.Labels
            .FromSql($"SELECT * FROM dbo.[Label] WITH (UPDLOCK, HOLDLOCK, INDEX(1)) WHERE Id = {labelId}")
            .AsNoTracking()
            .AnyAsync(cancellationToken)
            .ConfigureAwait(false);

        if (!visible)
        {
            return LabelDeletion.NotFound;
        }

        var applied = await dbContext.Set<TaskLabel>()
            .FromSql($"SELECT * FROM dbo.[TaskLabel] WITH (UPDLOCK, HOLDLOCK) WHERE LabelId = {labelId}")
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
