namespace Yello.Contracts.Labels;

/// <summary>
/// The colour keys a Label definition may carry, as the wire states them: the client offers exactly these, and the
/// Host refuses anything else with <c>invalid-label-colour</c>. Story 2.4, open question 3.
/// </summary>
/// <remarks>
/// <b>The Domain's <c>LabelPalette</c> is the rule; this is its statement on the wire</b>, for a client that cannot
/// reference the Domain. The Architecture suite holds the two sets, and the token layer's <c>--label-{key}</c>
/// family, equal - so a key added in one place and not the others fails the build.
/// </remarks>
public static class LabelColours
{
    /// <summary>
    /// The palette's keys, in the order the define form offers them.
    /// </summary>
    public static IReadOnlyList<string> Keys { get; } = ["rust", "ochre", "olive", "grass", "jade", "teal", "blue", "orchid"];
}
