namespace Yello.Client.Spaces;

/// <summary>
/// What the Role narration element under the context bar says: a Role drop in the active Space, or why a surface the
/// Role cannot see was left. One element for every surface, so a drop is narrated the same way wherever it lands.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bound to the Space it was said in.</b> Registered with the Space purge in <c>Program</c>, so a switch drops it and
/// another Space never hears about this one's Role. The sentence names a Role and a surface, never a Space.
/// </para>
/// <para>
/// <b>Kept until the next navigation</b>, except across the one a surface makes on its way out after saying it, so the
/// page it routes to still shows it.
/// </para>
/// </remarks>
public sealed class RoleNarrator
{
    private bool _survivesNavigation;

    /// <summary>
    /// Raised whenever the sentence changes.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// The sentence shown, or <c>null</c>.
    /// </summary>
    public string? Sentence { get; private set; }

    /// <summary>
    /// Whether the sentence is said politely - nothing changed under the person - rather than assertively.
    /// </summary>
    public bool Polite { get; private set; }

    /// <summary>
    /// Whether the narration holds focus, so the page it lands on leaves focus there rather than on its heading.
    /// </summary>
    public bool HoldsFocus { get; private set; }

    /// <summary>
    /// Shows a sentence.
    /// </summary>
    /// <param name="sentence">The sentence, in the client's own words.</param>
    /// <param name="polite">Whether it is said politely.</param>
    /// <param name="survivesNextNavigation">Whether the navigation the caller makes next keeps it.</param>
    public void Say(string sentence, bool polite, bool survivesNextNavigation)
    {
        Sentence = sentence;
        Polite = polite;
        HoldsFocus = false;
        _survivesNavigation = survivesNextNavigation;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Records that the narration element has taken focus.
    /// </summary>
    public void Focused() => HoldsFocus = Sentence is not null;

    /// <summary>
    /// The tab is navigating: the sentence goes, unless it was said to survive this navigation.
    /// </summary>
    public void Navigating()
    {
        if (_survivesNavigation)
        {
            _survivesNavigation = false;
            return;
        }

        Forget();
    }

    /// <summary>
    /// Drops the sentence, unsaid if it was still pending - on a Space purge and on a rise.
    /// </summary>
    public void Forget()
    {
        if (Sentence is null)
        {
            return;
        }

        Sentence = null;
        HoldsFocus = false;
        _survivesNavigation = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
