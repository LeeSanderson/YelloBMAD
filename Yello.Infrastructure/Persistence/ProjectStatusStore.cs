using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Yello.Domain.Statuses;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Writes one Project's Status delta inside the request's Space-scoped unit of work.
/// </summary>
/// <remarks>
/// <para>
/// <b>The Project's Tasks lock, not a lock of its own.</b> It is the lock every placer of the Project's Tasks and the
/// Project's deletion take, so one lock decides which Statuses a Task of the Project may hold, and a removal that must
/// also move Tasks needs no second lock in a fixed order.
/// </para>
/// <para>
/// <b>A plain read before the lock</b>, as the Project's deletion makes one: a Project id that is missing or another
/// Space's is answered before any lock is named, and the read after the lock catches a deletion that committed while
/// this waited.
/// </para>
/// <para>
/// <b>No <c>Where</c> on <c>SpaceId</c></b> (see <see cref="StatusDefinitionReader"/>).
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
internal sealed class ProjectStatusStore(YelloDbContext dbContext) : IProjectStatusStore
{
    private const int DuplicateKeyRowErrorNumber = 2601;

    private const int UniqueConstraintViolationErrorNumber = 2627;

    /// <inheritdoc />
    public async Task<bool> LockAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (!await ProjectExistsAsync(projectId, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await ApplicationLocks.TakeProjectTasksAsync(dbContext, projectId, cancellationToken).ConfigureAwait(false);

        return await ProjectExistsAsync(projectId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> WriteAsync(StatusDefinition? added, IReadOnlyList<StatusDeltaOperation> operations, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operations);

        return ReplaceAndSaveAsync(added, operations, cancellationToken);
    }

    private async Task<bool> ReplaceAndSaveAsync(StatusDefinition? added, IReadOnlyList<StatusDeltaOperation> operations, CancellationToken cancellationToken)
    {
        foreach (var operation in operations)
        {
            await dbContext.Set<StatusDeltaOperation>()
                .Where(existing => existing.ProjectId == operation.ProjectId && existing.StatusId == operation.StatusId && existing.Kind == operation.Kind)
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        await RenameOwnedAsync(operations, cancellationToken).ConfigureAwait(false);

        if (added is not null)
        {
            dbContext.StatusDefinitions.Add(added);
        }

        dbContext.Set<StatusDeltaOperation>().AddRange(operations);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (IsDuplicateKey(exception))
        {
            // Detached so nothing is left pending for the idempotency record's save to flush. The endpoint's refusal
            // rolls back the deletes above.
            if (added is not null)
            {
                dbContext.Entry(added).State = EntityState.Detached;
            }

            foreach (var operation in operations)
            {
                dbContext.Entry(operation).State = EntityState.Detached;
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// Keeps the owned-name index on the names the Project shows (see <see cref="StatusDefinition.Name"/>). A default has
    /// no owner, so its row is never written.
    /// </summary>
    private async Task RenameOwnedAsync(IReadOnlyList<StatusDeltaOperation> operations, CancellationToken cancellationToken)
    {
        foreach (var rename in operations.Where(operation => operation.Kind == StatusDeltaKind.Rename && operation.Name is not null))
        {
            await dbContext.StatusDefinitions
                .Where(status => status.Id == rename.StatusId && status.OwnerProjectId == rename.ProjectId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(status => status.Name, rename.Name!), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private Task<bool> ProjectExistsAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Projects.AsNoTracking().AnyAsync(project => project.Id == projectId, cancellationToken);

    /// <summary>
    /// Matched on the error number, never on the message, which the server localises.
    /// </summary>
    private static bool IsDuplicateKey(DbUpdateException exception) =>
        exception.InnerException is SqlException sql
        && sql.Errors.Cast<SqlError>().Any(error => error.Number is DuplicateKeyRowErrorNumber or UniqueConstraintViolationErrorNumber);
}
