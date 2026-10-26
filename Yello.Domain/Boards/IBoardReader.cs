using Yello.Domain.Projects;
using Yello.Domain.Tasks;

namespace Yello.Domain.Boards;

/// <summary>
/// Reads what one Project's Board shows, in the Space the current unit of work is scoped to. FR-28;
/// story 2.2.
/// </summary>
/// <remarks>
/// <para>
/// <b>The totals are their own read</b> (AD-29, AC5): a count per Status over the indexed predicate, never
/// the number of Tasks another call happened to return. Story 2.9 changes how many Tasks come back; it
/// never changes what a column's total means.
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
    /// The Project's Tasks, by position key and then id.
    /// </summary>
    /// <param name="projectId">The Project.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Tasks, materialised.</returns>
    Task<IReadOnlyList<TaskItem>> ListTasksAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// The Labels applied to each of <paramref name="taskIds"/> - the Tasks this Board read returns - each list by
    /// Label name and then id. Story 2.4; one statement for the whole page, never one per Task.
    /// </summary>
    /// <param name="taskIds">The Tasks being returned.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Label ids per Task id; a Task carrying none is absent.</returns>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LabelIdsOfAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken);
}
