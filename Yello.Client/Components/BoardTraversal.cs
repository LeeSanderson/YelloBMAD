namespace Yello.Client.Components;

/// <summary>
/// The Board's arrow grammar as pure logic (story 2.8, AC1, AC2, AC5; UX-DR24, UX-DR42): no Blazor, no DOM - the columns
/// are their lengths, in the Board's order.
/// </summary>
/// <remarks>
/// <para>
/// <b>Traversal</b>: <c>↑</c>/<c>↓</c> move within a column, clamped at its ends (no wrap), and reset the run's origin to the
/// index they land on. <c>←</c>/<c>→</c> keep the origin and land at it clamped to the next column's length - the STICKY
/// ORIGIN INDEX, never re-derived per hop - or on the column itself when it is empty; they stop at the first and last
/// column. A run is the caller's: any other key, or focus moved by anything but the traversal, starts a new one.
/// </para>
/// <para>
/// <b>Carry</b>: the same grammar over slots, the carried Task left out of every count. A column of <c>n</c> others has
/// slots <c>0..n</c> - <c>n</c> is the bottom - so an empty column takes slot 0 and the destination always has two
/// concrete neighbours, or the top, or the bottom (AC5).
/// </para>
/// </remarks>
public static class BoardTraversal
{
    /// <summary>
    /// The key a <c>keydown</c>'s <c>key</c> is, the horizontal arrows mapped by the Board's direction (UX-DR42): the Status
    /// sequence never reverses, so only the keys mirror.
    /// </summary>
    /// <param name="key">The DOM <c>KeyboardEvent.key</c>.</param>
    /// <param name="rightToLeft">Whether the Board's computed direction is <c>rtl</c>.</param>
    /// <returns>The key.</returns>
    public static BoardKey KeyOf(string key, bool rightToLeft) => key switch
    {
        "ArrowLeft" or "ArrowRight" => Horizontal(key == "ArrowRight", rightToLeft),
        "ArrowUp" => BoardKey.Up,
        "ArrowDown" => BoardKey.Down,
        " " => BoardKey.Space,
        "Escape" => BoardKey.Escape,
        _ => Named(key),
    };

    /// <summary>
    /// The Status pager's selection after a key (story 2.11, AC4, AC9, open question 3 (a)): the inline keys step one tab by
    /// logical direction - already mirrored by <see cref="KeyOf"/> - and clamp at the ends, never wrapping; <c>Home</c> and
    /// <c>End</c> go to the first and last. The tabs are the Status sequence, which never reverses.
    /// </summary>
    /// <param name="count">How many tabs there are.</param>
    /// <param name="selected">The selected tab's position.</param>
    /// <param name="key">The key.</param>
    /// <returns>The position of the tab selected next.</returns>
    public static int Pager(int count, int selected, BoardKey key) => key switch
    {
        BoardKey.InlineStart => Math.Max(selected - 1, 0),
        BoardKey.InlineEnd => Math.Min(selected + 1, count - 1),
        BoardKey.Home => 0,
        BoardKey.End => count - 1,
        _ => selected,
    };

    /// <summary>
    /// One traversal step at a width (story 2.11, AC12, open question 5 (a)): below 768px the pager is the only route to
    /// another Status, so <c>←</c>/<c>→</c> move nothing - the key is still consumed by the caller - and every other key is
    /// <see cref="Traverse(IReadOnlyList{int}, ValueTuple{int, int?}, BoardKey, int)"/>'s.
    /// </summary>
    /// <param name="counts">Each column's number of Tasks, in the Board's order.</param>
    /// <param name="stop">Where focus stands.</param>
    /// <param name="key">The key.</param>
    /// <param name="origin">The run's sticky origin index.</param>
    /// <param name="narrow">Whether the viewport is below 768px.</param>
    /// <returns>Where focus goes, and the run's origin after the step.</returns>
    public static ((int Column, int? Index) Stop, int Origin) Traverse(IReadOnlyList<int> counts, (int Column, int? Index) stop, BoardKey key, int origin, bool narrow) =>
        narrow && IsInline(key) ? (stop, origin) : Traverse(counts, stop, key, origin);

    /// <summary>
    /// One traversal step.
    /// </summary>
    /// <param name="counts">Each column's number of Tasks, in the Board's order.</param>
    /// <param name="stop">
    /// Where focus stands: a column by its position, and a Task by its index in it - or <c>null</c> for the column itself, a
    /// stop only while it is empty (open question 4 (a)).
    /// </param>
    /// <param name="key">The key.</param>
    /// <param name="origin">The run's sticky origin index.</param>
    /// <returns>Where focus goes, and the run's origin after the step.</returns>
    public static ((int Column, int? Index) Stop, int Origin) Traverse(IReadOnlyList<int> counts, (int Column, int? Index) stop, BoardKey key, int origin)
    {
        var count = counts[stop.Column];

        switch (key)
        {
            case BoardKey.Up or BoardKey.Down when count == 0:
                return (stop, origin);

            case BoardKey.Up or BoardKey.Down when stop.Index is null:
                // A column focused as a whole, holding Tasks (a card in it was deleted): its first card.
                return ((stop.Column, 0), 0);

            case BoardKey.Up or BoardKey.Down:
                var index = Math.Clamp(stop.Index.GetValueOrDefault() + (key == BoardKey.Up ? -1 : 1), 0, count - 1);
                return ((stop.Column, index), index);

            case BoardKey.InlineStart or BoardKey.InlineEnd:
                var column = stop.Column + (key == BoardKey.InlineStart ? -1 : 1);

                if (column < 0 || column >= counts.Count)
                {
                    return (stop, origin);
                }

                return counts[column] == 0
                    ? ((column, null), origin)
                    : ((column, Math.Min(origin, counts[column] - 1)), origin);

            default:
                return (stop, origin);
        }
    }

    /// <summary>
    /// One carried step at a width (story 2.11, AC12): below 768px a carried <c>←</c>/<c>→</c> leaves the Task's destination
    /// where it is - a cross-Status move is the Move control's - and <c>↑</c>/<c>↓</c> reorder within the column as always.
    /// </summary>
    /// <param name="others">Each column's number of Tasks other than the carried one, in the Board's order.</param>
    /// <param name="column">The destination column's position.</param>
    /// <param name="slot">The destination slot in it.</param>
    /// <param name="key">The key.</param>
    /// <param name="origin">The carry's sticky origin index.</param>
    /// <param name="narrow">Whether the viewport is below 768px.</param>
    /// <returns>The new destination column, slot and origin.</returns>
    public static (int Column, int Slot, int Origin) Carry(IReadOnlyList<int> others, int column, int slot, BoardKey key, int origin, bool narrow) =>
        narrow && IsInline(key) ? (column, slot, origin) : Carry(others, column, slot, key, origin);

    /// <summary>
    /// One step of a carried Task's destination slot.
    /// </summary>
    /// <param name="others">Each column's number of Tasks other than the carried one, in the Board's order.</param>
    /// <param name="column">The destination column's position.</param>
    /// <param name="slot">The destination slot in it, counted without the carried Task.</param>
    /// <param name="key">The key.</param>
    /// <param name="origin">The carry's sticky origin index.</param>
    /// <returns>The new destination column, slot and origin.</returns>
    public static (int Column, int Slot, int Origin) Carry(IReadOnlyList<int> others, int column, int slot, BoardKey key, int origin)
    {
        switch (key)
        {
            case BoardKey.Up or BoardKey.Down:
                var moved = Math.Clamp(slot + (key == BoardKey.Up ? -1 : 1), 0, others[column]);
                return (column, moved, moved);

            case BoardKey.InlineStart or BoardKey.InlineEnd:
                var next = column + (key == BoardKey.InlineStart ? -1 : 1);

                return next < 0 || next >= others.Count
                    ? (column, slot, origin)
                    : (next, Math.Min(origin, others[next]), origin);

            default:
                return (column, slot, origin);
        }
    }

    private static BoardKey Named(string key) => key switch
    {
        "Tab" => BoardKey.Tab,
        "Home" => BoardKey.Home,
        "End" => BoardKey.End,
        _ => BoardKey.Other,
    };

    private static bool IsInline(BoardKey key) => key is BoardKey.InlineStart or BoardKey.InlineEnd;

    private static BoardKey Horizontal(bool right, bool rightToLeft) =>
        right != rightToLeft ? BoardKey.InlineEnd : BoardKey.InlineStart;
}
