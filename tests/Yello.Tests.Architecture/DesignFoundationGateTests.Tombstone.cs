using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// The tombstone avatar in the sheets: a deleted Account's attribution keeps the avatar's shape, muted - the default 3px
/// radius, the column surface and the muted ink - and is never hidden.
/// </summary>
/// <remarks>
/// <para>
/// Anchored on a rule that selects <c>.avatar-tombstone</c>, the class this gate expects the tombstone state to carry.
/// </para>
/// <para>
/// <b>No contrast pair is asserted</b>: the tombstone carries no text, so it has no text-contrast obligation. Its
/// accessible name comes from the markup, not the sheet.
/// </para>
/// </remarks>
public sealed partial class DesignFoundationGateTests
{
    private const string TombstoneAvatarClass = ".avatar-tombstone";

    /// <summary>
    /// The tombstone takes the avatar's 3px default radius, as the live avatar does.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR10")]
    public void The_tombstone_avatar_takes_the_3px_radius() => AssertRadius(TombstoneAvatarClass, 3, "var(--radius-default)");

    /// <summary>
    /// The tombstone is muted by tokens only: its background is <c>var(--surface-column)</c> and its colour
    /// <c>var(--text-muted)</c>, and no rule that selects it sets any other background or colour.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR10")]
    public void The_tombstone_avatar_is_muted_by_the_column_surface_and_the_muted_ink()
    {
        var declarations = CssCorpus.AllDeclarations()
            .Where(d => SelectsComponent(d.Rule.Selector, TombstoneAvatarClass))
            .ToList();

        Assert.True(declarations.Count > 0, $"No rule selects {TombstoneAvatarClass}.");

        var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["background"] = "var(--surface-column)",
            ["background-color"] = "var(--surface-column)",
            ["color"] = "var(--text-muted)",
        };

        Assert.Contains(declarations, d => IsDeclared(d.Declaration, "color"));
        Assert.Contains(declarations, d => IsDeclared(d.Declaration, "background") || IsDeclared(d.Declaration, "background-color"));

        var problems = declarations
            .Where(d => expected.ContainsKey(d.Declaration.Property.Trim()))
            .Where(d => !string.Equals(d.Declaration.Value.Trim(), expected[d.Declaration.Property.Trim()], StringComparison.Ordinal))
            .Select(d => $"{Where(d.Sheet, d.Declaration)} gives '{d.Rule.Selector}' '{d.Declaration.Property}: {d.Declaration.Value}'.")
            .ToList();

        AssertNoProblems(problems, "The tombstone avatar is not muted by the column surface and the muted ink.", "Use var(--surface-column) and var(--text-muted) on .avatar-tombstone.");
    }

    /// <summary>
    /// The tombstone is never removed from view: no rule that selects it hides it, at any breakpoint or none.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR10")]
    public void The_tombstone_avatar_is_never_hidden()
    {
        var declarations = CssCorpus.AllDeclarations()
            .Where(d => SelectsComponent(d.Rule.Selector, TombstoneAvatarClass))
            .ToList();

        Assert.True(declarations.Count > 0, $"No rule selects {TombstoneAvatarClass}.");

        var problems = declarations
            .Where(d => Hides(d.Declaration))
            .Select(d => $"{Where(d.Sheet, d.Declaration)} sets '{d.Declaration.Property}: {d.Declaration.Value}' on '{d.Rule.Selector}'.")
            .ToList();

        AssertNoProblems(problems, "A rule hides the tombstone avatar.", "A deleted Account's attribution is never blank and never removed (UX-DR10).");
    }

    private static bool IsDeclared(CssCorpus.Declaration declaration, string property) =>
        string.Equals(declaration.Property.Trim(), property, StringComparison.OrdinalIgnoreCase);
}
