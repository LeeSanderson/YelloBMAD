namespace Yello.Client.Components;

/// <summary>
/// A key the Board's keyboard composite acts on (story 2.8, UX-DR24), the arrows already mapped to LOGICAL directions.
/// </summary>
public enum BoardKey
{
    /// <summary>
    /// A key the composite does not act on; it ends a traversal run.
    /// </summary>
    Other,

    /// <summary>
    /// <c>↑</c>: the Task above, within the column.
    /// </summary>
    Up,

    /// <summary>
    /// <c>↓</c>: the Task below, within the column.
    /// </summary>
    Down,

    /// <summary>
    /// The column at the inline start: <c>←</c> left to right, <c>→</c> right to left (UX-DR42).
    /// </summary>
    InlineStart,

    /// <summary>
    /// The column at the inline end: <c>→</c> left to right, <c>←</c> right to left (UX-DR42).
    /// </summary>
    InlineEnd,

    /// <summary>
    /// <c>Space</c> on a mover's card: pick the Task up, or drop it.
    /// </summary>
    Space,

    /// <summary>
    /// <c>Esc</c> during a carry: cancel it.
    /// </summary>
    Escape,

    /// <summary>
    /// <c>Tab</c> during a carry: cancel it, so focus never leaves with a Task in hand.
    /// </summary>
    Tab,

    /// <summary>
    /// <c>Home</c> on the Status pager: its first tab (story 2.11, AC9). On the composite it ends a run, as any other key.
    /// </summary>
    Home,

    /// <summary>
    /// <c>End</c> on the Status pager: its last tab (story 2.11, AC9). On the composite it ends a run, as any other key.
    /// </summary>
    End,
}
