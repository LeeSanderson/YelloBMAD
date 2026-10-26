using Microsoft.EntityFrameworkCore;
using Yello.Domain.Boards;
using Yello.Domain.Projects;
using Yello.Domain.Tasks;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Reads one Project's Board inside the request's Space-scoped unit of work. Story 2.2; FR-28, AD-29.
/// </summary>
/// <remarks>
/// <para>
/// <b>No <c>Where</c> on <c>SpaceId</c></b>, for the reason <see cref="StatusDefinitionReader"/> gives.
/// </para>
/// <para>
/// <b>The totals are one grouped <c>COUNT</c></b> over <c>(SpaceId, ProjectId, StatusId)</c>, the leading
/// columns of the Task index - a separate statement from the Task read, so a total never depends on how
/// many Tasks are returned (AD-29).
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
internal sealed class BoardReader(YelloDbContext dbContext) : IBoardReader
{
    /// <inheritdoc />
    public Task<Project?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        dbContext.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(project => project.Id == projectId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> CountTasksByStatusAsync(Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.Tasks
            .Where(task => task.ProjectId == projectId)
            .GroupBy(task => task.StatusId)
            .Select(group => new { StatusId = group.Key, Total = group.Count() })
            .ToDictionaryAsync(row => row.StatusId, row => row.Total, cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TaskItem>> ListTasksAsync(Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.Tasks
            .AsNoTracking()
            .Where(task => task.ProjectId == projectId)
            .OrderBy(task => task.PositionKey)
            .ThenBy(task => task.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LabelIdsOfAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken) =>
        TaskLabelReads.LabelIdsOfAsync(dbContext, taskIds, cancellationToken);
}
