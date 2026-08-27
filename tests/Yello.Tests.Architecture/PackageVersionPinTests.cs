using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Gate A - AC1's "every dependency is pinned to the AR-1 versions", and AC4's ban on any
/// database provider that cannot exercise row-level security.
/// </summary>
/// <remarks>
/// <para>
/// The story statement requires "the stack versions enforced by tests that fail the build",
/// so a pin drifting silently must break the build rather than be caught in review.
/// Changing a version means editing the AR-1 table in epics.md first, then
/// Directory.Packages.props, then the expected table below - in that order.
/// </para>
/// <para>
/// <b>The pin assertions are exact in both directions.</b> Iterating only the expected table
/// does not gate: it says nothing about a pin the file has and the table does not, so a
/// package could be added, removed, re-versioned or set to a floating range with every
/// assertion green. That is the same defect Gate A's ring rule was hardened out of when a
/// subset check reported green over a solution with no project references at all, and it is
/// closed here the same way.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-1")]
public sealed class PackageVersionPinTests
{
    /// <summary>
    /// The runtime patch, read from the file that owns it rather than restated here.
    /// </summary>
    private static readonly string PinnedRuntimeVersion =
        RepositoryLayout.LoadXml(RepositoryLayout.DirectoryBuildProps)
            .Descendants()
            .Where(e => e.Name.LocalName.Equals("RuntimeFrameworkVersion", StringComparison.Ordinal))
            .Where(RepositoryLayout.IsUnconditional)
            .Select(e => e.Value.Trim())
            .LastOrDefault()
        ?? throw new InvalidOperationException(
            "Directory.Build.props declares no unconditional <RuntimeFrameworkVersion>, so the " +
            "Blazor WASM pins have nothing to track. ProjectFileGateTests asserts that pin " +
            "directly and will say so too.");

    /// <summary>
    /// The AR-1 pins, as written. Four of these are behind the current latest
    /// (<c>Asp.Versioning.Http</c> 10.0.0 vs 10.2.x, Aspire 13.4 vs 13.5.x,
    /// <c>Testcontainers.XunitV3</c> 4.6.0 vs 4.14.0, <c>TngTech.ArchUnitNET</c> 0.13.3 vs
    /// 0.13.4). That drift is deliberate and raised as a question for Lee, not resolved
    /// here: AC1 asserts the pins as specified, and refreshing one is an architecture edit.
    /// </summary>
    private static readonly Dictionary<string, string> ExpectedPins = new(StringComparer.OrdinalIgnoreCase)
    {
        // EF Core 10 - pinned to the runtime patch. Not yet referenced anywhere; story 1.3
        // adds the first reference against the version pinned here.
        ["Microsoft.EntityFrameworkCore"] = "10.0.11",
        ["Microsoft.EntityFrameworkCore.Design"] = "10.0.11",
        ["Microsoft.EntityFrameworkCore.SqlServer"] = "10.0.11",

        // ASP.NET Core Identity 10. Wired for authentication ONLY - Account store, password
        // hashing, cookie issuance. The Role API is banned outright; see RoleApiBanTests.
        ["Microsoft.AspNetCore.Identity.EntityFrameworkCore"] = "10.0.11",

        ["Asp.Versioning.Http"] = "10.0.0",

        // AR-1 gives Aspire no patch, so 13.4.6 is the last of the pinned minor line.
        // Yello.AppHost.csproj repeats 13.4.6 in its Sdk attribute and .config/dotnet-tools.json
        // repeats it a third time; central package management can govern neither, so both
        // copies are asserted separately below.
        ["Aspire.Hosting.AppHost"] = "13.4.6",
        ["Aspire.Hosting.SqlServer"] = "13.4.6",

        ["xunit.v3"] = "4.0.0",
        ["xunit.runner.visualstudio"] = "4.0.0",
        ["Testcontainers.XunitV3"] = "4.6.0",
        ["TngTech.ArchUnitNET"] = "0.13.3",

        // AR-1's "ASP.NET Core / Blazor WASM 10" row. These were previously classified as
        // framework packages rather than stack choices and left out of this table entirely -
        // a misreading: epics.md lists the row under "Pinned versions" and the architecture
        // spine gives it its own line in the stack table, so it is an AR-1 pin like any other.
        // AC1's own enumeration omits it while AR-1 keeps it, and the implementation had
        // followed the paraphrase rather than the source.
        //
        // DERIVED from the runtime pin, not restated. Directory.Packages.props says these "must
        // track the runtime patch pinned in Directory.Build.props", and nothing asserted that:
        // both this table and the framework gate hard-coded 10.0.11 independently, so bumping the
        // runtime would have left the WASM pins behind with every assertion green. That is the
        // same unasserted-coupling defect this suite already closes for the Aspire SDK attribute
        // and the tool manifest, and the fix is to have one source rather than three copies.
        ["Microsoft.AspNetCore.Components.WebAssembly"] = PinnedRuntimeVersion,
        ["Microsoft.AspNetCore.Components.WebAssembly.DevServer"] = PinnedRuntimeVersion,
    };

    /// <summary>
    /// Pins that are genuinely not AR-1 stack choices, each with the reason it exists.
    /// </summary>
    /// <remarks>
    /// This table is not a lower bar than the one above - both are asserted exactly. It is a
    /// separate table so that "which pins does the architecture own" stays answerable, and so
    /// that adding one of these does not read as an architecture edit.
    /// </remarks>
    private static readonly Dictionary<string, string> ExpectedNonAr1Pins = new(StringComparer.OrdinalIgnoreCase)
    {
        // AC4's one-shot connectivity check needs a SQL driver and .NET ships none. 7.0.2 is
        // where the transitive floors of Aspire.Hosting.SqlServer (>= 7.0.1) and
        // Microsoft.EntityFrameworkCore.SqlServer (>= 6.1.6) both hold.
        ["Microsoft.Data.SqlClient"] = "7.0.2",

        // The fixture-library half of xunit.v3, for Yello.Tests.Shared, pinned transitively
        // so Testcontainers.XunitV3 4.6.0 cannot silently unify it at its own 2.0.2.
        ["xunit.v3.extensibility.core"] = "4.0.0",

        // The MsSql module of the AR-1 Testcontainers pin, at the same version.
        ["Testcontainers.MsSql"] = "4.6.0",

        // The xunit v3 assertion adapter of the AR-1 ArchUnitNET pin, at the same version.
        ["TngTech.ArchUnitNET.xUnitV3"] = "0.13.3",

        // Transitive security pin, not a stack choice: GHSA-q939-rpr3-3284, first patched in
        // 2026.0.0. See the comment in Directory.Packages.props.
        ["SSH.NET"] = "2026.0.0",
    };

    /// <summary>
    /// The SDK band pinned in global.json. Two .NET 10 SDKs are installed on the dev machine
    /// (10.0.302 and 10.0.303); without this pin the build is non-deterministic across
    /// machines.
    /// </summary>
    private const string ExpectedSdkVersion = "10.0.303";

    /// <summary>
    /// The two switches that make central package management real.
    /// </summary>
    private static readonly string[] CentralManagementSwitches =
        ["ManagePackageVersionsCentrally", "CentralPackageTransitivePinningEnabled"];

    /// <summary>
    /// Providers that cannot exercise row-level security, which is what NFR-1 rests on.
    /// </summary>
    /// <remarks>
    /// The ban named only <c>Microsoft.EntityFrameworkCore.InMemory</c>, which is the letter
    /// of AC4 and less than its reason. SQLite - including <c>:memory:</c> - supports no
    /// <c>CREATE SECURITY POLICY</c> and no <c>SESSION_CONTEXT</c>, so a suite that swapped
    /// to it would satisfy the old ban and still prove nothing about isolation. The reason
    /// the ban exists is the thing to enforce.
    /// </remarks>
    private static readonly string[] BannedProviders =
    [
        "Microsoft.EntityFrameworkCore.InMemory",
        "Microsoft.EntityFrameworkCore.Sqlite",
        "Microsoft.EntityFrameworkCore.Sqlite.Core",
        "Microsoft.Data.Sqlite",
        "Microsoft.Data.Sqlite.Core",
    ];

    /// <summary>
    /// Central package management has to be ON for any of the pins to mean anything, and the
    /// two switches that turn it on are themselves never read by a pin assertion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every other assertion in this class reads <c>PackageVersion</c> <i>elements</i>, which
    /// remain present in the file regardless of whether central package management is on.
    /// </para>
    /// <para>
    /// <b>What each switch actually costs, measured rather than assumed.</b> Turning
    /// <c>ManagePackageVersionsCentrally</c> off does <i>not</i> silently loosen the pins: from
    /// a <c>.csproj</c> it fails restore outright with NU1015, because no project here carries
    /// an inline version - which <see cref="No_project_declares_a_package_version_of_its_own"/>
    /// independently enforces - and from a root <c>Directory.Build.targets</c> the property
    /// evaluates false while restore still honours the central pins. So the "pins inert,
    /// assertions green" exposure does not exist while that holds, and this assertion states
    /// the invariant directly rather than inferring it from NuGet's error behaviour.
    /// <c>CentralPackageTransitivePinningEnabled</c> is the consequential one: turn it off and
    /// the <c>SSH.NET</c> 2026.0.0 forward-pin stops applying, silently reinstating
    /// GHSA-q939-rpr3-3284 (HIGH), with no NuGet backstop at all.
    /// </para>
    /// <para>
    /// <b>Asserted from evaluated state, per project.</b> Two text-reading versions of this
    /// assertion were both defeated. "Any occurrence equals true" passed on a <c>true</c>
    /// followed by a <c>false</c>. Reading the last <i>unconditional</i> declaration then passed
    /// on a <c>Condition</c>-guarded <c>false</c> whose condition fires - and separately, the
    /// switch is settable from a root <c>Directory.Build.targets</c>, which this file's own
    /// guidance points future stories towards and which a gate scoped to
    /// <c>Directory.Packages.props</c> cannot see. Both were demonstrated with
    /// <c>dotnet msbuild -getProperty:</c> returning <c>false</c> where the text said
    /// <c>true</c>. Asking MSBuild per project answers the only question that matters: does
    /// central package management apply to this project as it is actually built.
    /// </para>
    /// <para>
    /// One correction to a claim this assertion used to make: turning
    /// <c>CentralPackageTransitivePinningEnabled</c> off does <b>not</b> reinstate
    /// GHSA-q939-rpr3-3284 unnoticed. Measured: <c>dotnet build Yello.slnx</c> then fails with
    /// <c>error NU1903</c> four times over. NuGet is a real backstop here; this assertion is the
    /// one that names the cause rather than leaving a developer to infer it from an advisory.
    /// </para>
    /// </remarks>
    [Fact]
    public void Central_package_management_and_transitive_pinning_are_enabled()
    {
        var problems = new List<string>();

        foreach (var project in RepositoryLayout.AllProjectFiles)
        {
            problems.AddRange(
                from name in CentralManagementSwitches
                let value = MsBuildEvaluation.Property(project, name)
                where !value.Equals("true", StringComparison.OrdinalIgnoreCase)
                select $"{RepositoryLayout.RelativePath(project)} evaluates '{name}' to " +
                       $"'{(value.Length == 0 ? "(unset)" : value)}', not true.");
        }

        // The declared side as well, because "it is switched on for every project" and "it is
        // stated in the one file that owns package versions" are different invariants and both
        // matter: the second is what makes the file worth reading.
        var declared = RepositoryLayout.LoadXml(RepositoryLayout.DirectoryPackagesProps)
            .Descendants()
            .Where(e => e.Parent?.Name.LocalName.Equals("PropertyGroup", StringComparison.Ordinal) == true)
            .Where(RepositoryLayout.IsUnconditional)
            .Select(e => e.Name.LocalName)
            .ToList();

        problems.AddRange(CentralManagementSwitches
            .Where(name => !declared.Contains(name, StringComparer.Ordinal))
            .Select(name => $"'{name}' is not stated unconditionally in Directory.Packages.props, " +
                            "which is the file that owns it."));

        Assert.True(problems.Count == 0,
            "Central package management is what makes every pin in this file load-bearing, and " +
            "the switches that enable it are not themselves pinned by anything." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            string.Join(Environment.NewLine, problems.Select(p => $"  - {p}")) +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "Without ManagePackageVersionsCentrally every version-less PackageReference would " +
            "resolve freely - though NuGet fails restore with NU1015 first, since no project " +
            "here carries an inline version. Without CentralPackageTransitivePinningEnabled the " +
            "SSH.NET forward-pin for GHSA-q939-rpr3-3284 stops applying, and NuGet then fails " +
            "the build with NU1903 rather than passing silently. This assertion exists to name " +
            "the cause instead of leaving someone to work back from an advisory - and because " +
            "these switches are settable from files other than the one that owns them, which " +
            "is why it reads evaluated state per project rather than this file's text.");
    }

    [Fact]
    public void Directory_Packages_props_pins_exactly_the_expected_set_at_the_expected_versions()
    {
        var actual = CentralPackageVersions();
        var expected = ExpectedPins
            .Concat(ExpectedNonAr1Pins)
            .ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase);

        var problems = new List<string>();

        foreach (var (package, want) in expected.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!actual.TryGetValue(package, out var found))
            {
                problems.Add($"'{package}' has no <PackageVersion> in Directory.Packages.props. Expected {want}.");
            }
            else if (!found.Equals(want, StringComparison.Ordinal))
            {
                problems.Add($"'{package}' is pinned to {found}, but {want} is expected.");
            }
            else
            {
                // Pinned, and pinned to the expected version. The unexpected-pin pass below
                // is the other half of the equality.
            }
        }

        foreach (var package in actual.Keys.Except(expected.Keys, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            problems.Add(
                $"'{package}' is pinned in Directory.Packages.props but appears in neither " +
                "expected table. Add it - to ExpectedPins if the AR-1 stack table names it, to " +
                "ExpectedNonAr1Pins with the reason it exists otherwise.");
        }

        Assert.True(problems.Count == 0,
            "Directory.Packages.props does not match the expected pin set exactly." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            string.Join(Environment.NewLine, problems.Select(p => $"  - {p}")) +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "Changing an AR-1 pin is an architecture edit, not a developer decision: amend the " +
            "AR-1 table in epics.md first.");
    }

    [Fact]
    public void No_project_declares_a_package_version_of_its_own()
    {
        var offenders = new List<string>();

        foreach (var project in RepositoryLayout.AllProjectFiles)
        {
            offenders.AddRange(InlineVersionOffenders(project));
        }

        Assert.True(offenders.Count == 0,
            "Under central package management, a project declares " +
            "<PackageReference Include=\"...\" /> with NO version of its own. Every form below " +
            "escapes Directory.Packages.props, which is the single place AC1's pins are " +
            $"expressed and the single place this gate reads:{Environment.NewLine}" +
            string.Join(Environment.NewLine, offenders.Select(o => $"  - {o}")));
    }

    /// <summary>
    /// The three ways a project can carry its own version: the <c>Version</c> attribute, a
    /// child <c>&lt;Version&gt;</c> element, and <c>VersionOverride</c>.
    /// </summary>
    /// <remarks>
    /// Only the attribute used to be read. <c>VersionOverride</c> is central package
    /// management's own sanctioned escape hatch and resolves normally, so a project could
    /// leave the AR-1 pins entirely with the gate green; and the child-element form is the
    /// same value written the other way round, which NuGet honours identically.
    /// </remarks>
    private static IEnumerable<string> InlineVersionOffenders(FileInfo project)
    {
        var path = RepositoryLayout.RelativePath(project);

        foreach (var element in RepositoryLayout.LoadXml(project)
                     .Descendants()
                     .Where(e => e.Name.LocalName.Equals("PackageReference", StringComparison.Ordinal)))
        {
            var package = element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value ?? "(no Include)";

            if (element.Attribute("Version") is not null)
            {
                yield return $"{path}: '{package}' carries a Version attribute.";
            }

            if (element.Attribute("VersionOverride") is not null)
            {
                yield return $"{path}: '{package}' carries a VersionOverride attribute.";
            }

            if (element.Elements().Any(c => c.Name.LocalName.Equals("Version", StringComparison.Ordinal)))
            {
                yield return $"{path}: '{package}' carries a child <Version> element.";
            }
        }
    }

    [Fact]
    public void The_global_json_pins_the_sdk_band_and_opts_into_the_test_platform()
    {
        Assert.True(RepositoryLayout.GlobalJson.Exists,
            "global.json is missing. Without it the build picks whichever .NET 10 SDK the " +
            "machine happens to resolve first, and two are installed here.");

        using var document = RepositoryLayout.LoadJson(RepositoryLayout.GlobalJson);
        var root = document.RootElement;

        var problems = new List<string>();

        // Singularity, for the same reason MsBuildImportFiles asserts it for the props files:
        // the SDK band is only "pinned for the solution" if there is one file stating it. A
        // second global.json deeper in the tree governs everything beneath it, and this gate
        // would go on reading the root copy and reporting green.
        problems.AddRange(RepositoryLayout.EnumerateSourceFiles("global.json")
            .Where(f => !f.FullName.Equals(RepositoryLayout.GlobalJson.FullName, StringComparison.OrdinalIgnoreCase))
            .Select(f => $"a second global.json exists at '{RepositoryLayout.RelativePath(f)}', " +
                         "which selects a different SDK for everything beneath it"));

        AddIfNotEqual(problems, root, ["sdk", "version"], ExpectedSdkVersion,
            "the SDK band is what makes the build deterministic across machines");

        // rollForward is half of what the pin means: without it the `version` above is a
        // floor rather than a band, which is not what "pinned" claims.
        AddIfNotEqual(problems, root, ["sdk", "rollForward"], "latestPatch",
            "without it the pinned version is a floor, not a band");

        // On the .NET 10 SDK the VSTest target refuses outright, so this opt-in is what makes
        // `dotnet test` able to run at all. It was tried in dotnet.config first, where it has
        // no effect - so its absence here is silent and total.
        AddIfNotEqual(problems, root, ["test", "runner"], "Microsoft.Testing.Platform",
            "xunit.v3 4.0.0 runs on Microsoft.Testing.Platform only, and `dotnet test` needs telling");

        Assert.True(problems.Count == 0,
            $"global.json has drifted:{Environment.NewLine}" +
            string.Join(Environment.NewLine, problems.Select(p => $"  - {p}")));
    }

    private static void AddIfNotEqual(
        List<string> problems,
        JsonElement root,
        string[] path,
        string expected,
        string why)
    {
        var current = root;

        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                problems.Add($"'{string.Join('.', path)}' is missing - {why}.");
                return;
            }
        }

        var actual = current.ValueKind == JsonValueKind.String ? current.GetString() : current.ToString();

        if (!expected.Equals(actual, StringComparison.Ordinal))
        {
            problems.Add($"'{string.Join('.', path)}' is '{actual}', expected '{expected}' - {why}.");
        }
    }

    /// <summary>
    /// The AppHost's Aspire version lives in its <c>Sdk</c> attribute, and the CLI's lives in
    /// the local tool manifest. Central package management can reach neither, which makes them
    /// the two copies able to drift away from the rest of the Aspire pin unnoticed.
    /// </summary>
    /// <remarks>
    /// The Sdk attribute was gated from the start; the tool manifest is the same hazard and
    /// was not. <c>dotnet aspire run</c> is the invocation AC4 is verified through, so a CLI
    /// on a different Aspire line is orchestrating the solution with tooling the solution does
    /// not pin.
    /// </remarks>
    [Fact]
    public void Every_ungoverned_copy_of_the_Aspire_version_matches_the_pin()
    {
        var expected = ExpectedPins["Aspire.Hosting.AppHost"];
        var problems = new List<string>();

        // Not Single(): with two project files named Yello.AppHost it throws "Sequence contains
        // more than one matching element", which names neither the project nor the files, and
        // with none it throws a different exception that names nothing at all.
        var appHosts = RepositoryLayout.AllProjectFiles
            .Where(p => RepositoryLayout.ProjectName(p).Equals("Yello.AppHost", StringComparison.Ordinal))
            .ToList();

        if (appHosts.Count != 1)
        {
            problems.Add(
                $"Expected exactly one Yello.AppHost.csproj, found {appHosts.Count}" +
                $"{(appHosts.Count == 0 ? "." : $": {string.Join(", ", appHosts.Select(RepositoryLayout.RelativePath))}.")}");
        }
        else
        {
            var sdk = RepositoryLayout.LoadXml(appHosts[0]).Root?.Attribute("Sdk")?.Value;

            if (!$"Aspire.AppHost.Sdk/{expected}".Equals(sdk, StringComparison.Ordinal))
            {
                problems.Add(
                    $"{RepositoryLayout.RelativePath(appHosts[0])} declares Sdk='{sdk}', expected " +
                    $"'Aspire.AppHost.Sdk/{expected}'.");
            }
        }

        problems.AddRange(AspireCliProblems(expected));

        Assert.True(problems.Count == 0,
            "An Aspire version outside central package management has drifted from the AR-1 " +
            $"pin ({expected}):{Environment.NewLine}" +
            string.Join(Environment.NewLine, problems.Select(p => $"  - {p}")));
    }


    /// <summary>
    /// The <c>tools.aspire.cli.version</c> element, if the manifest has the shape that implies.
    /// </summary>
    /// <remarks>
    /// Every level's <c>ValueKind</c> is checked because <c>TryGetProperty</c> throws
    /// <c>InvalidOperationException</c> on a non-object - so <c>{"tools": []}</c>, or a manifest
    /// where <c>aspire.cli</c> maps straight to a string, produced a bare exception naming
    /// neither the file nor the remedy. That is exactly what <c>RepositoryLayout.LoadJson</c> was
    /// added to prevent, and this gate was reaching past it.
    /// </remarks>
    private static bool TryReadAspireCliVersion(JsonDocument document, out JsonElement version)
    {
        version = default;

        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("tools", out var tools)
            || tools.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!tools.TryGetProperty("aspire.cli", out var cli) || cli.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return cli.TryGetProperty("version", out version);
    }

    private static IEnumerable<string> AspireCliProblems(string expected)
    {
        if (!RepositoryLayout.DotnetToolsManifest.Exists)
        {
            yield return
                ".config/dotnet-tools.json is missing. `aspire` is not on PATH on a developer " +
                "machine; the local tool manifest is both how it is installed and how it is pinned.";
            yield break;
        }

        foreach (var stray in RepositoryLayout.EnumerateSourceFiles("dotnet-tools.json")
                     .Where(f => !f.FullName.Equals(
                         RepositoryLayout.DotnetToolsManifest.FullName, StringComparison.OrdinalIgnoreCase)))
        {
            yield return
                $"a second tool manifest exists at '{RepositoryLayout.RelativePath(stray)}', " +
                "which supplies a different aspire.cli to anything run from beneath it";
        }

        using var document = RepositoryLayout.LoadJson(RepositoryLayout.DotnetToolsManifest);

        if (!TryReadAspireCliVersion(document, out var version))
        {
            yield return
                ".config/dotnet-tools.json does not declare a version for 'aspire.cli' in the " +
                "expected shape ({ \"tools\": { \"aspire.cli\": { \"version\": ... } } }).";
            yield break;
        }

        // Not GetString(): `"version": 13.4` is legal JSON and throws there, which would
        // present as a defect in the gate rather than as a malformed pin.
        var pinned = RepositoryLayout.JsonValueText(version);

        if (!expected.Equals(pinned, StringComparison.Ordinal))
        {
            yield return
                $".config/dotnet-tools.json pins aspire.cli to '{pinned}', expected '{expected}'.";
        }
    }

    /// <summary>
    /// AC4 states the ban as a property of the solution, so it needs a gate rather than a
    /// convention. The reason belongs in the failure message: these providers cannot exercise
    /// row-level security, which is what NFR-1 rests on.
    /// </summary>
    [Fact]
    [Trait("Requirement", "NFR-1")]
    public void No_provider_that_cannot_enforce_row_level_security_is_centrally_available()
    {
        var central = CentralPackageVersions();

        var offenders = BannedProviders
            .Where(central.ContainsKey)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(offenders.Count == 0,
            "Directory.Packages.props declares a <PackageVersion> for a provider that cannot " +
            "enforce row-level security, which is what NFR-1 rests on - neither an in-memory " +
            "provider nor SQLite supports CREATE SECURITY POLICY or SESSION_CONTEXT. Leaving " +
            "the version centrally unavailable is the cheapest enforcement of AC4's ban: a " +
            $"project that references it then fails to restore.{Environment.NewLine}" +
            string.Join(Environment.NewLine, offenders.Select(o => $"  - {o}")));
    }

    [Fact]
    [Trait("Requirement", "NFR-1")]
    public void No_test_project_references_a_provider_that_cannot_enforce_row_level_security()
    {
        // Evaluated, for the same reason as the two bans above: NFR-1 rests on row-level
        // security, and a provider that cannot exercise it must not reach a suite by any route
        // the build honours - property indirection and GlobalPackageReference included.
        var offenders = RepositoryLayout.AllProjectFiles
            .Where(RepositoryLayout.IsUnderTestsDirectory)
            .SelectMany(p => MsBuildEvaluation.PackageIds(p)
                .Where(r => BannedProviders.Contains(r, StringComparer.OrdinalIgnoreCase))
                .Select(r => $"{RepositoryLayout.RelativePath(p)} references {r}"))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.True(offenders.Count == 0,
            "These test projects reference a database provider that cannot enforce row-level " +
            "security, which AC4 forbids and NFR-1 rests on - suites run against the real SQL " +
            $"Server container in {AllowedReferenceEdges.DeclaredVariance} instead:" +
            $"{Environment.NewLine}" +
            string.Join(Environment.NewLine, offenders.Select(o => $"  - {o}")));
    }

    /// <summary>
    /// The ring rule for packages. See
    /// <see cref="AllowedReferenceEdges.ForbiddenPackagePrefixes"/> for why a ban rather than
    /// an allow-list, and why this is not covered by Gate B.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-2")]
    public void No_project_references_a_package_its_ring_forbids()
    {
        var violations = new List<string>();

        foreach (var project in RepositoryLayout.AllProjectFiles)
        {
            var name = RepositoryLayout.ProjectName(project);

            if (!AllowedReferenceEdges.ForbiddenPackagePrefixes.TryGetValue(name, out var forbidden))
            {
                continue;
            }

            // Evaluated, not declared. Reading the XML attribute compared the literal text, so
            // <PackageReference Include="$(Orm)" /> and Include="@(Orm)" both restored EF Core
            // while this ban matched neither - demonstrated during the third review pass by
            // planting EF Core into Yello.Domain, the ring that may reference nothing, with all
            // 47 assertions green. Evaluation also folds in GlobalPackageReference, which
            // reached every project from a file this gate never read.
            violations.AddRange(
                from package in MsBuildEvaluation.PackageIds(project)
                from prefix in forbidden
                where package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                select $"{RepositoryLayout.RelativePath(project)}: '{name}' references '{package}', " +
                       $"which matches the forbidden prefix '{prefix}'.");
        }

        Assert.True(violations.Count == 0,
            "A project references a package its ring forbids (AD-21 / AR-2). A package " +
            "reference crosses a ring boundary exactly as a project reference does, and Gate B " +
            "cannot see it until a type is actually touched - which is why this is asserted " +
            $"here, from the project file:{Environment.NewLine}" +
            string.Join(Environment.NewLine, violations.Select(v => $"  - {v}")));
    }

    /// <summary>
    /// <c>GlobalPackageReference</c> applies a package to every project in the solution
    /// without any project mentioning it, so the set of them is asserted exactly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This one assertion closes a bypass that ran under every other gate in this class at
    /// once. <c>CentralPackageVersions</c> reads <c>PackageVersion</c> elements and
    /// <c>DeclaredPackageReferences</c> reads project files, so a single line in
    /// <c>Directory.Packages.props</c> -
    /// <c>&lt;GlobalPackageReference Include="Microsoft.EntityFrameworkCore.InMemory"
    /// Version="10.0.11" /&gt;</c> - added the banned provider to all fourteen projects and
    /// was invisible to the three assertions that exist to stop it. The same line could carry
    /// <c>Microsoft.NET.Test.Sdk</c> past the VSTest ban, or EF Core past the ring's package
    /// ban, for the same reason.
    /// </para>
    /// <para>
    /// It is already the established idiom in that file, which is what makes it the natural
    /// way a later story would write exactly this by accident.
    /// </para>
    /// <para>
    /// The version is deliberately not asserted. The coding standard's version is a developer
    /// decision rather than an architecture edit - the file says so, and that stance is
    /// settled. What is asserted is that nothing <i>else</i> arrives by this route.
    /// </para>
    /// </remarks>
    [Fact]
    [Trait("Requirement", "AR-35")]
    public void The_only_solution_wide_package_is_the_coding_standard()
    {
        var sanctioned = RepositoryLayout
            .ItemIncludes(RepositoryLayout.DirectoryPackagesProps, "GlobalPackageReference")
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var problems = sanctioned
            .Where(p => !p.Equals("Opinionated.DotNet.CodingStandards", StringComparison.OrdinalIgnoreCase))
            .Select(p => $"Directory.Packages.props declares GlobalPackageReference '{p}', which is " +
                         "not the coding standard.")
            .ToList();

        if (sanctioned.Count == 0)
        {
            problems.Add(
                "Directory.Packages.props declares no GlobalPackageReference at all, so the " +
                "coding standard - the single source of TreatWarningsAsErrors, the analysers " +
                "and the NuGet audit settings - has been removed.");
        }

        // EVERY other file, project files included. Scoping this assertion to the sanctioned
        // home was itself the bypass: the identical line in Directory.Build.props reaches every
        // project and was read by nothing here and nothing in ProjectFileGateTests. Verified
        // end-to-end during review - Microsoft.NET.Test.Sdk placed that way applied its build
        // assets solution-wide. The story's plant used this file, which is the one that was
        // already covered.
        foreach (var file in RepositoryLayout.AllProjectFiles
                     .Concat(RepositoryLayout.MsBuildImportFiles)
                     .Where(f => !f.FullName.Equals(
                         RepositoryLayout.DirectoryPackagesProps.FullName, StringComparison.OrdinalIgnoreCase)))
        {
            problems.AddRange(RepositoryLayout.ItemIncludes(file, "GlobalPackageReference")
                .Select(p => $"{RepositoryLayout.RelativePath(file)} declares " +
                             $"GlobalPackageReference '{p}'. Directory.Packages.props is the one " +
                             "sanctioned home for it; from anywhere else it is a solution-wide " +
                             "package that no per-project gate can see."));
        }

        Assert.True(problems.Count == 0,
            "A GlobalPackageReference reaches all fourteen projects while appearing in none of " +
            "them, so it bypasses every per-project gate here - the row-level-security provider " +
            $"ban, the VSTest ban and the ring's package ban alike:{Environment.NewLine}" +
            string.Join(Environment.NewLine, problems.Select(p => $"  - {p}")));
    }

    /// <summary>
    /// xunit.v3 4.0.0 depends on xunit.v3.mtp-v2 [4.0.0] - it runs on Microsoft.Testing
    /// Platform only. There is no VSTest path in this solution, so Microsoft.NET.Test.Sdk
    /// must stay absent; adding it produces two runners disagreeing about how tests are
    /// discovered.
    /// </summary>
    [Fact]
    public void No_project_references_the_VSTest_SDK()
    {
        // Evaluated: the same property- and item-indirection that defeated the ring package ban
        // defeated this one, and a GlobalPackageReference in Directory.Build.props applied the
        // VSTest SDK's build assets to every project (IsTestProject and GenerateProgramFile both
        // evaluated true in projects declaring neither).
        var offenders = RepositoryLayout.AllProjectFiles
            .Where(p => MsBuildEvaluation.PackageIds(p)
                .Contains("Microsoft.NET.Test.Sdk", StringComparer.OrdinalIgnoreCase))
            .Select(RepositoryLayout.RelativePath)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"Microsoft.NET.Test.Sdk is referenced by: {string.Join(", ", offenders)}. " +
            "xunit.v3 4.0.0 runs on Microsoft.Testing.Platform only; there is no VSTest path here.");
    }

    /// <summary>
    /// Reads <c>Directory.Packages.props</c>, honouring both spellings of a version and NuGet's
    /// own case-insensitivity.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Package ids are case-insensitive to NuGet, so an Ordinal dictionary let
    /// <c>microsoft.entityframeworkcore.inmemory</c> restore correctly while the ban that
    /// reads this missed it - AC4 bypassed by letter case alone. Values are trimmed for the
    /// same reason: <c>Include=" xunit.v3 "</c> resolves and would not have matched.
    /// </para>
    /// <para>
    /// A version written as a child element rather than an attribute is honoured too. Filtered
    /// out, it made the banned package look absent while NuGet honoured the version and the
    /// project restored.
    /// </para>
    /// <para>
    /// <b>Two ways an element used to disappear from this dictionary entirely</b>, each of
    /// which hid a package from the provider ban and from the exact-set assertion at once.
    /// A <c>PackageVersion</c> carrying <i>no</i> version was skipped, and it restores - to the
    /// lowest available version, with only NU1604 - so
    /// <c>&lt;PackageVersion Include="Microsoft.EntityFrameworkCore.InMemory" /&gt;</c> was a
    /// present, banned provider that read as absent. And a <c>Condition</c> on the element or
    /// its <c>ItemGroup</c> was ignored, so a pin could be green here and never reach restore
    /// (verified: <c>-getItem:PackageVersion</c> returns empty for a guarded declaration).
    /// Both are now reported rather than skipped, which is also why this method throws instead
    /// of returning a quietly smaller dictionary.
    /// </para>
    /// </remarks>
    private static Dictionary<string, string> CentralPackageVersions()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var duplicates = new List<string>();
        var malformed = new List<string>();

        foreach (var element in RepositoryLayout.LoadXml(RepositoryLayout.DirectoryPackagesProps)
                     .Descendants()
                     .Where(e => e.Name.LocalName.Equals("PackageVersion", StringComparison.Ordinal)))
        {
            var include = element.Attribute("Include")?.Value.Trim();
            var version = VersionOf(element);

            if (!RepositoryLayout.IsUnconditional(element))
            {
                malformed.Add(
                    $"'{include ?? "(no Include)"}' is declared inside a Condition, so whether " +
                    "this pin reaches restore depends on a condition no gate here evaluates");
                continue;
            }

            if (string.IsNullOrEmpty(include))
            {
                malformed.Add("a <PackageVersion> carries no Include attribute");
                continue;
            }

            if (version is null)
            {
                malformed.Add(
                    $"'{include}' has a <PackageVersion> with no version, which restores to the " +
                    "LOWEST available version rather than not restoring - so the package is " +
                    "present while reading as absent to the provider ban");
                continue;
            }

            // Not ToDictionary: a duplicate Include throws there, and the exception names the
            // key without naming the file or saying what to do about it.
            if (!result.TryAdd(include, version))
            {
                duplicates.Add(include);
            }
        }

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                "Directory.Packages.props declares more than one <PackageVersion> for: " +
                $"{string.Join(", ", duplicates)}. NuGet takes the last, so the file says one " +
                "thing and the restore does another. Remove the duplicate.");
        }

        if (malformed.Count > 0)
        {
            throw new InvalidOperationException(
                "Directory.Packages.props declares a <PackageVersion> this gate cannot read as " +
                $"a pin:{Environment.NewLine}" +
                string.Join(Environment.NewLine, malformed.Select(m => $"  - {m}")) +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "Every pin has to be one unconditional statement with a version, because both " +
                "the AR-1 pin set and the row-level-security provider ban are asserted from " +
                "this dictionary. An element that cannot be read is a package the bans cannot " +
                "see, not a package that is absent.");
        }

        return result;
    }

    private static string? VersionOf(XElement element) =>
        element.Attribute("Version")?.Value.Trim()
        ?? element.Elements()
            .FirstOrDefault(c => c.Name.LocalName.Equals("Version", StringComparison.Ordinal))
            ?.Value.Trim();
}
