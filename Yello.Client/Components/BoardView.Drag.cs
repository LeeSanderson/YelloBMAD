using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Yello.Client.Components;

/// <summary>
/// The Board's half of the pointer and touch drag (story 2.7): the lifted card and the drop zone rendered from what
/// <see cref="BoardDrag"/> reports, and the drop handed to story 2.6's move. AC1, AC6, AC9-AC14; UX-DR22, UX-DR23,
/// UX-DR25, UX-DR27.
/// </summary>
/// <remarks>
/// <para>
/// <b>One write path</b> (AC9; FR-29, FR-35, AD-15). A drop names the destination Status and the Task immediately above the
/// drop zone - counted without the dragged Task, <c>null</c> for the top - and goes through <see cref="MoveAsync"/> as a
/// <see cref="MoveChoice"/>, exactly as a picker move does: optimistic, re-placed by the server's key, announced on
/// admission, reverted and said on the Board on a refusal. There is no second route, no key from the client.
/// </para>
/// <para>
/// <b>A drop on the Task's own slot sends nothing</b> (open question 5 (a)): same Status, same neighbour above. It is a
/// cancel after a lift, and like every such cancel it says where the Task is: "Returned to {Status}, position {n}." (open
/// question 7 (a)). Nothing is said on the lift, on crossing a column, or for a pending lift a pan cancelled (AC14).
/// </para>
/// </remarks>
public sealed partial class BoardView
{
    private BoardDrag? _drag;
    private ElementReference _columnsElement;

    // The card a drag just dropped or returned, whose settle is animated (UX-DR6, motion.settle).
    private Guid? _settlingCard;

    /// <summary>
    /// The drop zone's slot in <paramref name="column"/>, counted without the dragged Task, or <c>null</c> when the zone is
    /// not in it.
    /// </summary>
    private int? DropIndexIn(ColumnState column) =>
        _drag is { LiftedTaskId: not null, TargetStatusId: { } statusId } drag && statusId == column.StatusId ? drag.TargetIndex : null;

    /// <summary>
    /// The drag is attached while the Board is drawn for a Role that may move Tasks, and only then (UX-DR27).
    /// </summary>
    private Task AttachDragAsync()
    {
        if (_drag is not { } drag)
        {
            return Task.CompletedTask;
        }

        return _canManage && _columns is not null ? drag.AttachAsync(_columnsElement) : drag.DetachAsync();
    }

    /// <inheritdoc />
    bool IBoardDragHost.CanLift(Guid taskId)
    {
        if (!_canManage || _openTaskId is not null || Locate(taskId) is null)
        {
            return false;
        }

        // Starting a drag closes an open card menu (story 2.7, open question 4 (a)).
        _menuTaskId = null;
        _menuPicking = false;
        _settlingCard = null;

        return true;
    }

    /// <inheritdoc />
    void IBoardDragHost.DragChanged() => StateHasChanged();

    /// <inheritdoc />
    async Task IBoardDragHost.DropAsync(Guid taskId, Guid statusId, int index)
    {
        if (!_canManage || Locate(taskId) is not { } found || _columns?.Find(column => column.StatusId == statusId) is not { } destination)
        {
            StateHasChanged();
            return;
        }

        var others = destination.Tasks.FindAll(task => task.Id != taskId);
        var at = Math.Min(index, others.Count);
        Guid? after = at == 0 ? null : others[at - 1].Id;

        var (column, slot) = found;
        Guid? current = slot == 0 ? null : column.Tasks[slot - 1].Id;

        if (column.StatusId == statusId && current == after)
        {
            Returned(taskId);
            StateHasChanged();
            return;
        }

        // The optimistic placement is made before the first await, so it renders now; the answer renders when it is in.
        var moving = MoveAsync(taskId, new MoveChoice(statusId, after), MoveOrigin.Drag);

        StateHasChanged();
        await moving;
        StateHasChanged();
    }

    /// <inheritdoc />
    Task IBoardDragHost.DragCancelledAsync(Guid taskId)
    {
        Returned(taskId);
        StateHasChanged();

        return Task.CompletedTask;
    }

    /// <summary>
    /// A lifted Task put back where it was, with nothing sent: it settles, and its slot is announced.
    /// </summary>
    private void Returned(Guid taskId)
    {
        if (Locate(taskId) is not { } found)
        {
            return;
        }

        _settlingCard = taskId;
        _announcement = string.Format(CultureInfo.CurrentCulture, Localizer["TaskReturned"], found.Column.Name, found.Index + 1);
    }
}
