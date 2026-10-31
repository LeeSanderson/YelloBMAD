namespace Yello.Client.Spaces;

/// <summary>
/// One sentence a page says on its way out, for the page it routes to to say on arrival - so "narrated, then routed"
/// (UX-DR29) survives the navigation it describes. Story 2.4: Space settings, when a Role re-read lower no longer
/// permits it, narrates the change and routes to Space home, which says it.
/// </summary>
/// <remarks>
/// <b>Never Space content, and never stored.</b> The sentence names a Role and a surface, both the client's own copy;
/// it lives in this tab's memory until the destination takes it, and nothing reaches browser storage
/// (<c>ClientStorageBanTests</c>).
/// <para>
/// <b>Still bound to the Space it was said in</b> (story 3.2, AC11; UX-DR31, UX-DR38). A held sentence is a pending
/// announcement about that Space's Role, so it is registered with the Space purge in <c>Program</c>: a switch made
/// before the destination takes it drops it, and the next Space's home says nothing about the last one. A navigation
/// within one Space purges nothing, so "narrated, then routed" still works.
/// </para>
/// </remarks>
public sealed class SpaceNarration
{
    private string? _pending;

    /// <summary>
    /// Holds a sentence for the next page to say.
    /// </summary>
    /// <param name="sentence">The sentence, already in the client's own words.</param>
    public void Say(string sentence) => _pending = sentence;

    /// <summary>
    /// Takes the held sentence, once.
    /// </summary>
    /// <returns>The sentence, or <c>null</c> when none is held.</returns>
    public string? Take()
    {
        var sentence = _pending;
        _pending = null;

        return sentence;
    }

    /// <summary>
    /// Drops any held sentence, unsaid - registered with the Space purge (story 3.2, AC11), so a sentence held for one
    /// Space's home is never said in another's.
    /// </summary>
    public void Forget() => _pending = null;
}
