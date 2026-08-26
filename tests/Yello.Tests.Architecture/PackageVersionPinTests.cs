using System.Text.Json;
using System.Xml.Linq;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Gate A - AC1's "every dependency is pinned to the AR-1 versions", and AC4's ban on any
/// EF Core in-memory provider.
/// </summary>
/// <remarks>
/// The story statement requires "the stack versions enforced by tests that fail the build",
/// so a pin drifting silently must break the build rather than be caught in review.
/// Changing a version means editing the AR-1 table in epics.md first, then
/// Directory.Packages.props, then the expected table below - in that order.
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-1")]
public sealed class PackageVersionPinTests
{
    /// <summary>
    /// The AR-1 pins, as written. Four of these are behind the current latest
    /// (<c>Asp.Versioning.Http</c> 10.0.0 vs 10.2.x, Aspire 13.4 vs 13.5.x,
    /// <c>Testcontainers.XunitV3</c> 4.6.0 vs 4.14.0, <c>TngTech.ArchUnitNET</c> 0.13.3 vs
    /// 0.13.4). That drift is deliberate and raised as a question for Lee, not resolved
    /// here: AC1 asserts the pins as specified, and refreshing one is an architecture edit.
    /// </summary>
    private static readonly Dictionary<string, string> ExpectedPins = new(StringComparer.Ordinal)
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
        // Yello.AppHost.csproj repeats 13.4.6 in its Sdk attribute, which central package
        // management cannot govern - that copy is asserted separately below.
        ["Aspire.Hosting.AppHost"] = "13.4.6",
        ["Aspire.Hosting.SqlServer"] = "13.4.6",

        ["xunit.v3"] = "4.0.0",
        ["xunit.runner.visualstudio"] = "4.0.0",
        ["Testcontainers.XunitV3"] = "4.6.0",
        ["TngTech.ArchUnitNET"] = "0.13.3",
    };

    /// <summary>
    /// The SDK band pinned in global.json. Two .NET 10 SDKs are installed on the dev machine
    /// (10.0.302 and 10.0.303); without this pin the build is non-deterministic across
    /// machines.
    /// </summary>
    private const string ExpectedSdkVersion = "10.0.303";

    private const string InMemoryProvider = "Microsoft.EntityFrameworkCore.InMemory";

    [Fact]
    public void Every_AR1_dependency_is_pinned_to_the_specified_version()
    {
        var actual = CentralPackageVersions();
        var problems = new List<string>();

        foreach (var (package, expected) in ExpectedPins.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (!actual.TryGetValue(package, out var found))
            {
                problems.Add($"'{package}' has no <PackageVersion> in Directory.Packages.props. AR-1 pins it to {expected}.");
                continue;
            }

            if (!found.Equals(expected, StringComparison.Ordinal))
            {
                problems.Add($"'{package}' is pinned to {found}, but AR-1 specifies {expected}.");
            }
        }

        Assert.True(problems.Count == 0,
            "Directory.Packages.props has drifted from the AR-1 stack table." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            string.Join(Environment.NewLine, problems.Select(p => $"  - {p}")) +
            $"{Environment.NewLine}{Environment.NewLine}" +
            "Changing a pin is an architecture edit, not a developer decision: amend the AR-1 " +
            "table in epics.md first.");
    }

    [Fact]
    public void No_project_declares_an_inline_package_version()
    {
        var offenders = new List<string>();

        foreach (var project in RepositoryLayout.AllProjectFiles)
        {
            var inline = XDocument.Load(project.FullName)
                .Descendants()
                .Where(e => e.Name.LocalName.Equals("PackageReference", StringComparison.Ordinal))
                .Where(e => e.Attribute("Version") is not null)
                .Select(e => e.Attribute("Include")?.Value ?? "(no Include)")
                .ToList();

            offenders.AddRange(inline.Select(pkg =>
                $"{RepositoryLayout.RelativePath(project)}: '{pkg}' carries a Version attribute."));
        }

        Assert.True(offenders.Count == 0,
            "Under central package management, a project declares " +
            "<PackageReference Include=\"...\" /> with NO Version attribute. An inline version " +
            "silently escapes Directory.Packages.props, which is the single place AC1's pins are " +
            $"expressed and the single place this gate reads:{Environment.NewLine}" +
            string.Join(Environment.NewLine, offenders.Select(o => $"  - {o}")));
    }

    [Fact]
    public void The_global_json_sdk_band_is_the_pinned_one()
    {
        Assert.True(RepositoryLayout.GlobalJson.Exists,
            "global.json is missing. Without it the build picks whichever .NET 10 SDK the " +
            "machine happens to resolve first, and two are installed here.");

        using var document = JsonDocument.Parse(File.ReadAllText(RepositoryLayout.GlobalJson.FullName));

        var version = document.RootElement.GetProperty("sdk").GetProperty("version").GetString();

        Assert.True(ExpectedSdkVersion.Equals(version, StringComparison.Ordinal),
            $"global.json pins SDK '{version}', expected '{ExpectedSdkVersion}'.");
    }

    /// <summary>
    /// The AppHost's Aspire version lives in its <c>Sdk</c> attribute, which central package
    /// management cannot reach. That makes it the one version in the solution able to drift
    /// away from the rest of the Aspire pin unnoticed, so it is asserted directly.
    /// </summary>
    [Fact]
    public void The_AppHost_Sdk_attribute_matches_the_pinned_Aspire_version()
    {
        var appHost = RepositoryLayout.AllProjectFiles
            .Single(p => RepositoryLayout.ProjectName(p).Equals("Yello.AppHost", StringComparison.Ordinal));

        var sdk = XDocument.Load(appHost.FullName).Root?.Attribute("Sdk")?.Value;
        var expected = $"Aspire.AppHost.Sdk/{ExpectedPins["Aspire.Hosting.AppHost"]}";

        Assert.True(expected.Equals(sdk, StringComparison.Ordinal),
            $"{RepositoryLayout.RelativePath(appHost)} declares Sdk='{sdk}', expected '{expected}'. " +
            "Central package management cannot govern an Sdk attribute, so this copy of the " +
            "Aspire version has to be asserted directly.");
    }

    /// <summary>
    /// AC4 states the in-memory ban as a property of the solution, so it needs a gate rather
    /// than a convention. The reason belongs in the failure message: an in-memory provider
    /// cannot exercise row-level security, which is what NFR-1 rests on.
    /// </summary>
    [Fact]
    [Trait("Requirement", "NFR-1")]
    public void No_EF_Core_in_memory_provider_is_centrally_available()
    {
        Assert.False(CentralPackageVersions().ContainsKey(InMemoryProvider),
            $"Directory.Packages.props declares a <PackageVersion> for {InMemoryProvider}. " +
            "It must not: an in-memory provider cannot exercise row-level security, which is " +
            "what NFR-1 rests on. Leaving the version centrally unavailable is the cheapest " +
            "enforcement of AC4's ban - a project that references it then fails to restore.");
    }

    [Fact]
    [Trait("Requirement", "NFR-1")]
    public void No_test_project_references_an_EF_Core_in_memory_provider()
    {
        var offenders = RepositoryLayout.AllProjectFiles
            .Where(RepositoryLayout.IsUnderTestsDirectory)
            .Where(p => RepositoryLayout.DeclaredPackageReferences(p)
                .Any(r => r.Contains("InMemory", StringComparison.OrdinalIgnoreCase)))
            .Select(RepositoryLayout.RelativePath)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.True(offenders.Count == 0,
            "These test projects reference an EF Core in-memory provider, which AC4 forbids. " +
            "An in-memory provider cannot exercise row-level security, which is what NFR-1 " +
            "rests on - suites run against the real SQL Server container in " +
            $"{AllowedReferenceEdges.DeclaredVariance} instead:{Environment.NewLine}" +
            string.Join(Environment.NewLine, offenders.Select(o => $"  - {o}")));
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
        var offenders = RepositoryLayout.AllProjectFiles
            .Where(p => RepositoryLayout.DeclaredPackageReferences(p)
                .Contains("Microsoft.NET.Test.Sdk", StringComparer.OrdinalIgnoreCase))
            .Select(RepositoryLayout.RelativePath)
            .ToList();

        Assert.True(offenders.Count == 0,
            $"Microsoft.NET.Test.Sdk is referenced by: {string.Join(", ", offenders)}. " +
            "xunit.v3 4.0.0 runs on Microsoft.Testing.Platform only; there is no VSTest path here.");
    }

    private static Dictionary<string, string> CentralPackageVersions() =>
        XDocument.Load(RepositoryLayout.DirectoryPackagesProps.FullName)
            .Descendants()
            .Where(e => e.Name.LocalName.Equals("PackageVersion", StringComparison.Ordinal))
            .Where(e => e.Attribute("Include") is not null && e.Attribute("Version") is not null)
            .ToDictionary(
                e => e.Attribute("Include")!.Value,
                e => e.Attribute("Version")!.Value,
                StringComparer.Ordinal);
}
