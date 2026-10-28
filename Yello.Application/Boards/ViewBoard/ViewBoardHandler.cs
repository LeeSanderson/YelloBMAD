using Yello.Domain.Boards;
using Yello.Domain.Projects;
using Yello.Domain.Statuses;
using Yello.Domain.Tasks;

namespace Yello.Application.Boards.ViewBoard;

/// <summary>
/// Reads one Project's Board: one column per Status of its effective set, in effective order, each with
/// the true total for that Status and its first page of Tasks. FR-28; story 2.2, AC1, AC4, AC5; story 2.9, AC1, AC9.
/// </summary>
/// <remarks>
/// <para>
/// <b>No Role, no Space and no count of anything a bound limits</b> (AR-3). The pipeline resolved the Space
/// and checked the read capability, which every Role holds, so a Viewer is served exactly a Member's Board.
/// </para>
/// <para>
/// <b>A column's total is the database's count, never the number of Tasks placed in it</b> (AD-29): one grouped
/// <c>COUNT</c> for every column.
/// </para>
/// <para>
/// <b>First paint is one bounded seek per column</b> (story 2.9, AC1, test design P-2): a loop over the effective
/// Statuses - three to five statements, each a <c>TOP (51)</c> keyset seek of one column - never a
/// <c>ROW_NUMBER() OVER (PARTITION BY StatusId)</c>, which would read the whole Project. Each column carries
/// <see cref="BoardPage.Size"/> Tasks at most and its cursor; the rest is <c>ViewBoardColumnHandler</c>'s.
/// </para>
/// <para>
/// <b>Statements, not one snapshot</b> (story 2.9, AC13, open question 10 (a)): under <c>READ_COMMITTED_SNAPSHOT</c> the
/// totals, each column's page and the Labels each read their own committed snapshot, never waiting on a writer. They may
/// disagree by the writes that commit between them - bounded to concurrent change, and corrected by the next read.
/// </para>
/// <para>
/// <b>Each Task lands in exactly one column</b> (AC4): each column is read by its own Status, so a Task appears only in
/// the column of its Status. A Task whose Status is outside the effective set cannot exist (AD-17: a composite foreign
/// key and a create that takes the first Status), and no column would read it.
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
        var pages = new List<(StatusDefinition Status, IReadOnlyList<TaskItem> Tasks, string? NextAfter)>();

        foreach (var status in effective)
        {
            var (tasks, nextAfter) = await BoardPage.ReadAsync(reader, projectId, status.Id, after: null, cancellationToken).ConfigureAwait(false);

            pages.Add((status, tasks, nextAfter));
        }

        var labels = await reader.LabelIdsOfAsync([.. pages.SelectMany(page => page.Tasks).Select(task => task.Id)], cancellationToken).ConfigureAwait(false);

        var columns = pages
            .Select(page => new BoardColumnView(page.Status, totals.GetValueOrDefault(page.Status.Id), page.Tasks, page.NextAfter))
            .ToList();

        return new BoardView(project, columns, labels);
    }
}

/// <summary>
/// One Project's Board.
/// </summary>
/// <param name="Project">The Project.</param>
/// <param name="Columns">One column per Status of its effective set, in effective order.</param>
/// <param name="LabelIds">
/// The Labels applied to each Task placed on the Board, by Label name then id; a Task carrying none is absent.
/// Story 2.4.
/// </param>
public sealed record BoardView(
    Project Project,
    IReadOnlyList<BoardColumnView> Columns,
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> LabelIds);

/// <summary>
/// One column of a Board.
/// </summary>
/// <param name="Status">The column's Status.</param>
/// <param name="Total">The true number of the Project's Tasks in that Status, counted by the database.</param>
/// <param name="Tasks">Its first page of Tasks, by position key.</param>
/// <param name="NextAfter">The cursor: the last Task's key when another follows, otherwise <c>null</c> (story 2.9).</param>
public sealed record BoardColumnView(StatusDefinition Status, int Total, IReadOnlyList<TaskItem> Tasks, string? NextAfter);
