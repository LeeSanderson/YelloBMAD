using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Yello.Client.Components;

/// <summary>
/// The Board's side of the pointer and touch drag (story 2.7, open question 1 (a)): it loads <c>js/board-drag.js</c>,
/// attaches it to the Board's row of columns for a Role that may move Tasks, and holds what the module reports - the
/// lifted Task and the slot under the pointer - for the Board to render the lifted card and the drop zone from. AC1, AC6,
/// AC9, AC10, AC13, AC15; UX-DR22, UX-DR23, UX-DR27.
/// </summary>
/// <remarks>
/// <para>
/// <b>.NET hears four things</b> (AC15): a lift, a change of destination slot, a drop and a cancel. Per-move work stays
/// in the browser, and nothing here reaches the network: a drop is handed to the Board, which sends it as story 2.6's move.
/// </para>
/// <para>
/// <b>Absent for a Viewer</b> (UX-DR27): the Board attaches the module only while its Role may move Tasks, and detaches it
/// when the Role stops permitting it - no listener, not a disabled drag.
/// </para>
/// <para>
/// <b>The Board ends a drag itself</b> when it purges, reads itself again, loses the dragged Task or opens the panel
/// (AC13): <see cref="End"/> clears the state and tells the module, silently - the Board already knows.
/// </para>
/// </remarks>
public sealed class BoardDrag : IAsyncDisposable
{
    private const string ModulePath = "./js/board-drag.js";

    private readonly IJSRuntime _js;
    private readonly IBoardDragHost _host;
    private DotNetObjectReference<BoardDrag>? _self;
    private IJSObjectReference? _module;
    private IJSObjectReference? _attached;
    private string? _attachedTo;
    private bool _disposed;

    /// <summary>
    /// Creates the drag for one Board.
    /// </summary>
    /// <param name="js">Blazor's interop.</param>
    /// <param name="host">The Board the drag reports to.</param>
    public BoardDrag(IJSRuntime js, IBoardDragHost host)
    {
        _js = js;
        _host = host;
    }

    /// <summary>
    /// The Task being dragged, once its lift has committed; <c>null</c> otherwise.
    /// </summary>
    public Guid? LiftedTaskId { get; private set; }

    /// <summary>
    /// The column the drop zone is in, or <c>null</c> when the pointer is outside every column.
    /// </summary>
    public Guid? TargetStatusId { get; private set; }

    /// <summary>
    /// The drop zone's slot in <see cref="TargetStatusId"/>'s column, counted without the dragged Task.
    /// </summary>
    public int TargetIndex { get; private set; }

    /// <summary>
    /// Attaches the module to the Board's row of columns, or re-attaches it when the row has been drawn afresh. Does
    /// nothing when already attached to it.
    /// </summary>
    /// <param name="columns">The row of columns.</param>
    /// <returns>A task.</returns>
    public async Task AttachAsync(ElementReference columns)
    {
        if (_disposed || string.Equals(_attachedTo, columns.Id, StringComparison.Ordinal))
        {
            return;
        }

        await DetachAsync();

        _module ??= await _js.InvokeAsync<IJSObjectReference>("import", ModulePath);
        _self ??= DotNetObjectReference.Create(this);
        _attached = await _module.InvokeAsync<IJSObjectReference>("attach", columns, _self);
        _attachedTo = columns.Id;
    }

    /// <summary>
    /// Ends any drag and lets go of the module's listeners: the Board is gone, or its Role no longer moves Tasks.
    /// </summary>
    /// <returns>A task.</returns>
    public async Task DetachAsync()
    {
        Clear();

        if (_attached is not { } attached)
        {
            return;
        }

        _attached = null;
        _attachedTo = null;

        await DisposeQuietlyAsync(attached, dispose: true);
    }

    /// <summary>
    /// Ends any drag from the Board's side - a purge, a re-read, the dragged Task deleted, the panel opened - with nothing
    /// moved and nothing said.
    /// </summary>
    public void End()
    {
        var dragging = LiftedTaskId is not null;

        Clear();

        if (dragging && _attached is { } attached)
        {
            _ = CancelQuietlyAsync(attached);
        }
    }

    /// <summary>
    /// The module: a lift has committed on a Task's card.
    /// </summary>
    /// <param name="taskId">The Task.</param>
    [JSInvokable]
    public void Lifted(string taskId)
    {
        if (!Guid.TryParse(taskId, out var id) || !_host.CanLift(id))
        {
            Clear();
            _ = CancelAttachedAsync();
            return;
        }

        LiftedTaskId = id;
        TargetStatusId = null;
        TargetIndex = 0;
        _host.DragChanged();
    }

    /// <summary>
    /// The module: the slot under the pointer has changed.
    /// </summary>
    /// <param name="statusId">The column's Status, or <c>null</c> when the pointer is outside every column.</param>
    /// <param name="index">The slot, counted without the dragged Task.</param>
    [JSInvokable]
    public void Targeted(string? statusId, int index)
    {
        if (LiftedTaskId is null)
        {
            return;
        }

        TargetStatusId = Guid.TryParse(statusId, out var id) ? id : null;
        TargetIndex = Math.Max(0, index);
        _host.DragChanged();
    }

    /// <summary>
    /// The module: the dragged card was released over a slot.
    /// </summary>
    /// <param name="statusId">The column's Status.</param>
    /// <param name="index">The slot, counted without the dragged Task.</param>
    /// <returns>A task that completes once the move has been answered.</returns>
    [JSInvokable("Dropped")]
    public Task DroppedAsync(string statusId, int index)
    {
        if (LiftedTaskId is not { } taskId)
        {
            return Task.CompletedTask;
        }

        Clear();

        return Guid.TryParse(statusId, out var id)
            ? _host.DropAsync(taskId, id, Math.Max(0, index))
            : _host.DragCancelledAsync(taskId);
    }

    /// <summary>
    /// The module: the person ended the drag with nothing moved - released outside every column, <c>Esc</c>, the
    /// platform's cancel, a second finger, the window losing focus.
    /// </summary>
    /// <returns>A task.</returns>
    [JSInvokable("Cancelled")]
    public Task CancelledAsync()
    {
        if (LiftedTaskId is not { } taskId)
        {
            return Task.CompletedTask;
        }

        Clear();

        return _host.DragCancelledAsync(taskId);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _disposed = true;

        await DetachAsync();

        if (_module is { } module)
        {
            _module = null;
            await DisposeQuietlyAsync(module, dispose: false);
        }

        _self?.Dispose();
        _self = null;
    }

    private static async Task CancelQuietlyAsync(IJSObjectReference attached)
    {
        try
        {
            await attached.InvokeVoidAsync("cancel");
        }
        catch (Exception exception) when (exception is JSException or ObjectDisposedException or InvalidOperationException)
        {
            // The module has gone with the page: there is no drag left to end.
        }
    }

    private static async Task DisposeQuietlyAsync(IJSObjectReference reference, bool dispose)
    {
        try
        {
            if (dispose)
            {
                await reference.InvokeVoidAsync("dispose");
            }

            await reference.DisposeAsync();
        }
        catch (Exception exception) when (exception is JSException or ObjectDisposedException or InvalidOperationException)
        {
            // The module has gone with the page: there is nothing left to let go of.
        }
    }

    private Task CancelAttachedAsync() => _attached is { } attached ? CancelQuietlyAsync(attached) : Task.CompletedTask;

    private void Clear()
    {
        LiftedTaskId = null;
        TargetStatusId = null;
        TargetIndex = 0;
    }
}
