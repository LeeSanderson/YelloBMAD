using Yello.Domain.Boards;
using Yello.Domain.Tasks;

namespace Yello.Application.Boards;

/// <summary>
/// One page of a Board column: the page size, and how a seek's rows become a page and its cursor. Story 2.9; AR-22,
/// AD-29.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Size"/> is an assumption</b> (AR-22, spine <c>:259</c>: "the seek shape is the invariant, the page
/// size is not"): 50 Tasks per column, stated once and tunable. The server owns it - a page route takes no size, because a
/// client-chosen size is a lever on an unbounded read.
/// </para>
/// <para>
/// <b>The cursor is the last served Task's position key</b> - readable, as FR-35 already makes it - and <c>null</c>
/// exactly when no Task follows: the seek asks for one more row than a page, and that row is never served.
/// </para>
/// </remarks>
public static class BoardPage
{
    private const int Fifty = 50;

    /// <summary>
    /// The most Tasks a column page holds.
    /// </summary>
    public static int Size => Fifty;

    /// <summary>
    /// Reads one page of a column: <see cref="Size"/> Tasks after <paramref name="after"/>, and the cursor.
    /// </summary>
    /// <param name="reader">The Board reader.</param>
    /// <param name="projectId">The Project.</param>
    /// <param name="statusId">The column's Status.</param>
    /// <param name="after">The cursor, or <c>null</c> for the first page.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The page's Tasks, by key, and its cursor.</returns>
    internal static async Task<(IReadOnlyList<TaskItem> Tasks, string? NextAfter)> ReadAsync(
        IBoardReader reader,
        Guid projectId,
        Guid statusId,
        string? after,
        CancellationToken cancellationToken)
    {
        var rows = await reader.PageAsync(projectId, statusId, after, Size + 1, cancellationToken).ConfigureAwait(false);

        return rows.Count > Size
            ? ([.. rows.Take(Size)], rows[Size - 1].PositionKey)
            : (rows, null);
    }
}
