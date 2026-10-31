namespace Yello.Client.Spaces;

/// <summary>
/// One sentence about a Space the tab has left for good, for the page it lands on to say - so "Space deleted." survives
/// the purge of the Space it describes. Story 3.3, AC12, open question 6 (a); UX-DR38.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not <see cref="SpaceNarration"/>.</b> That one is bound to the Space it was said in and registered with the
/// Space purge (story 3.2, AC11), so a sentence held for one Space's home is never said in another's. A deletion leaves
/// its Space through the entry point and lands in another Space, or in none: the purge on leaving would drop the
/// sentence. This holder is not on the Space purge; the entry point takes it, and hands it to the destination's
/// <see cref="SpaceNarration"/> once the tab is already in that Space, or says it itself when the Account belongs to none.
/// </para>
/// <para>
/// <b>Never Space content, and never stored.</b> The sentence is the client's own copy and names no Space (UX-DR38); it
/// lives in this tab's memory until taken, is dropped on sign-out and when the Session ends (<c>Program</c>), and nothing
/// reaches browser storage (<c>ClientStorageBanTests</c>).
/// </para>
/// </remarks>
public sealed class DepartureNarration
{
    private string? _pending;

    /// <summary>
    /// Holds a sentence for the page the tab lands on to say.
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
    /// Drops any held sentence, unsaid - on sign-out and when the Session ends.
    /// </summary>
    public void Forget() => _pending = null;
}
