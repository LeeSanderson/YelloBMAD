using Microsoft.EntityFrameworkCore;
using Yello.Domain.Statuses;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Takes a Space default out of the active Space and every Project of it, inside the request's Space-scoped unit of work.
/// </summary>
/// <remarks>
/// <para>
/// <b>The statements follow the keys.</b> <c>Task</c> and both of <c>StatusDeltaOperation</c>'s Status keys are
/// <c>NO ACTION</c>, so the Tasks leave the Status first, then the places anchored on it are replaced, then every operation
/// naming it goes, and the definition goes last, when nothing refers to it. All of it is the one transaction the request
/// runs in.
/// </para>
/// <para>
/// <b>No <c>Where</c> on <c>SpaceId</c></b> (see <see cref="StatusDefinitionReader"/>).
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
internal sealed class SpaceDefaultStatusRemovalStore(YelloDbContext dbContext) : ISpaceDefaultStatusRemovalStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> LockProjectsAsync(CancellationToken cancellationToken)
    {
        await ApplicationLocks.TakeProjectsPerSpaceAsync(dbContext, cancellationToken).ConfigureAwait(false);

        return await dbContext.Projects
            .AsNoTracking()
            .ToDictionaryAsync(project => project.Id, project => project.Name, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task LockProjectTasksAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) =>
        ApplicationLocks.TakeProjectTasksAsync(dbContext, projectIds, cancellationToken);

    /// <inheritdoc />
    public Task RemoveAsync(SpaceDefaultStatusRemovalPlan plan, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return RemovePlannedAsync(plan, cancellationToken);
    }

    private async Task RemovePlannedAsync(SpaceDefaultStatusRemovalPlan plan, CancellationToken cancellationToken)
    {
        var statusId = plan.StatusId;

        foreach (var migration in plan.Migrations)
        {
            await TaskStatusMigration.MigrateAsync(dbContext, migration.ProjectId, statusId, migration.ProjectId, migration.DestinationStatusId, cancellationToken).ConfigureAwait(false);
        }

        if (plan.Places.Count > 0)
        {
            var projectIds = plan.Places.Select(reanchoring => reanchoring.ProjectId).ToList();

            await dbContext.Set<StatusDeltaOperation>()
                .Where(operation => operation.Kind == StatusDeltaKind.Place && projectIds.Contains(operation.ProjectId))
                .ExecuteDeleteAsync(cancellationToken)
                .ConfigureAwait(false);

            dbContext.Set<StatusDeltaOperation>().AddRange(plan.Places.SelectMany(reanchoring => reanchoring.Places));
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        // A Project that had removed the Status may still hold a place anchored on it, which the derivation ignores but the
        // definition's key would not.
        await dbContext.Set<StatusDeltaOperation>()
            .Where(operation => operation.StatusId == statusId || operation.AfterStatusId == statusId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        await dbContext.StatusDefinitions
            .Where(status => status.Id == statusId && status.OwnerProjectId == null)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
