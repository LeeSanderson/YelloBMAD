using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Yello.Client.Components;

/// <summary>
/// The Board's side of its keyboard (story 2.8, open question 11 (a)): it loads <c>js/board-keys.js</c>, attaches it to the
/// Board for EVERY Role - a Viewer traverses too - and hands what the module reports to the Board: a key pressed on one of
/// the composite's stops, and where focus moved. AC1, AC3, AC4, AC6, AC7, AC11, AC15; UX-DR24, UX-DR27.
/// </summary>
/// <remarks>
/// <para>
/// <b>Separate from the drag</b> (<see cref="BoardDrag"/>), which is attached for movers only (UX-DR27). What a Viewer may not
/// do from the keyboard is decided by what Blazor rendered for the Role - the module prevents Space only on a mover's card -
/// and again here by the Board, which never picks a Task up for a Role that cannot move one.
/// </para>
/// <para>
/// <b>Attaching runs one at a time</b>, as the drag's does (story 2.7's lesson): the Board asks after every render, and renders
/// overlap. A module that cannot be loaded leaves the Board without the arrow grammar - Tab, Enter and the Move control still
/// work (UX-DR21) - and is not retried on every render.
/// </para>
/// </remarks>
public sealed class BoardKeys : IAsyncDisposable
{
    private const string ModulePath = "./js/board-keys.js";

    private readonly IJSRuntime _js;
    private readonly IBoardKeysHost _host;
    private DotNetObjectReference<BoardKeys>? _self;
    private IJSObjectReference? _module;
    private IJSObjectReference? _attached;
    private string? _attachedTo;
    private bool _disposed;
    private Task _queue = Task.CompletedTask;

    /// <summary>
    /// Creates the keyboard for one Board.
    /// </summary>
    /// <param name="js">Blazor's interop.</param>
    /// <param name="host">The Board the keyboard reports to.</param>
    public BoardKeys(IJSRuntime js, IBoardKeysHost host)
    {
        _js = js;
        _host = host;
    }

    /// <summary>
    /// Attaches the module to the Board, or re-attaches it when the Board has been drawn afresh. Does nothing when already
    /// attached to it.
    /// </summary>
    /// <param name="board">The Board's element.</param>
    /// <returns>A task.</returns>
    public Task AttachAsync(ElementReference board)
    {
        if (_disposed || (_queue.IsCompleted && string.Equals(_attachedTo, board.Id, StringComparison.Ordinal)))
        {
            return Task.CompletedTask;
        }

        return EnqueueAsync(() => AttachNowAsync(board));
    }

    /// <summary>
    /// Lets go of the module's listeners: the Board is not drawn.
    /// </summary>
    /// <returns>A task.</returns>
    public Task DetachAsync() =>
        _queue.IsCompleted && _attached is null && _attachedTo is null ? Task.CompletedTask : EnqueueAsync(DetachNowAsync);

    /// <summary>
    /// Brings a carry's drop zone into view (AC5): <c>scrollIntoView</c>, nearest, without moving focus.
    /// </summary>
    /// <returns>A task.</returns>
    public async Task RevealAsync()
    {
        if (_attached is not { } attached)
        {
            return;
        }

        try
        {
            await attached.InvokeVoidAsync("reveal");
        }
        catch (Exception exception) when (exception is JSException or ObjectDisposedException or InvalidOperationException)
        {
            // The module has gone with the page: there is nothing to bring into view.
        }
    }

    /// <summary>
    /// The module: a key pressed on a stop.
    /// </summary>
    /// <param name="key">The DOM <c>KeyboardEvent.key</c>.</param>
    /// <param name="rightToLeft">Whether the Board's computed direction is right to left.</param>
    /// <param name="taskId">The card's Task, or <c>null</c>.</param>
    /// <param name="statusId">The column's Status, or <c>null</c>.</param>
    /// <returns>A task.</returns>
    [JSInvokable("Key")]
    public Task KeyAsync(string key, bool rightToLeft, string? taskId, string? statusId) =>
        _host.KeyAsync(BoardTraversal.KeyOf(key, rightToLeft), IdOf(taskId), IdOf(statusId));

    /// <summary>
    /// The module: focus moved to a stop, or left the Board.
    /// </summary>
    /// <param name="taskId">The card's Task, or <c>null</c>.</param>
    /// <param name="statusId">The column's Status, or <c>null</c>.</param>
    [JSInvokable]
    public void Focused(string? taskId, string? statusId) => _host.Focused(IdOf(taskId), IdOf(statusId));

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _disposed = true;

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

    private static Guid? IdOf(string? text) => Guid.TryParse(text, out var id) ? id : null;

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

    private async Task AttachNowAsync(ElementReference board)
    {
        if (_disposed || string.Equals(_attachedTo, board.Id, StringComparison.Ordinal))
        {
            return;
        }

        await DetachNowAsync();

        try
        {
            _module ??= await _js.InvokeAsync<IJSObjectReference>("import", ModulePath);
            _self ??= DotNetObjectReference.Create(this);

            var attached = await _module.InvokeAsync<IJSObjectReference>("attach", board, _self);

            if (_disposed)
            {
                await DisposeQuietlyAsync(attached, dispose: true);
                return;
            }

            _attached = attached;
        }
        catch (Exception exception) when (exception is JSException or ObjectDisposedException or InvalidOperationException or OperationCanceledException)
        {
            // A fault here would otherwise poison the queue for every later attach, detach and disposal.
            // The module could not be loaded or attached: this Board has no arrow grammar, and Tab, Enter and the Move
            // control stay the way through it (UX-DR21). The Board is still recorded, so that every render does not try again.
        }

        _attachedTo = board.Id;
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
}
