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
    [Fact(Skip = "red: AC11 - the ACS resources, the yello-acs-connection secret and ConnectionStrings__acs arrive with story 4.1 Task 8")]
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
}
