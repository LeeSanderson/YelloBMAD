namespace Yello.Client.Components;

/// <summary>
/// The List View's keyboard row traversal as pure logic (story 2.10, Task 6, open question 8 (a); AC7, AC18; UX-DR24):
/// the rows are one roving tab stop, <c>↑</c>/<c>↓</c> move between loaded rows, <c>Home</c>/<c>End</c> go to the first and
/// last loaded rows, and <c>↓</c> on the last loaded row asks for the next page - ignored while one is already out.
/// </summary>
/// <remarks>
/// <b>Its own, not <see cref="BoardTraversal"/>'s vertical half</b>: the Board's <c>↑</c>/<c>↓</c> clamp inside a column and
/// reset a sticky origin that the inline arrows read, and paging past a column's end is a separate show-more path there.
/// The list has one axis, no origin to keep, and pages from the arrow itself, so sharing would have bent the Board's rules
/// to carry a mode it does not need. Vertical only, so right-to-left needs nothing.
/// </remarks>
public static class ListTraversal
{
    /// <summary>
    /// Where a key on the row at <paramref name="at"/> takes focus.
    /// </summary>
    /// <param name="count">How many rows are loaded.</param>
    /// <param name="at">The focused row's index among them.</param>
    /// <param name="key">The key, as the browser names it.</param>
    /// <param name="hasMore">Whether Tasks follow the loaded ones (the list holds a cursor).</param>
    /// <param name="loading">Whether a page is already out.</param>
    /// <returns>The step to take, and the row index for <see cref="ListStep.Move"/>.</returns>
    public static (ListStep Step, int Index) Next(int count, int at, string key, bool hasMore, bool loading)
    {
        if (count == 0 || at < 0 || at >= count)
        {
            return (ListStep.None, at);
        }

        return key switch
        {
            "ArrowUp" => MoveTo(at, at - 1, count),
            "ArrowDown" => Down(count, at, hasMore, loading),
            "Home" => MoveTo(at, 0, count),
            "End" => MoveTo(at, count - 1, count),
            _ => (ListStep.None, at),
        };
    }

    /// <summary>
    /// <c>↓</c>: the next loaded row; past the last, the next page when one follows and none is out.
    /// </summary>
    private static (ListStep Step, int Index) Down(int count, int at, bool hasMore, bool loading)
    {
        if (at < count - 1)
        {
            return (ListStep.Move, at + 1);
        }

        return hasMore && !loading ? (ListStep.LoadMore, at) : (ListStep.None, at);
    }

    /// <summary>
    /// A move to <paramref name="to"/>, or nothing at an edge or where focus already is.
    /// </summary>
    private static (ListStep Step, int Index) MoveTo(int at, int to, int count) =>
        to >= 0 && to < count && to != at ? (ListStep.Move, to) : (ListStep.None, at);
}

/// <summary>
/// What a key does to the List View's roving row (<see cref="ListTraversal.Next"/>).
/// </summary>
public enum ListStep
{
    /// <summary>
    /// Nothing: an edge, a page already out, or a key the list does not use.
    /// </summary>
    None,

    /// <summary>
    /// Focus moves to another loaded row.
    /// </summary>
    Move,

    /// <summary>
    /// The next page is read, and focus lands on its first Task by identity once it is drawn.
    /// </summary>
    LoadMore,
}
