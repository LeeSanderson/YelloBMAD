using Yello.Domain.Boards;
using Yello.Domain.Statuses;
using Yello.Domain.Tasks;

namespace Yello.Application.Boards.ViewBoardColumn;

/// <summary>
/// Reads one page of one Board column: the Tasks of a Status whose position key sorts after a cursor, at most
/// <see cref="BoardPage.Size"/>, with the column's true total and the next cursor. FR-28; story 2.9, AC1, AC4, AC9, open
/// questions 1 (a) and 2 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>One bounded keyset seek per request</b> (AD-29): the cursor is a key, not an id or an ordinal, so the page after a
/// key no Task holds any more - deleted, moved away - is still the page of the first key after it, and nothing is
/// skipped or repeated. The cursor's shape is checked before it reaches here (<see cref="PositionKey.IsWellFormed"/>).
/// </para>
/// <para>
/// <b>The total is a separate <c>COUNT</c> over the same predicate</b> (AC4), never the page's length - one more
/// statement, a seek on the same index. Its snapshot is its own (open question 10 (a)); see <c>ViewBoardHandler</c>.
/// </para>
/// <para>
/// <b>A Project that matches nothing, or a Status outside the Project's effective set, is <c>null</c></b>: the endpoint
/// answers the boundary's <c>404 not-available</c>, byte for byte, as for a Space that does not exist (AD-3). Another
/// Space's Project and Status ids are never visible here (RLS and the query filter).
/// </para>
/// </remarks>
/// <param name="reader">Reads the Project, the column's count and its page.</param>
/// <param name="statuses">Reads the Space's Status definitions.</param>
public sealed class ViewBoardColumnHandler(IBoardReader reader, IStatusDefinitionReader statuses)
{
    /// <summary>
    /// Reads the page.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="statusId">The column's Status, from the route.</param>
    /// <param name="after">A well-formed cursor, or <c>null</c> for the first page.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The page, or <c>null</c> when the Project or the Status is not one of the active Space's Board's.</returns>
    public async Task<BoardColumnPage?> HandleAsync(Guid projectId, Guid statusId, string? after, CancellationToken cancellationToken)
    {
        if (await reader.FindProjectAsync(projectId, cancellationToken).ConfigureAwait(false) is null)
        {
            return null;
        }

        var effective = EffectiveStatusSet.Of(await statuses.ListAsync(cancellationToken).ConfigureAwait(false));

        if (!EffectiveStatusSet.Contains(effective, statusId))
        {
            return null;
        }

        var total = await reader.CountTasksAsync(projectId, statusId, cancellationToken).ConfigureAwait(false);
        var (tasks, nextAfter) = await BoardPage.ReadAsync(reader, projectId, statusId, after, cancellationToken).ConfigureAwait(false);
        var labels = await reader.LabelIdsOfAsync([.. tasks.Select(task => task.Id)], cancellationToken).ConfigureAwait(false);

        return new BoardColumnPage(statusId, total, tasks, nextAfter, labels);
    }
}

/// <summary>
/// One page of one Board column.
/// </summary>
/// <param name="StatusId">The column's Status.</param>
/// <param name="Total">The true number of the Project's Tasks in that Status, counted by the database.</param>
/// <param name="Tasks">The page's Tasks, by position key.</param>
/// <param name="NextAfter">The cursor: the last Task's key when another follows, otherwise <c>null</c>.</param>
/// <param name="LabelIds">The Labels applied to each of the page's Tasks; a Task carrying none is absent.</param>
public sealed record BoardColumnPage(
    Guid StatusId,
    int Total,
    IReadOnlyList<TaskItem> Tasks,
    string? NextAfter,
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> LabelIds);
