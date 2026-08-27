using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace Yello.Tests.Architecture;

/// <summary>
/// What MSBuild actually evaluates for each project, as opposed to what its files declare.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> Three review passes of story 1.1 found the same defect one level
/// along each time, and the root cause was one sentence: the gates read <i>declared</i> MSBuild
/// XML as text and reasoned about it as though it were <i>evaluated</i> build state. MSBuild
/// offers many indirections between the two - <c>$(property)</c>, <c>@(item)</c>,
/// <c>%(metadata)</c>, <c>%XX</c> escapes, <c>Condition</c> overrides, values assembled from
/// other properties, <c>Directory.Build.targets</c> overriding <c>Directory.Packages.props</c>,
/// and <c>Sdk=</c> imports - and each is a separate bypass of a text comparison. Each pass closed
/// one spelling and left the siblings, which was demonstrated the hard way: EF Core was planted
/// inside <c>Yello.Domain</c>, the ring that may reference nothing, with the whole suite green,
/// purely by writing <c>@(Orm)</c> where the gate looked for <c>$(</c>.
/// </para>
/// <para>
/// Asking MSBuild ends that class rather than its current spelling, and is immune to
/// indirections nobody has thought of yet. <c>Compile</c> items come back as resolved
/// <c>FullPath</c> values, so escaped and property-built paths are simply correct; a
/// <c>GlobalPackageReference</c> arrives as an ordinary evaluated <c>PackageReference</c>, so
/// the route that bypassed three bans is visible without a special case.
/// </para>
/// <para>
/// <b>Hybrid, deliberately.</b> This is used for the five questions where a bypass was actually
/// demonstrated: package references, <c>Compile</c> items, the framework pin, the central
/// package management switches, and the test-platform arguments. Everything else keeps reading
/// declared XML, because a question about what a <i>file states</i> - "the pin is declared
/// exactly once, in this one file" - is not answerable from evaluated state at all, and paying
/// this cost on assertions with no demonstrated bypass buys nothing. The architecture suite runs
/// first in CI precisely because it finishes in seconds; evaluation adds about two of them for
/// the whole solution, run in parallel.
/// </para>
/// <para>
/// <b>A gate that cannot answer is not a gate that passes.</b> Every failure here throws and
/// names the project, the exit code and the remedy. That is the <see cref="SolutionAssemblies"/>
/// lesson: silently narrowing scope was the highest-severity finding of the second pass, and an
/// MSBuild invocation that fails must not read as an absence of violations.
/// </para>
/// </remarks>
internal static class MsBuildEvaluation
{
    /// <summary>
    /// Item types requested for every project, in one invocation.
    /// </summary>
    /// <remarks>
    /// <c>Reference</c> is included because a raw assembly reference crosses a ring exactly as a
    /// project reference does. Verified on this stack: a web project evaluates <b>zero</b>
    /// <c>Reference</c> items, so the framework's implicit references do not show up here and the
    /// ban can be absolute.
    /// </remarks>
    private static readonly string[] ItemTypes =
        ["PackageReference", "ProjectReference", "Compile", "Reference"];

    /// <summary>
    /// Properties requested for every project, in the same invocation.
    /// </summary>
    private static readonly string[] PropertyNames =
    [
        "TargetFramework",
        "TargetFrameworks",
        "RuntimeFrameworkVersion",
        "ManagePackageVersionsCentrally",
        "CentralPackageTransitivePinningEnabled",
        "TestingPlatformCommandLineArguments",
        "IsTestProject",
        "OutputType",
    ];

    private static readonly Lazy<IReadOnlyDictionary<string, ProjectEvaluation>> Cache =
        new(EvaluateAll, isThreadSafe: true);

    /// <summary>
    /// The evaluated state of one project.
    /// </summary>
    public static ProjectEvaluation Of(FileInfo project)
    {
        if (Cache.Value.TryGetValue(project.FullName, out var evaluation))
        {
            return evaluation;
        }

        throw new InvalidOperationException(
            $"'{RepositoryLayout.RelativePath(project)}' was not evaluated. This dictionary is " +
            "built from RepositoryLayout.AllProjectFiles, so a miss means the two disagree about " +
            "which projects exist - which is itself the defect, not a lookup problem.");
    }

    /// <summary>
    /// An evaluated property's value, or the empty string when MSBuild resolved it to nothing.
    /// </summary>
    public static string Property(FileInfo project, string name) =>
        Of(project).Properties.TryGetValue(name, out var value) ? value : string.Empty;

    /// <summary>
    /// The evaluated items of a type. Empty when the project declares none.
    /// </summary>
    public static IReadOnlyList<EvaluatedItem> Items(FileInfo project, string itemType) =>
        Of(project).Items.TryGetValue(itemType, out var items) ? items : [];

    /// <summary>
    /// Every package id the project actually restores against.
    /// </summary>
    /// <remarks>
    /// This is what the package bans read, and the reason they now hold. It resolves
    /// <c>$(property)</c> and <c>@(item)</c> indirection, and a
    /// <c>GlobalPackageReference</c> - which reaches every project in the solution and bypassed
    /// three separate bans - arrives here as an ordinary <c>PackageReference</c> with no special
    /// case needed. The coding standard therefore appears for every project, correctly: it IS
    /// referenced by every project.
    /// </remarks>
    public static IReadOnlyList<string> PackageIds(FileInfo project) =>
    [
        .. Items(project, "PackageReference")
            .Select(i => i.Identity)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase),
    ];

    private static IReadOnlyDictionary<string, ProjectEvaluation> EvaluateAll()
    {
        var results = new ConcurrentDictionary<string, ProjectEvaluation>(StringComparer.OrdinalIgnoreCase);
        var failures = new ConcurrentBag<string>();

        // In parallel: fourteen sequential invocations take about ten seconds, concurrently about
        // two. This is the suite CI runs first because it is fast, so the difference matters.
        Parallel.ForEach(RepositoryLayout.AllProjectFiles, project =>
        {
            try
            {
                results[project.FullName] = Evaluate(project);
            }
            catch (Exception exception) when (exception is InvalidOperationException or JsonException)
            {
                failures.Add(exception.Message);
            }
        });

        if (!failures.IsEmpty)
        {
            throw new InvalidOperationException(
                "MSBuild could not be asked what it evaluates, so these gates cannot answer:" +
                $"{Environment.NewLine}" +
                string.Join(Environment.NewLine, failures.Order(StringComparer.Ordinal).Select(f => $"  - {f}")) +
                $"{Environment.NewLine}{Environment.NewLine}" +
                "This is a failure, not a pass. Run `dotnet restore` first if the projects have " +
                "never been restored - -getItem and -getProperty evaluate the project, which " +
                "needs its assets file.");
        }

        return results;
    }

    private static ProjectEvaluation Evaluate(FileInfo project)
    {
        var arguments = new List<string> { "msbuild", project.FullName };
        arguments.AddRange(ItemTypes.Select(i => $"-getItem:{i}"));
        arguments.AddRange(PropertyNames.Select(p => $"-getProperty:{p}"));

        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = RepositoryLayout.Root.FullName,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                $"could not start `dotnet msbuild` for '{RepositoryLayout.RelativePath(project)}'");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"`dotnet msbuild` exited {process.ExitCode} for " +
                $"'{RepositoryLayout.RelativePath(project)}': {FirstLine(stderr, stdout)}");
        }

        var trimmed = stdout.TrimStart();

        if (!trimmed.StartsWith('{'))
        {
            throw new InvalidOperationException(
                "`dotnet msbuild` did not return JSON for " +
                $"'{RepositoryLayout.RelativePath(project)}': {FirstLine(stdout, stderr)}");
        }

        using var document = JsonDocument.Parse(trimmed);

        return new ProjectEvaluation(ReadProperties(document.RootElement), ReadItems(document.RootElement));
    }

    private static IReadOnlyDictionary<string, string> ReadProperties(JsonElement root)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!root.TryGetProperty("Properties", out var element) || element.ValueKind != JsonValueKind.Object)
        {
            return properties;
        }

        foreach (var property in element.EnumerateObject())
        {
            properties[property.Name] = RepositoryLayout.JsonValueText(property.Value) ?? string.Empty;
        }

        return properties;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<EvaluatedItem>> ReadItems(JsonElement root)
    {
        var items = new Dictionary<string, IReadOnlyList<EvaluatedItem>>(StringComparer.Ordinal);

        if (!root.TryGetProperty("Items", out var element) || element.ValueKind != JsonValueKind.Object)
        {
            return items;
        }

        foreach (var itemType in element.EnumerateObject())
        {
            if (itemType.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            items[itemType.Name] =
            [
                .. itemType.Value.EnumerateArray()
                    .Select(i => new EvaluatedItem(
                        Text(i, "Identity"),
                        Text(i, "FullPath")))
                    .Where(i => i.Identity.Length > 0),
            ];
        }

        return items;
    }

    private static string Text(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value)
            ? RepositoryLayout.JsonValueText(value) ?? string.Empty
            : string.Empty;

    private static string FirstLine(params string[] candidates) =>
        candidates
            .SelectMany(c => c.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .FirstOrDefault(l => l.Length > 0)
        ?? "(no output)";
}

/// <summary>
/// One project's evaluated properties and items.
/// </summary>
internal sealed record ProjectEvaluation(
    IReadOnlyDictionary<string, string> Properties,
    IReadOnlyDictionary<string, IReadOnlyList<EvaluatedItem>> Items);

/// <summary>
/// An evaluated MSBuild item. <c>FullPath</c> is resolved by MSBuild, which is what makes
/// escaped and property-built paths correct here rather than merely readable.
/// </summary>
internal sealed record EvaluatedItem(string Identity, string FullPath);
