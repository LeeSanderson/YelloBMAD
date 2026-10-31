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
    /// <para>
    /// Assumes the story's recommended wiring (Task 2): <c>activeSpace.RegisterPurge(spaceNarration.Forget)</c> in
    /// <c>Program</c>. A method group compiles to <c>ldftn</c>, whose operand is the method reference this reads.
    /// Plant: remove the registration - this goes red. <c>ActiveSpaceStateTests.A_pending_narration_is_forgotten_on_a_Space_change_and_kept_within_one_Space</c>
    /// stays green under that plant, because it wires its own registration; it goes red only if <c>Forget</c> itself
    /// stops forgetting.
    /// </para>
    /// <para>
    /// The IL reads which methods <c>Program</c> calls, not which purge <c>Forget</c> is handed to, so the source is also
    /// read (line comments stripped) for the registration on the Space seam itself: a
    /// <c>sessionState.RegisterPurge(spaceNarration.Forget)</c> would satisfy the IL and fail here (story 3.2 code review).
    /// </para>
    /// </remarks>
    [Fact]
    [Trait("Requirement", "UX-DR31")]
    [Trait("Requirement", "UX-DR38")]
    public void The_composition_root_registers_the_pending_narration_with_the_Space_purge()
    {
        // Anchor: the scan sees Program's existing Space wiring.
        Assert.Contains("Program", CallersOf(typeof(ActiveSpaceState).FullName!, nameof(ActiveSpaceState.Navigating)));

        Assert.Contains("Program", CallersOf(typeof(ActiveSpaceState).FullName!, nameof(ActiveSpaceState.RegisterPurge)));
        Assert.Contains("Program", CallersOf(typeof(SpaceNarration).FullName!, nameof(SpaceNarration.Forget)));

        var program = LineComment.Replace(File.ReadAllText(Path.Combine(ClientRoot.FullName, "Program.cs")), string.Empty);

        Assert.Matches(NarrationOnTheSpacePurge, program);
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
    [Fact]
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

        // Razor and C# line comments stripped, so a remark naming the set satisfies nothing (story 3.2 code review).
        var home = LineComment.Replace(
            RazorComment.Replace(File.ReadAllText(Path.Combine(ClientRoot.FullName, "Pages", "Home.razor")), string.Empty),
            string.Empty);

        Assert.Contains("Directory.RefusedInThisLoad(", home, StringComparison.Ordinal);
        Assert.DoesNotMatch(FirstListedSpace, home);
    }

    /// <summary>
    /// The AC12 scan for an unconditional first listed Space sees the shapes it would take, and not the route back's own
    /// filtered choice. A standing check on the scan.
    /// </summary>
    [Fact]
    public void The_first_listed_Space_scan_detects_each_shape()
    {
        Assert.Matches(FirstListedSpace, "ClientRoutes.SpaceHome(spaces[0].Id)");
        Assert.Matches(FirstListedSpace, "ClientRoutes.SpaceHome(spaces[ 0 ].Id)");
        Assert.Matches(FirstListedSpace, "var first = spaces.First();");
        Assert.Matches(FirstListedSpace, "var first = spaces.ElementAt(0);");
        Assert.DoesNotMatch(FirstListedSpace, "spaces.FirstOrDefault(space => !Directory.RefusedInThisLoad(space.Id))");
    }

    // A C# line comment, to the end of the line.
    [GeneratedRegex(@"//[^\r\n]*", RegexOptions.None, 1000)]
    private static partial Regex LineComment { get; }

    // The Space seam's registration of the pending narration (AC11).
    [GeneratedRegex(@"activeSpace\s*\.\s*RegisterPurge\s*\(\s*spaceNarration\s*\.\s*Forget\s*\)", RegexOptions.None, 1000)]
    private static partial Regex NarrationOnTheSpacePurge { get; }

    // The first listed Space taken unconditionally: spaces[0], spaces.First() or spaces.ElementAt(0) (AC12).
    [GeneratedRegex(@"\bspaces\s*(?:\[\s*0\s*\]|\.\s*First\s*\(\s*\)|\.\s*ElementAt\s*\(\s*0\s*\))", RegexOptions.None, 1000)]
    private static partial Regex FirstListedSpace { get; }

    // A Razor loop whose source is the switcher's answer: the directory's Current, or a variable or parameter named for
    // the Spaces (Spaces, _spaces, spaces).
    [GeneratedRegex(
        @"@foreach\s*\(\s*var\s+\w+\s+in\s+(?:[^)]*\b_?spaces\b[^)]*|[^)]*Directory\s*\.\s*Current[^)]*)\)",
        RegexOptions.IgnoreCase,
        1000)]
    private static partial Regex SpaceLoop { get; }
}
