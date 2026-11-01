using System.Text.RegularExpressions;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 4.1 AC11 (ledger <c>deferred-work.md:425</c>, AR-37): the Communication Services resources replace the named,
/// disabled slot in <c>infra/main.bicep</c>, the connection string reaches the container only as a Key Vault secret
/// reference named <c>yello-acs-connection</c> and exposed as <c>ConnectionStrings__acs</c>, and the sender address is
/// plain configuration. Written by the story 4.1 ATDD run, 2026-11-01. Never deployed.
/// </summary>
/// <remarks>
/// <c>Every_connection_string_reaches_the_container_as_a_key_vault_secret_reference</c> keeps its rule unchanged and must
/// pass with the new entry; plant 15 (<c>ConnectionStrings__acs</c> as <c>value:</c>) turns that gate red. This case adds
/// the anchor that the entry exists at all, which the general gate does not require.
/// </remarks>
public sealed partial class SecretHygieneTests
{
    /// <summary>
    /// AC11, AR-37 - an Email Communication Service with an Azure-managed domain and a Communication Services resource
    /// linked to it are declared; the Key Vault secret <c>yello-acs-connection</c> is written from <c>listKeys()</c> inside
    /// the template, never a parameter; the container reads it as <c>ConnectionStrings__acs</c> through a
    /// <c>secretRef</c>; and the sender is an ordinary <c>Email__Sender</c> value.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-37")]
    [Trait("Requirement", "NFR-6")]
    public void The_acs_connection_string_reaches_the_container_as_a_key_vault_secret_written_from_listKeys()
    {
        var bicep = BicepText();

        Assert.Contains("Microsoft.Communication/emailServices", bicep, StringComparison.Ordinal);
        Assert.Contains("Microsoft.Communication/emailServices/domains", bicep, StringComparison.Ordinal);
        Assert.Contains("AzureManaged", bicep, StringComparison.Ordinal);
        Assert.Contains("Microsoft.Communication/communicationServices", bicep, StringComparison.Ordinal);

        Assert.True(
            Regex.IsMatch(bicep, @"name:\s*'yello-acs-connection'[^}]*listKeys\(", RegexOptions.Singleline, TimeSpan.FromSeconds(1)),
            "The yello-acs-connection secret is not written from listKeys() inside the template.");

        var acs = ConnectionStringEnvEntry.Matches(bicep).Where(entry => entry.Groups["key"].Value == "ConnectionStrings__acs").ToList();

        Assert.NotEmpty(acs);
        Assert.All(acs, entry => Assert.Equal("secretRef", entry.Groups["kind"].Value));
        Assert.Matches(new Regex(@"name:\s*'Email__Sender'[\s,]*value\s*:", RegexOptions.None, TimeSpan.FromSeconds(1)), bicep);
        Assert.DoesNotContain("A NAMED, DISABLED SLOT", bicep, StringComparison.Ordinal);
    }

    /// <summary>
    /// Story 4.1 Q13 (a)'s safeguard (orchestrator's condition, 2026-11-01) - the file-drop email sender is chosen whenever
    /// <c>Email:DropFolder</c> is set, in any environment, so no deployed configuration may set it: nothing under
    /// <c>infra/</c>, no <c>appsettings.Production*.json</c> anywhere in the repository, and nothing in the AppHost names
    /// <c>DropFolder</c>, in either spelling (<c>Email:DropFolder</c>, <c>Email__DropFolder</c>). A deployed Host then has
    /// <c>ConnectionStrings__acs</c> or refuses to start; it can never write mail to a disk. Anchor: the scan reads
    /// <c>infra/main.bicep</c> and the AppHost's <c>Program.cs</c>, and the setting it looks for is live - the Host's
    /// Development configuration does name it.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-37")]
    [Trait("Requirement", "NFR-6")]
    public void No_deployed_configuration_sets_the_email_drop_folder()
    {
        var root = RepositoryLayout.Root;

        var development = new FileInfo(Path.Combine(root.FullName, "Yello.Host", "appsettings.Development.json"));

        Assert.Contains("DropFolder", File.ReadAllText(development.FullName), StringComparison.Ordinal);

        // Widened at code review (2026-11-01): every appsettings file but Development's - the base appsettings.json loads
        // in every environment, and a Staging file would deploy - and the CI workflows, whose deploy job runs
        // `az containerapp update`. What no scan can see (a setting added by hand on a revision) is refused at runtime:
        // a Host in Azure Container Apps will not start with a drop folder (Lee's D1 (a), EmailServices).
        var deployed = root.EnumerateFiles("*.bicep", SearchOption.AllDirectories)
            .Concat(root.EnumerateFiles("*.bicepparam", SearchOption.AllDirectories))
            .Concat(root.EnumerateFiles("appsettings*.json", SearchOption.AllDirectories)
                .Where(file => !file.Name.Equals("appsettings.Development.json", StringComparison.OrdinalIgnoreCase)))
            .Concat(new DirectoryInfo(Path.Combine(root.FullName, "Yello.AppHost")).EnumerateFiles("*", SearchOption.AllDirectories))
            .Concat(new DirectoryInfo(Path.Combine(root.FullName, "infra")).EnumerateFiles("*", SearchOption.AllDirectories))
            .Concat(new DirectoryInfo(Path.Combine(root.FullName, ".github", "workflows")).EnumerateFiles("*", SearchOption.AllDirectories))
            .Where(file => !IsBuildOutput(file))
            .DistinctBy(file => file.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.Contains(deployed, file => RepositoryLayout.RelativePath(file).Equals("infra/main.bicep", StringComparison.Ordinal));
        Assert.Contains(deployed, file => RepositoryLayout.RelativePath(file).Equals("Yello.AppHost/Program.cs", StringComparison.Ordinal));
        Assert.Contains(deployed, file => RepositoryLayout.RelativePath(file).Equals("Yello.Host/appsettings.json", StringComparison.Ordinal));
        Assert.Contains(deployed, file => RepositoryLayout.RelativePath(file).Equals(".github/workflows/ci.yml", StringComparison.Ordinal));

        var offenders = deployed
            .Where(file => File.ReadAllText(file.FullName).Contains("DropFolder", StringComparison.OrdinalIgnoreCase))
            .Select(RepositoryLayout.RelativePath)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "A deployed configuration names the email drop folder, which would write invitations to disk instead of sending them:\n"
            + string.Join('\n', offenders));
    }

    private static bool IsBuildOutput(FileInfo file)
    {
        var path = file.FullName.Replace('\\', '/');

        string[] outputs = ["/bin/", "/obj/", "/node_modules/", "/.git/", "/artifacts/"];

        return Array.Exists(outputs, output => path.Contains(output, StringComparison.OrdinalIgnoreCase));
    }
}
