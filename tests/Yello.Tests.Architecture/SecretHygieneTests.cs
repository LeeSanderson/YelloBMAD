using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// No secret exists in source, in appsettings, or in the deployment definitions; in Azure the
/// connection string arrives from Key Vault through a secret reference. Story 1.10 AC3 (AR-37,
/// spine Config convention <c>:273</c>); Task 11.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three gates.</b> The credential scan covers every <c>appsettings*.json</c>,
/// <c>launchSettings.json</c>, the AppHost, <c>infra/</c> and <c>.github/</c>; the appsettings rule keeps
/// connection strings out of the Host's settings; and the Key Vault rule reads the Bicep. Each is planted.
/// </para>
/// <para>
/// <b>A text gate, with anchors</b> (Q18 (a)): no Bicep or YAML parser package. Each case first finds
/// the file it names, so a scan that read nothing fails.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-37")]
public sealed partial class SecretHygieneTests
{
    /// <summary>
    /// 1.10-ARCH-004 - no credential-shaped content in any configuration or deployment file.
    /// </summary>
    /// <remarks>
    /// <b>Active - green today, a standing gate.</b> Plant 1 (story Task 11): a connection string with
    /// <c>Password=</c> in <c>Yello.Host/appsettings.json</c>.
    /// </remarks>
    [Fact]
    public void No_configuration_or_deployment_file_holds_a_credential()
    {
        var files = ScannedFiles();

        // Vacuity guard: the scan reads the Host's own appsettings.
        Assert.Contains(files, file => RepositoryLayout.RelativePath(file).Equals("Yello.Host/appsettings.json", StringComparison.Ordinal));

        var offenders = (
            from file in files
            let text = File.ReadAllText(file.FullName)
            from pattern in CredentialShapes()
            where pattern.IsMatch(text)
            select $"{RepositoryLayout.RelativePath(file)}: {pattern}")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "A credential is held in a file that is committed, or copied into an image. Every secret comes from "
            + "Key Vault in Azure and from user-secrets locally (AR-37, AC3):\n" + string.Join('\n', offenders));
    }

    /// <summary>
    /// 1.10-ARCH-005 - <c>Yello.Host/appsettings*.json</c> carry no <c>ConnectionStrings</c> section,
    /// with or without a password: the connection string is an environment value.
    /// </summary>
    /// <remarks><b>Active - green today, a standing gate.</b></remarks>
    [Fact]
    public void The_hosts_appsettings_carry_no_connection_strings()
    {
        var host = new DirectoryInfo(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Host"));
        var settings = host.EnumerateFiles("appsettings*.json", SearchOption.TopDirectoryOnly).ToList();

        Assert.Contains(settings, file => file.Name.Equals("appsettings.json", StringComparison.Ordinal));

        var offenders = settings
            .Where(file =>
            {
                using var document = RepositoryLayout.LoadJson(file);

                return document.RootElement.ValueKind == JsonValueKind.Object
                    && document.RootElement.EnumerateObject().Any(property =>
                        property.Name.Equals("ConnectionStrings", StringComparison.OrdinalIgnoreCase));
            })
            .Select(RepositoryLayout.RelativePath)
            .ToList();

        Assert.True(offenders.Count == 0, "The Host reads its connection string from the environment only (AC3): " + string.Join(", ", offenders));
    }

    /// <summary>
    /// 1.10-ARCH-006 - every <c>ConnectionStrings__*</c> environment entry in the Bicep is a
    /// <c>secretRef</c>, never a <c>value</c>, and each secret it names is a Key Vault reference
    /// (<c>keyVaultUrl</c> with an <c>identity</c>).
    /// </summary>
    /// <remarks>
    /// Plant 2 (story Task 11): <c>ConnectionStrings__yello</c> as a plain <c>value</c> in
    /// <c>main.bicep</c>.
    /// </remarks>
    [Fact]
    public void Every_connection_string_reaches_the_container_as_a_key_vault_secret_reference()
    {
        var bicep = BicepText();

        var entries = ConnectionStringEnvEntry.Matches(bicep).ToList();

        // Anchor: the Host's own key, which the build stamps as YelloDatabaseResourceName.
        Assert.Contains(entries, entry => entry.Groups["key"].Value.Equals("ConnectionStrings__yello", StringComparison.Ordinal));

        var plain = entries
            .Where(entry => !entry.Groups["kind"].Value.Equals("secretRef", StringComparison.Ordinal))
            .Select(entry => entry.Groups["key"].Value)
            .ToList();

        Assert.True(plain.Count == 0, "A connection string reaches the container as a plain value, not a Key Vault reference: " + string.Join(", ", plain));

        var notFromKeyVault = entries
            .Select(entry => entry.Groups["secret"].Value)
            .Where(secret => !Regex.IsMatch(
                bicep,
                $@"name:\s*'{Regex.Escape(secret)}'[^}}]*keyVaultUrl:[^}}]*identity:",
                RegexOptions.Singleline,
                TimeSpan.FromSeconds(1)))
            .ToList();

        Assert.True(notFromKeyVault.Count == 0, "A secret the container reads is not a Key Vault reference through a managed identity: " + string.Join(", ", notFromKeyVault));
    }

    /// <summary>
    /// The files the credential scan reads: every appsettings and launch-settings file, the AppHost, and
    /// everything under <c>infra/</c> and <c>.github/</c>.
    /// </summary>
    private static List<FileInfo> ScannedFiles()
    {
        var files = RepositoryLayout.EnumerateSourceFiles("appsettings*.json")
            .Concat(RepositoryLayout.EnumerateSourceFiles("launchSettings.json"))
            .Concat(FilesUnder("Yello.AppHost").Where(file => file.Extension is ".cs" or ".json"))
            .Concat(FilesUnder("infra"))
            .Concat(FilesUnder(".github"));

        return [.. files.DistinctBy(file => file.FullName).OrderBy(file => file.FullName, StringComparer.Ordinal)];
    }

    private static IEnumerable<FileInfo> FilesUnder(string relative)
    {
        var directory = new DirectoryInfo(Path.Combine(RepositoryLayout.Root.FullName, relative));

        return directory.Exists
            ? directory.EnumerateFiles("*", SearchOption.AllDirectories)
                .Where(file => !Array.Exists(RepositoryLayout.RelativePath(file).Split('/'), segment => segment is "bin" or "obj"))
            : [];
    }

    private static string BicepText()
    {
        var infra = new DirectoryInfo(Path.Combine(RepositoryLayout.Root.FullName, "infra"));

        Assert.True(infra.Exists, "infra/ does not exist (story 1.10 Task 6).");

        var files = infra.EnumerateFiles("*.bicep", SearchOption.AllDirectories).ToList();

        Assert.Contains(files, file => file.Name.Equals("main.bicep", StringComparison.Ordinal));

        return string.Join('\n', files.Select(file => File.ReadAllText(file.FullName)));
    }

    private static IEnumerable<Regex> CredentialShapes() =>
    [
        PasswordAssignment,
        AccountKeyAssignment,
        ClientSecret,
        PemBlock,
        SecretNamedBase64,
    ];

    [GeneratedRegex(@"\b(?:Password|Pwd)\s*=\s*[^;""'\s]", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex PasswordAssignment { get; }

    [GeneratedRegex(@"\b(?:AccountKey|SharedAccessKey)\s*=", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex AccountKeyAssignment { get; }

    [GeneratedRegex(@"client_secret", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex ClientSecret { get; }

    [GeneratedRegex(@"-----BEGIN", RegexOptions.None, 1000)]
    private static partial Regex PemBlock { get; }

    [GeneratedRegex(@"[""']?\w*(?:secret|password|key|token)\w*[""']?\s*[:=]\s*[""'][A-Za-z0-9+/]{32,}={0,2}[""']", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex SecretNamedBase64 { get; }

    /// <summary>
    /// A container env entry naming a connection string: its key, whether it is a <c>value</c> or a
    /// <c>secretRef</c>, and (for a reference) the secret it names.
    /// </summary>
    [GeneratedRegex(@"name:\s*'(?<key>ConnectionStrings__[^']+)'[\s,]*(?<kind>value|secretRef)\s*:\s*'?(?<secret>[^'\s}]*)", RegexOptions.None, 1000)]
    private static partial Regex ConnectionStringEnvEntry { get; }
}
