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
/// (AC13): <see cref="End"/> clears the state and tells the module to end any gesture, pending or lifted, silently - the
/// Board already knows. A lift already on its way when that happens is reported back as abandoned.
/// </para>
/// <para>
/// <b>Attaching and detaching run one at a time</b>, in the order asked: the Board asks after every render, and renders
/// overlap, so an attach still awaiting the module must not be raced by a second one (two listener sets) or outlived by
/// a dispose (listeners on a dead Board). A module that cannot be loaded leaves the Board without the drag - the Move
/// control is the canonical path (UX-DR21) - and is not retried on every render.
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
    private Task _queue = Task.CompletedTask;

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
    public Task AttachAsync(ElementReference columns)
    {
        if (_disposed || (_queue.IsCompleted && string.Equals(_attachedTo, columns.Id, StringComparison.Ordinal)))
        {
            return Task.CompletedTask;
        }

        return EnqueueAsync(() => AttachNowAsync(columns));
    }

    /// <summary>
    /// Ends any drag and lets go of the module's listeners: the Board is gone, or its Role no longer moves Tasks.
    /// </summary>
    /// <returns>A task.</returns>
    public Task DetachAsync()
    {
        Clear();

        return _queue.IsCompleted && _attached is null && _attachedTo is null ? Task.CompletedTask : EnqueueAsync(DetachNowAsync);
    }

    /// <summary>
    /// Ends any drag from the Board's side - a purge, a re-read, the dragged Task deleted, the panel opened - pending or
    /// lifted, with nothing moved and nothing said.
    /// </summary>
    public void End()
    {
        Clear();

        if (_attached is { } attached)
        {
            _ = CancelQuietlyAsync(attached);
        }
    }

    /// <summary>
    /// The module: a lift has committed on a Task's card.
    /// </summary>
    /// <param name="taskId">The Task.</param>
    /// <returns>Whether the lift was taken; when it was not, the module has been told to end the gesture.</returns>
    [JSInvokable]
    public bool Lifted(string taskId)
    {
        if (!Guid.TryParse(taskId, out var id) || !_host.CanLift(id))
        {
            Clear();
            _ = CancelAttachedAsync();
            return false;
        }

        LiftedTaskId = id;
        TargetStatusId = null;
        TargetIndex = 0;
        _host.DragChanged();

        return true;
    }

    /// <summary>
    /// The module: a lift taken here belonged to a gesture the Board had already ended - its <see cref="Lifted"/> was on
    /// its way when <see cref="End"/> ran. It is let go of, with nothing said.
    /// </summary>
    /// <param name="taskId">The Task.</param>
    [JSInvokable]
    public void Abandoned(string taskId)
    {
        if (Guid.TryParse(taskId, out var id) && LiftedTaskId == id)
        {
            Clear();
            _host.DragChanged();
        }
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
        Clear();

        // Queued behind any attach still in flight, so that what that attach made is let go of too.
        await EnqueueAsync(async () =>
        {
            await DetachNowAsync();

            if (_module is { } module)
            {
                _module = null;
                await DisposeQuietlyAsync(module, dispose: false);
            }

            _self?.Dispose();
            _self = null;
        });
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

    private static async Task RunAfterAsync(Task previous, Func<Task> work)
    {
        await previous;
        await work();
    }

    private Task EnqueueAsync(Func<Task> work)
    {
        _queue = RunAfterAsync(_queue, work);

        return _queue;
    }

    private async Task AttachNowAsync(ElementReference columns)
    {
        if (_disposed || string.Equals(_attachedTo, columns.Id, StringComparison.Ordinal))
        {
            return;
        }

        await DetachNowAsync();

        try
        {
            _module ??= await _js.InvokeAsync<IJSObjectReference>("import", ModulePath);
            _self ??= DotNetObjectReference.Create(this);

            var attached = await _module.InvokeAsync<IJSObjectReference>("attach", columns, _self);

            if (_disposed)
            {
                await DisposeQuietlyAsync(attached, dispose: true);
                return;
            }

            _attached = attached;
        }
        catch (JSException)
        {
            // The module could not be loaded or attached. This Board has no drag, and the Move control stays the way to
            // move a Task (UX-DR21). The columns are still recorded, so that every render does not try again.
        }

        _attachedTo = columns.Id;
    }

    private async Task DetachNowAsync()
    {
        _attachedTo = null;

        if (_attached is not { } attached)
        {
            return;
        }

        _attached = null;

        await DisposeQuietlyAsync(attached, dispose: true);
    }

    private Task CancelAttachedAsync() => _attached is { } attached ? CancelQuietlyAsync(attached) : Task.CompletedTask;

    private void Clear()
    {
        LiftedTaskId = null;
        TargetStatusId = null;
        TargetIndex = 0;
    }
}
