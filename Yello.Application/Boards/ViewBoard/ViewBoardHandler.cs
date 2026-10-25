using Yello.Domain.Boards;
using Yello.Domain.Projects;
using Yello.Domain.Statuses;
using Yello.Domain.Tasks;

namespace Yello.Application.Boards.ViewBoard;

/// <summary>
/// Reads one Project's Board: one column per Status of its effective set, in effective order, each with
/// the true total for that Status and its Tasks. FR-28; story 2.2, AC1, AC4, AC5.
/// </summary>
/// <remarks>
/// <para>
/// <b>No Role, no Space and no count of anything a bound limits</b> (AR-3). The pipeline resolved the Space
/// and checked the read capability, which every Role holds, so a Viewer is served exactly a Member's Board.
/// </para>
/// <para>
/// <b>A column's total is the database's count, never the number of Tasks placed in it</b> (AD-29). Today
/// every Task is returned (open question 6 (a)); story 2.9 pages them, and the totals do not move.
/// </para>
/// <para>
/// <b>Each Task lands in exactly one column</b> (AC4): the column of its own Status. A Task whose Status is
/// outside the effective set cannot exist (AD-17: a composite foreign key and a create that takes the first
/// Status), so meeting one is a broken invariant and fails loudly rather than dropping the Task.
/// </para>
/// </remarks>
/// <param name="reader">Reads the Project, its counts and its Tasks.</param>
/// <param name="statuses">Reads the Space's Status definitions.</param>
public sealed class ViewBoardHandler(IBoardReader reader, IStatusDefinitionReader statuses)
{
    /// <summary>
    /// Reads the Board.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Board, or <c>null</c> when no Project in the active Space has that id.</returns>
    public async Task<BoardView?> HandleAsync(Guid projectId, CancellationToken cancellationToken)
    {
        if (await reader.FindProjectAsync(projectId, cancellationToken).ConfigureAwait(false) is not { } project)
        {
            return null;
        }

        var effective = EffectiveStatusSet.Of(await statuses.ListAsync(cancellationToken).ConfigureAwait(false));
        var totals = await reader.CountTasksByStatusAsync(projectId, cancellationToken).ConfigureAwait(false);
        var tasks = await reader.ListTasksAsync(projectId, cancellationToken).ConfigureAwait(false);

        var placed = effective.ToDictionary(status => status.Id, _ => new List<TaskItem>());

        foreach (var task in tasks)
        {
            if (!placed.TryGetValue(task.StatusId, out var column))
            {
                throw new InvalidOperationException("A Task holds a Status outside its Project's effective set (AD-17).");
            }

            column.Add(task);
        }

        var columns = effective
            .Select(status => new BoardColumnView(status, totals.GetValueOrDefault(status.Id), placed[status.Id]))
            .ToList();

        return new BoardView(project, columns);
    }
}

/// <summary>
/// One Project's Board.
/// </summary>
/// <param name="Project">The Project.</param>
/// <param name="Columns">One column per Status of its effective set, in effective order.</param>
public sealed record BoardView(Project Project, IReadOnlyList<BoardColumnView> Columns);

/// <summary>
/// One column of a Board.
/// </summary>
/// <param name="Status">The column's Status.</param>
/// <param name="Total">The true number of the Project's Tasks in that Status, counted by the database.</param>
/// <param name="Tasks">The Tasks placed in it, by position key and then id.</param>
public sealed record BoardColumnView(StatusDefinition Status, int Total, IReadOnlyList<TaskItem> Tasks);
