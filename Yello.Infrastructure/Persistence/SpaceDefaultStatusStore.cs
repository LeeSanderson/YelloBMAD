using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Yello.Domain.Statuses;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Changes the active Space's default Statuses inside the request's Space-scoped unit of work.
/// </summary>
/// <remarks>
/// <para>
/// <b>Writes only the defaults and the <c>Rename</c> operations a rename deletes.</b> A Project's set is derived, so nothing
/// else is written for any Project.
/// </para>
/// <para>
/// <b>A Project's deletion beside a cascading rename</b> may delete the same operation rows. One waits for the other on
/// those rows; neither then takes a lock the other holds, because this touches only <c>StatusDeltaOperation</c> rows and
/// the default's own <c>StatusDefinition</c> row, so the two cannot cycle.
/// </para>
/// <para>
/// <b>No <c>Where</c> on <c>SpaceId</c></b> (see <see cref="StatusDefinitionReader"/>).
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
internal sealed class SpaceDefaultStatusStore(YelloDbContext dbContext) : ISpaceDefaultStatusStore
{
    private const int DuplicateKeyRowErrorNumber = 2601;

    private const int UniqueConstraintViolationErrorNumber = 2627;

    /// <inheritdoc />
    public Task LockAsync(CancellationToken cancellationToken) => ApplicationLocks.TakeStatusDefaultsAsync(dbContext, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, ProjectStatusDelta>> DeltasAsync(CancellationToken cancellationToken)
    {
        var owned = await dbContext.StatusDefinitions
            .AsNoTracking()
            .Where(status => status.OwnerProjectId != null)
            .OrderBy(status => status.Position)
            .ThenBy(status => status.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var operations = await dbContext.Set<StatusDeltaOperation>()
            .AsNoTracking()
            .OrderBy(operation => operation.Sequence)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return owned.Select(status => status.OwnerProjectId!.Value)
            .Concat(operations.Select(operation => operation.ProjectId))
            .Distinct()
            .ToDictionary(
                projectId => projectId,
                projectId => new ProjectStatusDelta(
                    [.. owned.Where(status => status.OwnerProjectId == projectId)],
                    [.. operations.Where(operation => operation.ProjectId == projectId)]));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> CountTasksByProjectAsync(Guid statusId, CancellationToken cancellationToken) =>
        await dbContext.Tasks
            .AsNoTracking()
            .Where(task => task.StatusId == statusId)
            .GroupBy(task => task.ProjectId)
            .Select(group => new { ProjectId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.ProjectId, row => row.Count, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> ProjectNamesAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) =>
        await dbContext.Projects
            .AsNoTracking()
            .Where(project => projectIds.Contains(project.Id))
            .ToDictionaryAsync(project => project.Id, project => project.Name, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<bool> WriteAsync(SpaceDefaultStatusChange change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);

        return WriteChangeAsync(change, cancellationToken);
    }

    private async Task<bool> WriteChangeAsync(SpaceDefaultStatusChange change, CancellationToken cancellationToken)
    {
        try
        {
            if (change is { RenamedStatusId: { } renamed, Name: { } name })
            {
                await RenameAsync(renamed, name, change.RenameOperationsDeletedIn, cancellationToken).ConfigureAwait(false);
            }

            foreach (var (statusId, position) in change.Positions)
            {
                await dbContext.StatusDefinitions
                    .Where(status => status.Id == statusId && status.OwnerProjectId == null)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(status => status.Position, position), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        catch (SqlException exception) when (IsDuplicateKey(exception))
        {
            return false;
        }

        return change.Added is not { } added || await InsertAsync(added, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The operations go first and the name second, in the one transaction: a failure in either leaves both as they were.
    /// </summary>
    private async Task RenameAsync(Guid statusId, string name, IReadOnlyList<Guid> deletedIn, CancellationToken cancellationToken)
    {
        if (deletedIn.Count > 0)
        {
            await dbContext.Set<StatusDeltaOperation>()
                .Where(operation => operation.StatusId == statusId && operation.Kind == StatusDeltaKind.Rename && deletedIn.Contains(operation.ProjectId))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        await dbContext.StatusDefinitions
            .Where(status => status.Id == statusId && status.OwnerProjectId == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(status => status.Name, name), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> InsertAsync(StatusDefinition added, CancellationToken cancellationToken)
    {
        dbContext.StatusDefinitions.Add(added);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && IsDuplicateKey(sql))
        {
            // Detached so nothing is left pending for the idempotency record's save to flush.
            dbContext.Entry(added).State = EntityState.Detached;

            return false;
        }

        return true;
    }

    /// <summary>
    /// Matched on the error number, never on the message, which the server localises.
    /// </summary>
    private static bool IsDuplicateKey(SqlException exception) =>
        exception.Errors.Cast<SqlError>().Any(error => error.Number is DuplicateKeyRowErrorNumber or UniqueConstraintViolationErrorNumber);
}
