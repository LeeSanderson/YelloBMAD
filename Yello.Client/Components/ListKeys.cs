using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Yello.Client.Components;

/// <summary>
/// The List View's keyboard module (<c>js/list-keys.js</c>) as the list holds it: attached to the list's element once it is
/// drawn, re-attached when that element is drawn afresh, and let go of with the list. Story 2.10, Task 6.
/// </summary>
/// <remarks>
/// <b>Calls run one at a time</b>, in the order made, as <see cref="BoardKeys"/>' do: an attach, a focus and the disposal
/// never interleave, and a module that cannot load leaves the list without its arrow grammar - Tab and the "Show more
/// Tasks" control stay the way through it - rather than failing the page.
/// </remarks>
public sealed class ListKeys : IAsyncDisposable
{
    private const string ModulePath = "./js/list-keys.js";

    private readonly IJSRuntime _js;
    private readonly Func<string, Guid?, Task> _onKey;
    private readonly Action<Guid?> _onFocused;
    private DotNetObjectReference<ListKeys>? _self;
    private IJSObjectReference? _module;
    private IJSObjectReference? _attached;
    private string? _attachedTo;
    private bool _disposed;
    private Task _queue = Task.CompletedTask;

    /// <summary>
    /// Creates the module's holder.
    /// </summary>
    /// <param name="js">The JavaScript runtime.</param>
    /// <param name="onKey">Told a key pressed on a row's stop, with the row's Task.</param>
    /// <param name="onFocused">Told that focus moved into a row's stop.</param>
    public ListKeys(IJSRuntime js, Func<string, Guid?, Task> onKey, Action<Guid?> onFocused)
    {
        _js = js;
        _onKey = onKey;
        _onFocused = onFocused;
    }

    /// <summary>
    /// Attaches to the list's element, unless already attached to it.
    /// </summary>
    /// <param name="list">The element.</param>
    /// <returns>A task that completes once attached.</returns>
    public Task AttachAsync(ElementReference list)
    {
        if (_disposed || (_queue.IsCompleted && string.Equals(_attachedTo, list.Id, StringComparison.Ordinal)))
        {
            return Task.CompletedTask;
        }

        return EnqueueAsync(() => AttachNowAsync(list));
    }

    /// <summary>
    /// Moves focus to a row's stop by its Task's identity.
    /// </summary>
    /// <param name="taskId">The Task.</param>
    /// <returns>A task that completes once asked.</returns>
    public Task FocusAsync(Guid taskId) => EnqueueAsync(async () =>
    {
        if (_attached is not { } attached)
        {
            return;
        }

        try
        {
            await attached.InvokeVoidAsync("focus", taskId.ToString("D"));
        }
        catch (Exception exception) when (exception is JSException or ObjectDisposedException or InvalidOperationException)
        {
            // The module has gone with the page: there is nothing to focus.
        }
    });

    /// <summary>
    /// A key the module reported.
    /// </summary>
    /// <param name="key">The key, as the browser names it.</param>
    /// <param name="taskId">The row's Task.</param>
    /// <returns>A task that completes once handled.</returns>
    [JSInvokable("Key")]
    public Task KeyAsync(string key, string? taskId) => _onKey(key, IdOf(taskId));

    /// <summary>
    /// Focus moved into a row's stop.
    /// </summary>
    /// <param name="taskId">The row's Task.</param>
    [JSInvokable]
    public void Focused(string? taskId) => _onFocused(IdOf(taskId));

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

    private async Task AttachNowAsync(ElementReference list)
    {
        if (_disposed || string.Equals(_attachedTo, list.Id, StringComparison.Ordinal))
        {
            return;
        }

        await DetachNowAsync();

        try
        {
            _module ??= await _js.InvokeAsync<IJSObjectReference>("import", ModulePath);
            _self ??= DotNetObjectReference.Create(this);

            var attached = await _module.InvokeAsync<IJSObjectReference>("attach", list, _self);

            if (_disposed)
            {
                await DisposeQuietlyAsync(attached, dispose: true);
                return;
            }

            _attached = attached;
        }
        catch (Exception exception) when (exception is JSException or ObjectDisposedException or InvalidOperationException or OperationCanceledException)
        {
            // The module could not be loaded or attached: the list has no arrow grammar, and Tab and "Show more Tasks" stay
            // the way through it. The element is still recorded, so that every render does not try again.
        }

        _attachedTo = list.Id;
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
