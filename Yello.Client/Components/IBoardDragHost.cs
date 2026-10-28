namespace Yello.Client.Components;

/// <summary>
/// What the pointer and touch drag needs of the Board it is attached to (story 2.7): whether a Task may be lifted now,
/// a render when the lifted card or the drop zone changes, the drop - sent as story 2.6's move - and a cancel after a lift.
/// </summary>
public interface IBoardDragHost
{
    /// <summary>
    /// Whether <paramref name="taskId"/> may be lifted now: the Role moves Tasks, the Task is on the Board, and no panel is
    /// open over it. Lifting closes an open card menu.
    /// </summary>
    /// <param name="taskId">The Task.</param>
    /// <returns><c>true</c> when the lift stands.</returns>
    bool CanLift(Guid taskId);

    /// <summary>
    /// The lifted card or the drop zone has changed: the Board renders again.
    /// </summary>
    void DragChanged();

    /// <summary>
    /// The dragged Task was dropped over a slot: moved there through story 2.6's move, or - on its own slot - returned.
    /// </summary>
    /// <param name="taskId">The Task.</param>
    /// <param name="statusId">The destination column's Status.</param>
    /// <param name="index">The slot, counted without the dragged Task.</param>
    /// <returns>A task that completes once the move has been answered.</returns>
    Task DropAsync(Guid taskId, Guid statusId, int index);

    /// <summary>
    /// The person ended a lifted drag with nothing moved: the Task's return is announced.
    /// </summary>
    /// <param name="taskId">The Task.</param>
    /// <returns>A task.</returns>
    Task DragCancelledAsync(Guid taskId);
}
