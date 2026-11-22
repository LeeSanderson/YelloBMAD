using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Yello.Client.Components;

/// <summary>
/// A Task's description in the detail panel: a textarea over the panel's <see cref="DescriptionSession"/> for a Role that
/// may edit it, or the text for one that may not.
/// </summary>
/// <remarks>
/// <para>
/// <b>Batched, never one request per keystroke.</b> Pending changes go when typing pauses for <see cref="IdlePause"/>, or
/// once the oldest has waited <see cref="OldestWait"/>, and on blur, Esc and close; a batch typed while one is out goes once
/// it is answered. Local edits render at once and never wait for the network.
/// </para>
/// <para>
/// <b>The caret keeps its place.</b> When an answer changes the text under the person, the caret is read before and mapped
/// through the change, or it would jump to the end.
/// </para>
/// </remarks>
public sealed partial class DescriptionEditor : IAsyncDisposable
{
    private static readonly TimeSpan IdlePause = TimeSpan.FromMilliseconds(250);

    private static readonly TimeSpan OldestWait = TimeSpan.FromSeconds(1);

    private readonly string _id = Guid.NewGuid().ToString("N");

    // A field name no browser's autofill would match to a saved value: random per panel.
    private readonly string _fieldName = Guid.NewGuid().ToString("N");

    private ElementReference _heading;
    private ElementReference _editor;
    private DescriptionSession? _session;
    private string _text = string.Empty;
    private bool _loaded;
    private bool _disposed;
    private CancellationTokenSource? _idle;
    private CancellationTokenSource? _oldest;
    private bool _oldestWaiting;
    private (int Start, int End)? _caret;
    private IJSObjectReference? _module;

    /// <summary>
    /// Gets or sets the Space the Task is in.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public Guid SpaceId { get; set; }

    /// <summary>
    /// Gets or sets the Task whose description this is.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public Guid TaskId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Role held may edit the description; otherwise it renders as text.
    /// </summary>
    [Parameter]
    public bool CanEdit { get; set; }

    /// <summary>
    /// Gets or sets what is raised when an Esc in the editor has been taken to leave it, so the panel does not also close.
    /// </summary>
    [Parameter]
    public EventCallback OnEscape { get; set; }

    private string HeadingId => "task-detail-description-" + _id;

    private string Shown => _session?.Replica?.Text ?? _session?.ReadText ?? string.Empty;

    private bool Editing => CanEdit && _session?.Replica is not null;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopWaitingAsync();

        // Closing the panel sends what is pending, as blur and Esc do.
        if (CanEdit && _session is not null)
        {
            await _session.SendAsync(ActiveSpace.Token);
        }

        _idle?.Dispose();
        _oldest?.Dispose();

        if (_module is not null)
        {
            await CallAsync(() => _module.DisposeAsync().AsTask());
        }
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        _session = new DescriptionSession(Http, Merge, SpaceId, TaskId);

        await _session.LoadAsync(ActiveSpace.Token);

        _text = Shown;
        _loaded = true;
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_caret is not { } caret || !Editing)
        {
            return;
        }

        _caret = null;

        if (await ModuleAsync() is { } module)
        {
            await CallAsync(() => module.InvokeVoidAsync("select", _editor, caret.Start, caret.End).AsTask());
        }
    }

    private void Input(ChangeEventArgs args)
    {
        if (_session?.Replica is not { } replica)
        {
            return;
        }

        try
        {
            _session.Record(_text, args.Value?.ToString() ?? string.Empty);
        }
        catch (ArgumentException)
        {
            // An input the replica cannot take (an unpaired surrogate mid-composition) is dropped; the field follows the
            // replica again on this render.
        }

        _text = replica.Text;
        Schedule();
    }

    private async Task KeyDownAsync(KeyboardEventArgs args)
    {
        if (args.Key != "Escape")
        {
            return;
        }

        // First, before anything is awaited: the panel's own Esc handler runs next and must see this one was taken.
        await OnEscape.InvokeAsync();
        await _heading.FocusAsync();
        await FlushAsync();
    }

    /// <summary>
    /// Restarts the pause timer, and starts the oldest-change timer if none is running.
    /// </summary>
    private void Schedule()
    {
        if (_disposed || _session is not { HasPending: true })
        {
            return;
        }

        _idle?.Dispose();
        _idle = new CancellationTokenSource();
        _ = FlushAfterAsync(IdlePause, _idle.Token);

        if (!_oldestWaiting)
        {
            _oldestWaiting = true;
            _oldest?.Dispose();
            _oldest = new CancellationTokenSource();
            _ = FlushAfterAsync(OldestWait, _oldest.Token);
        }
    }

    private async Task FlushAfterAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await InvokeAsync(FlushAsync);
    }

    private async Task StopWaitingAsync()
    {
        if (_idle is not null)
        {
            await _idle.CancelAsync();
        }

        if (_oldest is not null)
        {
            await _oldest.CancelAsync();
        }

        _oldestWaiting = false;
    }

    /// <summary>
    /// Sends what is pending, one batch at a time. A batch that fails stays pending and goes with the next flush; nothing
    /// new is said here.
    /// </summary>
    private async Task FlushAsync()
    {
        if (!CanEdit || _disposed || _session is not { HasPending: true, InFlight: false })
        {
            return;
        }

        await StopWaitingAsync();

        var before = _text;
        var selection = await SelectionAsync();

        if (await _session.SendAsync(ActiveSpace.Token) != DescriptionSession.Sent.Settled || _disposed)
        {
            return;
        }

        Follow(before, selection);
        Schedule();
    }

    /// <summary>
    /// The field follows the replica after an answer changed it, with the caret mapped through the change.
    /// </summary>
    private void Follow(string before, (int Start, int End)? selection)
    {
        // Typed while the batch was out: the replica already holds it, so only an answer's change is mapped.
        var current = _session?.Replica?.Text ?? before;

        if (string.Equals(current, _text, StringComparison.Ordinal))
        {
            return;
        }

        var shown = _text;

        _text = current;
        _caret = selection is { } caret && string.Equals(shown, before, StringComparison.Ordinal)
            ? (TextDiff.Map(before, current, caret.Start), TextDiff.Map(before, current, caret.End))
            : null;
        StateHasChanged();
    }

    private async Task<(int Start, int End)?> SelectionAsync()
    {
        if (!Editing || await ModuleAsync() is not { } module)
        {
            return null;
        }

        int[] selection = [];

        await CallAsync(async () => selection = await module.InvokeAsync<int[]>("selectionOf", _editor));

        return selection is [var start, var end] ? (start, end) : null;
    }

    private async Task<IJSObjectReference?> ModuleAsync()
    {
        if (_module is null)
        {
            await CallAsync(async () => _module = await JS.InvokeAsync<IJSObjectReference>("import", "./js/description-editor.js"));
        }

        return _module;
    }

    /// <summary>
    /// A call into the caret module. A module that cannot load, or a page that is going, leaves the caret where the browser
    /// puts it.
    /// </summary>
    private static async Task CallAsync(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException or InvalidOperationException or OperationCanceledException)
        {
            // See above.
        }
    }
}
