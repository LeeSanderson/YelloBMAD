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
            where instruction.Operand is string text && NamesBrowserStorage(text)
            select $"{type.FullName}.{method.Name} states '{instruction.Operand}'")
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
    /// cannot see it. There is no JavaScript file in this client at all today, and this is what
    /// keeps that true for the purpose it matters for.
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

    private static bool NamesBrowserStorage(string text) =>
        Array.Exists(BannedStorageNames, name => text.Contains(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The asset kinds a browser executes or loads, which no assembly contains.
    /// </summary>
    private static readonly string[] ScannableExtensions =
        [".html", ".js", ".mjs", ".css", ".json"];
}
