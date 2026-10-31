using System.Text.RegularExpressions;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 3.4 AC13 (<c>deferred-work.md:688</c>; Q9 (a)) - every exit from a Space purges the departed Space, whoever
/// navigates. A programmatic <c>NavigateTo</c> was not seen to run Blazor's location-changing hook (3.3), so a
/// Space-leaving navigation goes through one helper that calls <c>ActiveSpace.Navigating(target)</c> first, and this gate
/// allows no other <c>NavigateTo</c> in the client except those named here with the reason they are safe. Written red by
/// the story 3.4 ATDD run; green since its dev phase.
/// </summary>
/// <remarks>
/// <para>
/// <b>Q9 (a), decided by the observation.</b> With <c>SpaceDeleteSection</c>'s explicit <c>Navigating(null)</c> removed,
/// <c>SpaceSwitchMemoryTests.Deleting_a_Space_purges_it_before_the_destination_renders</c> went red with and without
/// <c>replace: true</c> (a frame at <c>/</c> rendered the deleted Space's name), so the hook is not relied on and the
/// helper is <c>Yello.Client/Spaces/SpaceNavigation.cs</c>.
/// </para>
/// <para>
/// <b>Source text, not IL</b>, because the reason a call is safe is about its target, which only the source shows; and
/// the scan reads <c>.razor</c> files, which compile into generated methods with no stable name.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "UX-DR31")]
public sealed partial class SpaceNavigationGateTests
{
    /// <summary>
    /// The one file that may call <c>NavigateTo</c> for a navigation leaving the current Space (Q9 (a)).
    /// </summary>
    private const string HelperFile = "SpaceNavigation.cs";

    /// <summary>
    /// Call sites that do not leave a Space through this gate's door, each with its reason. Session end and sign-out
    /// purge through the Session purge (<c>Program.cs</c>), which runs before they navigate; Space settings returns to its
    /// own Space's home.
    /// </summary>
    private static readonly Dictionary<string, string> Allowed = new(StringComparer.Ordinal)
    {
        ["Program.cs"] = "Session end and sign-out: the Session purge empties every Space-scoped holder before the navigation",
        ["SpaceSettings.razor"] = "returns to the same Space's home; no Space is left",
        ["AccountSettings.razor"] = "leaves Account settings, which is in no Space, for sign-in when no Session is held",
        ["SignIn.razor"] = "leaves sign-in, which is in no Space, for the entry point once signed in",
    };

    /// <summary>
    /// AC13 - every <c>NavigateTo</c> in <c>Yello.Client</c> is in the helper or in an allowed file. Activated by story
    /// 3.4 once the observation came back (the hook does not purge for a programmatic <c>NavigateTo</c>):
    /// <c>SpaceArrival.razor</c>, <c>SpaceDeleteSection.razor</c>, <c>Home.razor</c> and <c>CreateSpace.razor</c> go
    /// through <c>SpaceNavigation.GoTo</c>; <c>AccountSettings.razor</c> and <c>SignIn.razor</c> leave pages that are in no
    /// Space and are allowed by name.
    /// </summary>
    [Fact]
    public void Every_navigation_that_leaves_a_Space_goes_through_the_one_helper()
    {
        var calls = NavigateToCalls();

        // Vacuity guard: the scan reads the client and finds the calls known to exist.
        Assert.Contains(calls, call => call.StartsWith("Program.cs", StringComparison.Ordinal));

        Assert.Equal([], calls.Where(call => !call.StartsWith(HelperFile, StringComparison.Ordinal) && !Allowed.Keys.Any(file => call.StartsWith(file + ":", StringComparison.Ordinal))));
    }

    /// <summary>
    /// The allow-list names only files that still call <c>NavigateTo</c>, so a stale entry cannot hide a new call.
    /// </summary>
    [Fact]
    public void Every_allowed_file_still_navigates()
    {
        var calls = NavigateToCalls();

        Assert.Equal([], Allowed.Keys.Where(file => !calls.Exists(call => call.StartsWith(file + ":", StringComparison.Ordinal))));
    }

    private static List<string> NavigateToCalls()
    {
        var client = new DirectoryInfo(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client"));

        return [.. client.EnumerateFiles("*.*", SearchOption.AllDirectories)
            .Where(file => (file.Extension is ".cs" or ".razor")
                && !file.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .SelectMany(file => File.ReadAllLines(file.FullName)
                .Select((line, index) => (line, index))
                .Where(entry => NavigateToCall.IsMatch(entry.line) && !entry.line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .Select(entry => $"{file.Name}:{entry.index + 1}: {entry.line.Trim()}"))];
    }

    [GeneratedRegex(@"\.NavigateTo\s*\(", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex NavigateToCall { get; }
}
