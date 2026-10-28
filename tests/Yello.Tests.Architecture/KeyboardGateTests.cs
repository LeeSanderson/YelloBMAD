using System.Text.RegularExpressions;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.8's static gates on the Board's keyboard: nothing in the client forces application mode
/// (<c>role="application"</c>), no tab stop is ordered by a positive <c>tabindex</c>, the keyboard module is attached for every
/// Role while the drag stays movers-only, and Space becomes a pick-up only for a mover - in the module and in the Board. AC4,
/// AC7, AC11; UX-DR21, UX-DR24, UX-DR27.
/// </summary>
/// <remarks>
/// <b>Every detector is proved against a plant</b> (<c>TESTING-CONVENTIONS.md</c>, "An absence assertion must be validated
/// against a planted signal"): each gate's scan is a function of the text it reads, and a sibling case fires it on planted
/// text and on the clean form, so the gate cannot pass by reading nothing. The browser cases (<c>KeyboardTests</c>) press the
/// same keys on the rendered Board.
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P1")]
[Trait("Requirement", "UX-DR24")]
public sealed partial class KeyboardGateTests
{
    private static readonly string[] MarkupAndScript = [".razor", ".html", ".js"];

    private static string ClientPath(params string[] parts) => Path.Combine([RepositoryLayout.Root.FullName, "Yello.Client", .. parts]);

    /// <summary>
    /// AC7, UX-DR21 - no component, page or script in the client sets <c>role="application"</c>: the arrow grammar is an
    /// accelerator, and browse mode keeps the Board and the Move control (the conformance path).
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR21")]
    public void Nothing_in_the_client_forces_application_mode()
    {
        var files = ClientFiles().ToList();

        Assert.Contains(files, file => file.Name == "board-keys.js");

        var problems = files
            .Where(file => ApplicationRole.IsMatch(File.ReadAllText(file.FullName)))
            .Select(RepositoryLayout.RelativePath)
            .ToList();

        Assert.True(problems.Count == 0, $"role=\"application\" is set in: {string.Join(", ", problems)}.");
    }

    /// <summary>
    /// The detector behind <see cref="Nothing_in_the_client_forces_application_mode"/>, fired on planted text.
    /// </summary>
    [Fact]
    public void The_application_role_scan_detects_a_plant()
    {
        Assert.Matches(ApplicationRole, """<div class="board__columns" role="application">""");
        Assert.Matches(ApplicationRole, "root.setAttribute('role', 'application');");
        Assert.DoesNotMatch(ApplicationRole, """<ul role="listbox">""");
    }

    /// <summary>
    /// AC7, UX-DR40 - no tab stop is ordered by a positive <c>tabindex</c>: Tab follows reading order, and the composite's
    /// one stop is a roving <c>0</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR40")]
    public void No_tabindex_is_greater_than_zero()
    {
        var problems = ClientFiles()
            .Where(file => PositiveTabIndex.IsMatch(File.ReadAllText(file.FullName)))
            .Select(RepositoryLayout.RelativePath)
            .ToList();

        Assert.True(problems.Count == 0, $"A positive tabindex is set in: {string.Join(", ", problems)}.");
    }

    /// <summary>
    /// The detector behind <see cref="No_tabindex_is_greater_than_zero"/>, fired on planted text.
    /// </summary>
    [Fact]
    public void The_positive_tabindex_scan_detects_a_plant()
    {
        Assert.Matches(PositiveTabIndex, """<button tabindex="1">""");
        Assert.Matches(PositiveTabIndex, "element.tabIndex = 2;");
        Assert.DoesNotMatch(PositiveTabIndex, """<li tabindex="@(Active ? "0" : "-1")">""");
        Assert.DoesNotMatch(PositiveTabIndex, """<section tabindex="-1">""");
    }

    /// <summary>
    /// AC11, UX-DR27 - the keyboard is attached for every Role (a Viewer traverses), and the drag still for a Role that may
    /// move Tasks only: the keyboard's attach names no capability, the drag's names <c>_canManage</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    public void The_keyboard_is_attached_for_every_Role_and_the_drag_for_movers_only()
    {
        var keys = MethodBody(File.ReadAllText(ClientPath("Components", "BoardView.Keys.cs")), "AttachKeysAsync");
        var drag = MethodBody(File.ReadAllText(ClientPath("Components", "BoardView.Drag.cs")), "AttachDragAsync");

        Assert.True(AttachesForEveryRole(keys), "The keyboard's attach is gated on a capability: a Viewer would lose traversal.");
        Assert.False(AttachesForEveryRole(drag), "The drag's attach is not gated on _canManage: a Viewer would get a drag.");
    }

    /// <summary>
    /// The detector behind <see cref="The_keyboard_is_attached_for_every_Role_and_the_drag_for_movers_only"/>, fired on
    /// planted method bodies.
    /// </summary>
    [Fact]
    public void The_attach_scan_detects_a_capability_gate()
    {
        Assert.True(AttachesForEveryRole("{ await (_columns is not null ? keys.AttachAsync(_boardElement) : keys.DetachAsync()); }"));
        Assert.False(AttachesForEveryRole("{ await (_canManage && _columns is not null ? keys.AttachAsync(_boardElement) : keys.DetachAsync()); }"));
    }

    /// <summary>
    /// AC4, AC11, UX-DR27 - Space becomes a pick-up only for a mover: the module prevents it only on a mover's card
    /// (<c>.task-card--movable</c>, which Blazor renders for movers alone), and the Board picks up only while its Role may move
    /// Tasks.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR27")]
    public void Space_is_a_pick_up_only_for_a_mover()
    {
        var module = File.ReadAllText(ClientPath("wwwroot", "js", "board-keys.js"));
        var board = MethodBody(File.ReadAllText(ClientPath("Components", "BoardView.Keys.cs")), "CanPickUp");

        Assert.True(SpaceIsGuardedByTheMoversCard(module), "board-keys.js prevents Space on a card that is not a mover's.");
        Assert.Contains("_canManage", board, StringComparison.Ordinal);
    }

    /// <summary>
    /// The detector behind <see cref="Space_is_a_pick_up_only_for_a_mover"/>, fired on planted module text.
    /// </summary>
    [Fact]
    public void The_Space_scan_detects_an_unguarded_pick_up()
    {
        Assert.True(SpaceIsGuardedByTheMoversCard("if (key === ' ') {\n if (!event.shiftKey && stop.card && stop.card.matches(MOVABLE_CARD)) {\n event.preventDefault();"));
        Assert.False(SpaceIsGuardedByTheMoversCard("if (key === ' ') {\n if (!event.shiftKey && stop.card) {\n event.preventDefault();"));
        Assert.False(SpaceIsGuardedByTheMoversCard("if (key === ' ') {\n event.preventDefault();"));
    }

    private static IEnumerable<FileInfo> ClientFiles() =>
        MarkupAndScript
            .SelectMany(extension => RepositoryLayout.EnumerateSourceFiles("*" + extension))
            .Where(file => RepositoryLayout.RelativePath(file).StartsWith("Yello.Client/", StringComparison.Ordinal));

    private static bool AttachesForEveryRole(string body) => !body.Contains("_canManage", StringComparison.Ordinal);

    /// <summary>
    /// Whether the module's Space branch prevents the key only after checking the card is a mover's.
    /// </summary>
    private static bool SpaceIsGuardedByTheMoversCard(string module)
    {
        var branch = SpaceBranch.Match(module);

        if (!branch.Success)
        {
            return false;
        }

        var text = branch.Value;
        var guard = text.IndexOf("MOVABLE_CARD", StringComparison.Ordinal);
        var prevent = text.IndexOf("preventDefault", StringComparison.Ordinal);

        return guard >= 0 && prevent > guard;
    }

    /// <summary>
    /// The text of a method from its name to the end of its first balanced brace block (or its expression body's line).
    /// </summary>
    private static string MethodBody(string source, string name)
    {
        var start = source.IndexOf(" " + name + "(", StringComparison.Ordinal);

        Assert.True(start >= 0, $"No method named {name}.");

        var open = source.IndexOf('{', start);
        var arrow = source.IndexOf("=>", start, StringComparison.Ordinal);

        if (arrow >= 0 && (open < 0 || arrow < open))
        {
            var end = source.IndexOf(';', arrow);

            return source[start..(end < 0 ? source.Length : end)];
        }

        var depth = 0;

        for (var at = open; at < source.Length; at++)
        {
            depth += source[at] switch { '{' => 1, '}' => -1, _ => 0 };

            if (depth == 0)
            {
                return source[start..(at + 1)];
            }
        }

        return source[start..];
    }

    [GeneratedRegex("""role['"]?\s*[=,]\s*['"]application['"]""", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex ApplicationRole { get; }

    [GeneratedRegex("""tabindex\s*=\s*["']?\s*[1-9]""", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex PositiveTabIndex { get; }

    [GeneratedRegex("""key === ' '\)\s*\{[\s\S]*?preventDefault""", RegexOptions.None, 1000)]
    private static partial Regex SpaceBranch { get; }
}
