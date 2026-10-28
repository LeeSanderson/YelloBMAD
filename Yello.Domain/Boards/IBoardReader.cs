using Yello.Domain.Projects;
using Yello.Domain.Tasks;

namespace Yello.Domain.Boards;

/// <summary>
/// Reads what one Project's Board shows, in the Space the current unit of work is scoped to. FR-28;
/// stories 2.2 and 2.9.
/// </summary>
/// <remarks>
/// <para>
/// <b>The totals are their own read</b> (AD-29, AC5): a count per Status over the indexed predicate, never
/// the number of Tasks another call happened to return. Story 2.9 pages the Tasks; it never changes what a
/// column's total means.
/// </para>
/// <para>
/// <b>A column is read by keyset seek on its position key</b> (AD-29, AR-22; story 2.9, AC1): the Tasks of one
/// (Project, Status) whose key sorts after a cursor, in key order, at most a page - never by <c>OFFSET</c>, so the last
/// page of a column costs what the first does.
/// </para>
/// <para>
/// <b>Never asked about another Space</b>, for the reason <see cref="ITaskStore"/> gives.
/// </para>
/// </remarks>
public interface IBoardReader
{
    /// <summary>
    /// The Project, if it is visible in the resolved Space.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Project, or <c>null</c> when no Project with that id is visible.</returns>
    Task<Project?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// How many Tasks the Project holds in each Status, counted by the database.
    /// </summary>
    /// <param name="projectId">The Project.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The count per Status id; a Status holding none is absent.</returns>
    Task<IReadOnlyDictionary<Guid, int>> CountTasksByStatusAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// How many Tasks the Project holds in one Status, counted by the database over the same predicate a page of that
    /// column seeks (story 2.9, AC4).
    /// </summary>
    /// <param name="projectId">The Project.</param>
    /// <param name="statusId">The column's Status.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The count.</returns>
    Task<int> CountTasksAsync(Guid projectId, Guid statusId, CancellationToken cancellationToken);

    /// <summary>
    /// One page of a column: the Project's Tasks in <paramref name="statusId"/> whose position key sorts after
    /// <paramref name="after"/> (ordinal), by key, at most <paramref name="take"/> of them. Story 2.9, AC1.
    /// </summary>
    /// <param name="projectId">The Project.</param>
    /// <param name="statusId">The column's Status.</param>
    /// <param name="after">The cursor - a well-formed key, held by a Task or not - or <c>null</c> for the column's start.</param>
    /// <param name="take">The most Tasks to read; a caller asks for one more than a page to learn whether another follows.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Tasks, materialised, by position key.</returns>
    Task<IReadOnlyList<TaskItem>> PageAsync(Guid projectId, Guid statusId, string? after, int take, CancellationToken cancellationToken);

    /// <summary>
    /// The Labels applied to each of <paramref name="taskIds"/> - the Tasks this Board read returns - each list by
    /// Label name and then id. Story 2.4; one statement for the whole page, never one per Task.
    /// </summary>
    /// <param name="taskIds">The Tasks being returned.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Label ids per Task id; a Task carrying none is absent.</returns>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LabelIdsOfAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken);
}
