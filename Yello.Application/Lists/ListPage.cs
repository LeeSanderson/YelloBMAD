using Yello.Domain.Lists;
using Yello.Domain.Tasks;

namespace Yello.Application.Lists;

/// <summary>
/// One page of a List View: the page size, and how a seek's rows become a page and its cursor. Story 2.10; AR-22, AD-29,
/// open question 2 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Size"/> is an assumption</b> (AR-22, spine <c>:259</c>: "the seek shape is the invariant, the page size
/// is not"): 50 Tasks per page, stated once and tunable, the Board's size. The server owns it - the List route takes no
/// size, because a client-chosen size is a lever on an unbounded read (AC12).
/// </para>
/// <para>
/// <b>The cursor is the last served Task's pair</b> under the page's sort (<see cref="ListCursorText"/>), and <c>null</c>
/// exactly when no Task follows: the seek asks for one more row than a page, and that row is never served.
/// </para>
/// </remarks>
public static class ListPage
{
    private const int Fifty = 50;

    /// <summary>
    /// The most Tasks a List page holds.
    /// </summary>
    public static int Size => Fifty;

    /// <summary>
    /// Reads one page: <see cref="Size"/> Tasks after <paramref name="after"/>, and the next cursor.
    /// </summary>
    /// <param name="reader">The List reader.</param>
    /// <param name="projectId">The Project.</param>
    /// <param name="filter">The narrowed filter.</param>
    /// <param name="sort">The sort.</param>
    /// <param name="statusOrder">The Statuses to list under the Status sort, in effective order.</param>
    /// <param name="after">The cursor, or <c>null</c> for the first page.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The page's Tasks, in the sort's order, and its cursor text.</returns>
    internal static async Task<(IReadOnlyList<TaskItem> Tasks, string? NextAfter)> ReadAsync(
        IListReader reader,
        Guid projectId,
        ListFilter filter,
        ListSort sort,
        IReadOnlyList<Guid> statusOrder,
        ListCursor? after,
        CancellationToken cancellationToken)
    {
        var rows = await reader.PageAsync(projectId, filter, sort, statusOrder, after, Size + 1, cancellationToken).ConfigureAwait(false);

        return rows.Count > Size
            ? ([.. rows.Take(Size)], ListCursorText.Write(sort, ListCursor.After(rows[Size - 1])))
            : (rows, null);
    }
}
