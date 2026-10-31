using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;
using Yello.Client.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 3.2's static gates: the switcher is the only list of the Account's Spaces (AC1), the pending narration is
/// registered with the Space purge (AC11), and the route back's exclusion set is held by the directory and is never a
/// remembered last Space (AC12, UX-DR32). Written by the story 3.2 ATDD run, 2026-10-30.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read from source and IL, never from a comment.</b> Razor comments are stripped before any scan, and the
/// registration is read from <c>Program</c>'s compiled IL (<see cref="CallersOf"/>), so a remark naming
/// <c>Forget</c> satisfies nothing.
/// </para>
/// <para>
/// <b>Two exceptions to "the only list", each named in the gate's message.</b> Account settings' Token form lists the
/// Account's Spaces in a <c>select</c> to bind a new Token to one (story 1.8, AC1 of that story) - it existed before
/// 3.2 and is a choice of binding, not a way to reach a Space. And the AD-24 amendment (<c>ARCHITECTURE-SPINE.md:225</c>)
/// already permits story 5.4's delete-Account refusal to name the Spaces the Account owns; 5.4 adds its file here
/// when it lands. Any third list is a defect.
/// </para>
/// </remarks>
public sealed partial class SpaceSwitcherGateTests
{
    /// <summary>
    /// The Razor files allowed to render the Account's Spaces as a list, relative to <c>Yello.Client</c>.
    /// </summary>
    private static readonly string[] SpaceListRenderers =
    [
        "Components/SpaceSwitcher.razor",
        "Pages/AccountSettings.razor",
    ];

    /// <summary>
    /// AC1 (FR-9, UX-DR9) - the switcher remains the only list of the Account's Spaces anywhere in the product: no Razor
    /// file but the switcher (and the two named exceptions) loops over the switcher's answer.
    /// </summary>
    /// <remarks>
    /// <b>Green today, a standing gate.</b> <c>Home.razor</c> reads the list to choose a destination and renders none
    /// of it; a <c>@foreach</c> over <c>Directory.Current</c> planted there turns this red
    /// (<see cref="The_Space_list_scan_detects_a_planted_list"/> proves the scan sees that shape).
    /// </remarks>
    [Fact]
    [Trait("Requirement", "UX-DR9")]
    public void Only_the_switcher_renders_the_Accounts_Spaces_as_a_list()
    {
        var renderers = ClientSources()
            .Where(file => file.Extension == ".razor")
            .Where(file => SpaceLoop.IsMatch(RazorComment.Replace(File.ReadAllText(file.FullName), string.Empty)))
            .Select(file => Path.GetRelativePath(ClientRoot.FullName, file.FullName).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Anchor: the scan finds the switcher itself, so an empty answer is not a pass over nothing.
        Assert.Contains("Components/SpaceSwitcher.razor", renderers);

        Assert.True(
            renderers.SequenceEqual(SpaceListRenderers, StringComparer.Ordinal),
            "Only the Space switcher lists the Account's Spaces (story 3.2 AC1). Allowed besides it: Account settings' "
            + "Token-binding select (story 1.8) and, when it lands, story 5.4's delete-Account refusal naming owned Spaces "
            + $"(the AD-24 amendment). Found: [{string.Join(", ", renderers)}].");
    }

    /// <summary>
    /// The AC1 scan detects the shapes a second list would take, and not the loops the client already has over other
    /// things. Green today, a standing check on the scan.
    /// </summary>
    [Fact]
    public void The_Space_list_scan_detects_a_planted_list()
    {
        Assert.Matches(SpaceLoop, "@foreach (var space in Directory.Current ?? [])");
        Assert.Matches(SpaceLoop, "@foreach (var space in Spaces)");
        Assert.Matches(SpaceLoop, "@foreach (var space in _spaces)");
        Assert.Matches(SpaceLoop, "@foreach(var s in Directory . Current)");
        Assert.DoesNotMatch(SpaceLoop, "@foreach (var project in _projects)");
        Assert.DoesNotMatch(SpaceLoop, "@foreach (var label in SpaceLabels)");
        Assert.DoesNotMatch(SpaceLoop, "var spaces = await Directory.RefreshAsync(CancellationToken.None);");
    }

    /// <summary>
    /// AC11 (UX-DR31, UX-DR38) - the composition root registers <c>SpaceNarration</c> with the Space purge, so a
    /// sentence held for Space A's home ("narrated, then routed", UX-DR29) is gone before Space B renders.
    /// </summary>
    /// <remarks>
    /// Assumes the story's recommended wiring (Task 2): <c>activeSpace.RegisterPurge(spaceNarration.Forget)</c> in
    /// <c>Program</c>. A method group compiles to <c>ldftn</c>, whose operand is the method reference this reads.
    /// Plant: remove the registration - this goes red, and so does
    /// <c>ActiveSpaceStateTests.A_pending_narration_is_forgotten_on_a_Space_change_and_kept_within_one_Space</c> only if
    /// the plant removes <c>Forget</c> itself.
    /// </remarks>
    [Fact(Skip = "red: AC11 - SpaceNarration has no Forget() and Program registers nothing of it with the Space purge (story 3.2 Task 2)")]
    [Trait("Requirement", "UX-DR31")]
    [Trait("Requirement", "UX-DR38")]
    public void The_composition_root_registers_the_pending_narration_with_the_Space_purge()
    {
        // Anchor: the scan sees Program's existing Space wiring.
        Assert.Contains("Program", CallersOf(typeof(ActiveSpaceState).FullName!, nameof(ActiveSpaceState.Navigating)));

        Assert.Contains("Program", CallersOf(typeof(ActiveSpaceState).FullName!, nameof(ActiveSpaceState.RegisterPurge)));
        Assert.Contains("Program", CallersOf(typeof(SpaceNarration).FullName!, "Forget"));
    }

    /// <summary>
    /// AC12 (UX-DR32, open question 1 (a)) - the route back's per-load set of Spaces that refused this tab is held by
    /// <see cref="SpaceDirectory"/>, its name is not one the last-Space scan would flag, and the entry point no longer
    /// lands on <c>spaces[0]</c> unconditionally.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Assumes Q1 (a): a member whose name contains "Refused" (the story suggests <c>RefusedInThisLoad</c>). If the dev
    /// phase names it otherwise, correct the filter here and record it in the Debug Log.
    /// </para>
    /// <para>
    /// <b>The regex is not widened.</b> <c>RemembersASpace</c> is used as it stands; a member it matches fails here,
    /// which is the point - the set excludes and never chooses. Plant: "go to the last refused Space" in
    /// <c>Home.razor</c> - the browser case <c>SpaceSwitchCarryOverTests.The_route_back_skips_a_listed_Space_that_refused_with_409</c>
    /// goes red.
    /// </para>
    /// </remarks>
    [Fact(Skip = "red: AC12 - SpaceDirectory holds no per-load set of refused Spaces and Home still lands on spaces[0] (story 3.2 Task 3, Q1 (a))")]
    [Trait("Requirement", "UX-DR32")]
    public void The_route_back_exclusion_lives_in_the_directory_and_is_not_a_remembered_Space()
    {
        var refusedMembers = typeof(SpaceDirectory)
            .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(member => member.Name.Contains("Refused", StringComparison.OrdinalIgnoreCase))
            .Select(member => member.Name)
            .ToList();

        Assert.True(refusedMembers.Count > 0, "SpaceDirectory holds no set of the Spaces that refused this tab (Q1 (a)).");
        Assert.All(refusedMembers, name => Assert.DoesNotMatch(RemembersASpace, name));

        var home = RazorComment.Replace(File.ReadAllText(Path.Combine(ClientRoot.FullName, "Pages", "Home.razor")), string.Empty);

        Assert.Contains("Refused", home, StringComparison.Ordinal);
        Assert.DoesNotContain("spaces[0]", home, StringComparison.Ordinal);
    }

    // A Razor loop whose source is the switcher's answer: the directory's Current, or a variable or parameter named for
    // the Spaces (Spaces, _spaces, spaces).
    [GeneratedRegex(
        @"@foreach\s*\(\s*var\s+\w+\s+in\s+(?:[^)]*\b_?spaces\b[^)]*|[^)]*Directory\s*\.\s*Current[^)]*)\)",
        RegexOptions.IgnoreCase,
        1000)]
    private static partial Regex SpaceLoop { get; }
}
