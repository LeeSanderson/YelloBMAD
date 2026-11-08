namespace Yello.Client.Spaces;

/// <summary>
/// A Role in the active Space dropping, from <see cref="From"/> to <see cref="To"/> (stable codes), as
/// <see cref="ActiveSpaceState.RoleDropping"/> raises it before the narration renders.
/// </summary>
/// <param name="from">The Role held until now.</param>
/// <param name="to">The lower Role.</param>
public sealed class RoleDropEventArgs(string from, string to) : EventArgs
{
    private readonly TaskCompletionSource _narrated = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private readonly TaskCompletionSource _applied = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// The Role held until now.
    /// </summary>
    public string From => from;

    /// <summary>
    /// The lower Role.
    /// </summary>
    public string To => to;

    /// <summary>
    /// The sentence to narrate instead of the drop's own, from a surface the lower Role cannot see.
    /// </summary>
    public string? Sentence { get; private set; }

    /// <summary>
    /// Whether the surface showing is being left, so focus goes to the narration whatever holds it.
    /// </summary>
    public bool LeavesSurface { get; private set; }

    /// <summary>
    /// Completes once the narration has rendered, before the lower Role is applied.
    /// </summary>
    public Task Narrated => _narrated.Task;

    /// <summary>
    /// Completes once the lower Role has been applied, or the drop abandoned.
    /// </summary>
    public Task Applied => _applied.Task;

    /// <summary>
    /// The surface showing is one the lower Role cannot see: it is narrated with <paramref name="sentence"/>, focus moves
    /// to the narration, and the surface routes away once <see cref="Narrated"/> completes.
    /// </summary>
    /// <param name="sentence">The surface's own sentence.</param>
    public void LeaveSurface(string sentence)
    {
        Sentence = sentence;
        LeavesSurface = true;
    }

    internal void MarkNarrated() => _narrated.TrySetResult();

    internal void MarkApplied()
    {
        _narrated.TrySetResult();
        _applied.TrySetResult();
    }
}
