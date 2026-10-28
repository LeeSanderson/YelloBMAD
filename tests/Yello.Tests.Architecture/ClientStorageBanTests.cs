using Mono.Cecil;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Nothing in the browser client writes to <c>localStorage</c>, <c>sessionStorage</c> or
/// IndexedDB - not a credential, and not Space-scoped content. AC5, UX-DR31.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two rules, one gate, and the wider of the two is the UX one.</b> AC5 bans a <i>credential</i>
/// in the first two stores. UX-DR31 (<c>epics.md:275</c>) bans Space-scoped <i>content</i> in all
/// three. A gate that could tell a credential from a Board would be guessing, so this bans the
/// mechanism: there is no browser storage in this client at all, and the day a story needs one it
/// has to come here and say which rule it believes it satisfies.
/// </para>
/// <para>
/// <b>Read from compiled IL as well as from source text, and neither alone would do.</b> A source
/// scan is satisfied by the string appearing in a comment - and this repository's comments now
/// mention these names in several places, including this file, so a naive scan would find itself.
/// An IL scan alone misses <c>wwwroot</c>: a <c>.js</c> file or an inline script in
/// <c>index.html</c> is never compiled into an assembly. So the IL scan covers the C# and the
/// Razor, and the asset scan covers what the browser loads directly.
/// </para>
/// <para>
/// <b>This is an absence assertion over a client that has no browser storage today</b>, which is
/// exactly the shape that passes vacuously - so
/// <see cref="The_scan_detects_a_planted_storage_call"/> runs the same detector over a planted
/// signal and requires it to fire.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-14")]
[Trait("Requirement", "AD-7")]
[Trait("Requirement", "UX-DR31")]
public sealed class ClientStorageBanTests
{
    /// <summary>
    /// The interop targets that reach browser storage. Assembled at use rather than written as
    /// whole literals, so this gate's own constants are not what it finds when it scans its own
    /// assembly - the same evasion <c>RoleApiScan</c> had to make for the role claim type.
    /// </summary>
    private static readonly string[] BannedStorageNames =
    [
        string.Concat("local", "Storage"),
        string.Concat("session", "Storage"),
        string.Concat("indexed", "DB"),
        string.Concat("open", "Database"),
    ];

    /// <summary>
    /// AC5 and UX-DR31: no compiled client code names a browser storage API.
    /// </summary>
    [Fact]
    public void No_client_code_reaches_browser_storage()
    {
        var clientAssembly = ProductionAssemblies.All
            .Single(assembly => assembly.GetName().Name == "Yello.Client");

        using var module = ModuleDefinition.ReadModule(clientAssembly.Location);

        var found = (
            from type in module.GetTypes()
            from method in type.Methods
            where method.HasBody
            from instruction in method.Body.Instructions
            let operand = DescribeOperand(instruction.Operand)
            where operand is not null && NamesBrowserStorage(operand)
            select $"{type.FullName}.{method.Name} states '{operand}'")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            found.Count == 0,
            "Yello.Client reaches a browser storage API. AC5 forbids writing a credential to "
            + "localStorage or sessionStorage, and UX-DR31 widens that to Space-scoped content in "
            + "either of those or IndexedDB - so the ban is on the mechanism rather than on a "
            + "judgement about what is being stored. The Session is an HttpOnly cookie precisely "
            + "so that nothing here needs one, and the anti-forgery token lives in a field, which "
            + "dies with the tab as it should."
            + $"{Environment.NewLine}{string.Join(Environment.NewLine, found.Select(f => "  - " + f))}");
    }

    /// <summary>
    /// The same ban over what the browser loads directly, which no assembly contains.
    /// </summary>
    /// <remarks>
    /// <c>wwwroot</c> is served as-is. An inline script in <c>index.html</c> or a <c>.js</c> file
    /// beside it would reach browser storage with nothing compiled anywhere, so the IL scan above
    /// cannot see it. Since story 2.7 the client has one JavaScript file of its own -
    /// <c>wwwroot/js/board-drag.js</c>, the Board's pointer and touch drag - and this is what keeps
    /// it, and any file after it, out of browser storage.
    /// </remarks>
    [Fact]
    public void No_client_asset_reaches_browser_storage()
    {
        var client = new DirectoryInfo(
            Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client", "wwwroot"));

        Assert.True(
            client.Exists,
            $"'{client.FullName}' does not exist, so this gate reads nothing and asserts nothing.");

        var assets = client
            .EnumerateFiles("*.*", SearchOption.AllDirectories)
            .Where(file => ScannableExtensions.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(assets);

        var found = (
            from asset in assets
            let text = File.ReadAllText(asset.FullName)
            from name in BannedStorageNames
            where text.Contains(name, StringComparison.OrdinalIgnoreCase)
            select $"{RepositoryLayout.RelativePath(asset)} states '{name}'")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(
            found.Count == 0,
            "A file served to the browser reaches a browser storage API. See the sibling gate's "
            + "message for the two rules this enforces; the difference here is that nothing "
            + "compiles these files, so no IL scan would ever see them."
            + $"{Environment.NewLine}{string.Join(Environment.NewLine, found.Select(f => "  - " + f))}");
    }

    /// <summary>
    /// The planted signal, kept as a permanent test.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>TESTING-CONVENTIONS.md:101</c>: "An absence assertion must be validated against a planted
    /// signal, or it is not a test." Both assertions above pass over a client that has no browser
    /// storage, so on their own they would pass equally well over a detector that matched nothing
    /// at all - a typo in one of the names, a case-sensitivity mistake, a scan that read the wrong
    /// member.
    /// </para>
    /// <para>
    /// The plants are written in the shapes an implementer would actually reach for: the Blazor
    /// interop spelling (<c>localStorage.setItem</c>), the bracket form that a name-only scan on
    /// the dotted spelling would miss, and IndexedDB's entry point.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_scan_detects_a_planted_storage_call()
    {
        var plants = new[]
        {
            string.Concat("local", "Storage.setItem"),
            string.Concat("window['session", "Storage']"),
            string.Concat("indexed", "DB.open"),
            string.Concat("LOCAL", "STORAGE"),
        };

        foreach (var plant in plants)
        {
            Assert.True(
                NamesBrowserStorage(plant),
                $"The browser-storage detector did not fire on '{plant}', so both absence "
                + "assertions beside it prove nothing about a client that does reach storage.");
        }

        // And it does not fire on ordinary client vocabulary, because a gate whose failures a
        // developer learns to dismiss is worse than no gate.
        foreach (var innocent in new[] { "SessionState", "sessionRoutes", "storage-panel", "LocalisationGate" })
        {
            Assert.False(
                NamesBrowserStorage(innocent),
                $"The browser-storage detector fired on '{innocent}', which names no storage API.");
        }
    }

    /// <summary>
    /// The plant that runs the <i>scan</i>, not just the predicate - over real IL that reaches
    /// storage through a typed member rather than a string.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The sibling plant above validates <see cref="NamesBrowserStorage"/> and nothing else.</b>
    /// It passes strings to the predicate directly, so it cannot see how the scan chooses what to
    /// pass it - and the scan's own remarks claimed the plant covered "a scan that read the wrong
    /// member", which was the exact defect present: only <c>ldstr</c> operands were read. This test
    /// exists because that claim needed to become true. Added at code review on 2026-09-19.
    /// </para>
    /// <para>
    /// It scans this assembly rather than <c>Yello.Client</c>, so the plant cannot make the real
    /// gate fail; and it scans one named method rather than the whole module, so it asserts that
    /// <i>this</i> call was seen rather than that something somewhere matched.
    /// </para>
    /// </remarks>
    [Fact]
    public void The_scan_reads_a_called_member_and_not_only_a_string_literal()
    {
        using var module = ModuleDefinition.ReadModule(typeof(ClientStorageBanTests).Assembly.Location);

        var plant = module
            .GetTypes()
            .Single(type => type.Name == nameof(PlantedLocalStorageShim))
            .Methods
            .Single(method => method.Name == nameof(PlantedLocalStorageShim.Reach));

        // Called as well as scanned, for two reasons. MA0182 removes an internal type nothing uses,
        // which would delete the plant; and running it proves the plant is inert - it stores
        // nothing anywhere, so the only thing it contributes to this suite is the IL above.
        PlantedLocalStorageShim.Reach();

        var found = plant.Body.Instructions
            .Select(instruction => DescribeOperand(instruction.Operand))
            .Where(operand => operand is not null)
            .Any(operand => NamesBrowserStorage(operand!));

        Assert.True(
            found,
            $"The IL scan did not see '{nameof(PlantedLocalStorageShim)}.{nameof(PlantedLocalStorageShim.Reach)}' "
            + "reaching browser storage through a typed member. No string literal names the API "
            + "there - which is precisely how ProtectedLocalStorage, ProtectedSessionStorage and "
            + "every wrapper around them compile. A scan that reads only ldstr operands reports "
            + "green while the client writes a credential to localStorage, so AC5 and UX-DR31 "
            + "would both be gated by nothing.");
    }

    /// <summary>
    /// What an instruction names, whether that is a literal string or a member it calls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The IL scan read only <c>ldstr</c> operands until code review on 2026-09-19, which made it
    /// blind to every typed way of reaching browser storage.</b> A raw interop call carries the API
    /// name as a string literal and was caught; <c>ProtectedLocalStorage.SetAsync(...)</c>,
    /// <c>ProtectedSessionStorage</c>, <c>Blazored.LocalStorage.ILocalStorageService.SetItemAsync(...)</c>
    /// or any wrapper compiles to a <c>callvirt</c> whose operand is a <c>MethodReference</c> with no
    /// matching literal anywhere - so AC5's credential ban and UX-DR31's Space-scoped content ban
    /// both reported green against the shapes an implementer is most likely to reach for, being the
    /// convenient ones.
    /// </para>
    /// <para>
    /// The repository already knew how to do this: <c>SessionSeamTests</c> and
    /// <c>LocalisationGateTests</c> both match on <c>MemberReference</c> operands. The declaring
    /// type is included as well as the member name, so a call to <c>ILocalStorageService.SetItemAsync</c>
    /// is caught by the type even though the method name alone names no storage API.
    /// </para>
    /// </remarks>
    /// <param name="operand">The instruction's operand.</param>
    /// <returns>Text to test, or <c>null</c> for an operand that names nothing.</returns>
    private static string? DescribeOperand(object? operand) => operand switch
    {
        string text => text,

        // BEFORE MemberReference, because Cecil's TypeReference derives from it - putting this arm
        // second makes it unreachable, which the compiler refuses outright.
        TypeReference type => type.FullName,
        MemberReference member => $"{member.DeclaringType?.FullName}.{member.Name}",
        _ => null,
    };

    private static bool NamesBrowserStorage(string text) =>
        Array.Exists(BannedStorageNames, name => text.Contains(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The asset kinds a browser executes or loads, which no assembly contains.
    /// </summary>
    private static readonly string[] ScannableExtensions =
        [".html", ".js", ".mjs", ".css", ".json"];
}

/// <summary>
/// A stand-in for the typed browser-storage wrappers a Blazor implementer actually reaches for,
/// so that <see cref="ClientStorageBanTests.The_scan_reads_a_called_member_and_not_only_a_string_literal"/>
/// has real IL to scan.
/// </summary>
/// <remarks>
/// <para>
/// <b>It lives in the test assembly, never in <c>Yello.Client</c>.</b> The gate it validates scans
/// the client's module, so a plant placed there would fail the real assertion rather than prove it
/// works - and a plant that has to be deleted before the suite passes is not a permanent test.
/// </para>
/// <para>
/// The name carries the banned token the way <c>ProtectedLocalStorage</c> does: the detector must
/// catch this through the <i>declaring type</i> of the call, because <see cref="Reach"/> contains no
/// string literal naming any storage API. That is the whole point - it is the shape that slipped
/// past the scan until code review on 2026-09-19.
/// </para>
/// </remarks>
internal static class PlantedLocalStorageShim
{
    /// <summary>
    /// Calls the planted member, and says nothing in a string while doing it.
    /// </summary>
    internal static void Reach() => SetItem("yello_session", "not-a-real-credential");

    private static void SetItem(string key, string value)
    {
        // Deliberately empty. Nothing is stored anywhere - the parameters exist so the call site
        // compiles to a real `call` instruction with a MethodReference operand, which is the only
        // thing the scan under test is looking at.
        _ = key;
        _ = value;
    }
}
