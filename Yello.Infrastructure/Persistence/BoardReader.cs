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
/// many Tasks are returned (AD-29). One column's count (story 2.9) is the same predicate for one Status.
/// </para>
/// <para>
/// <b>Each statement reads its own snapshot</b> (story 2.9, AC13, open question 10 (a)): <c>READ_COMMITTED_SNAPSHOT</c>
/// is on, so no read here waits on a writer; a count, a page and its Labels may disagree by the writes that commit
/// between them, a disagreement bounded to concurrent change and corrected by the next read.
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
    public Task<int> CountTasksAsync(Guid projectId, Guid statusId, CancellationToken cancellationToken) =>
        dbContext.Tasks
            .CountAsync(task => task.ProjectId == projectId && task.StatusId == statusId, cancellationToken);

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// <b>A keyset seek, never <c>OFFSET</c></b> (AD-29, story 2.9 AC1): <c>WHERE ProjectId = @p AND StatusId = @s AND
    /// PositionKey &gt; @after ORDER BY PositionKey</c> with <c>TOP</c>, which seeks
    /// <c>IX_Task_SpaceId_ProjectId_StatusId_PositionKey</c> - the policy predicate supplies its leading <c>SpaceId</c>.
    /// <c>string.Compare(…) &gt; 0</c> translates to <c>&gt;</c>, and its parameter takes the column's own mapping,
    /// <c>varchar(128)</c>: an <c>nvarchar</c> parameter would convert the BIN2 column and lose the seek
    /// (<c>BoardPagingTests</c> PG-12 reads the plan). The comparison is ordinal because the column's collation is BIN2.
    /// </para>
    /// <para>
    /// <b>No tiebreaker</b>: the index is unique on <c>(SpaceId, ProjectId, StatusId, PositionKey)</c>, so a key names at
    /// most one Task of a column and the order is total without <c>Id</c>.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<TaskItem>> PageAsync(Guid projectId, Guid statusId, string? after, int take, CancellationToken cancellationToken)
    {
        var column = dbContext.Tasks
            .AsNoTracking()
            .Where(task => task.ProjectId == projectId && task.StatusId == statusId);

        if (after is not null)
        {
            column = column.Where(task => string.Compare(task.PositionKey, after) > 0);
        }

        return await column
            .OrderBy(task => task.PositionKey)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LabelIdsOfAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken) =>
        TaskLabelReads.LabelIdsOfAsync(dbContext, taskIds, cancellationToken);
}
