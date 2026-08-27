using System.Text.Json;
using System.Xml.Linq;

namespace Yello.Tests.Architecture;

/// <summary>
/// Locates the repository on disk and reads its build files as text.
/// </summary>
/// <remarks>
/// <para>
/// Gate A reads <c>.csproj</c>, <c>Directory.Packages.props</c> and <c>global.json</c> as
/// files rather than as compiled metadata, and that is the whole point of it. ArchUnitNET
/// analyses compiled bytecode through Mono.Cecil, and Roslyn emits no <c>AssemblyRef</c>
/// for a referenced assembly whose types are never used. A ring-violating
/// <c>&lt;ProjectReference&gt;</c> that nobody has written code against yet is therefore
/// invisible to Gate B. AC2 requires the build to fail when <i>a project reference is
/// added</i> - that is a project-file fact, and only a project-file gate sees it.
/// </para>
/// <para>
/// <b>What this class reads is what a build file DECLARES, not what MSBuild evaluates.</b>
/// The distinction matters and is asserted rather than assumed: <see cref="MsBuildImportFiles"/>
/// exists so a gate can require that the repository has exactly one of each import file, which
/// is what makes "the root Directory.Build.props says X" equivalent to "every project gets X".
/// Without that, a nested <c>tests/Directory.Build.props</c> would shadow the root file
/// entirely - MSBuild imports only the nearest - while a gate reading the root file stayed
/// green.
/// </para>
/// </remarks>
// Every helper below takes FileInfo where FileSystemInfo would compile: each reads only Name
// or FullName, both of which the base type carries. An IDE will suggest widening them - S3242,
// which the coding standard reports at `suggestion` and which therefore no longer fails the
// build. Do not take the suggestion. FileSystemInfo means "a file or a directory", and a
// DirectoryInfo reaching ProjectName or DeclaredProjectReferences does not fail - it returns a
// plausible wrong answer, silently, inside the gate the whole solution's structure is asserted
// by. The parameter type is carrying an invariant here, which is the one job S3242 does not
// weigh. This note is the only thing standing between that invariant and a tidy-up commit.
internal static class RepositoryLayout
{
    /// <summary>
    /// Directories that are never part of the source tree. Enumerating them is not merely
    /// wasted work: a vendored <c>.csproj</c> arriving under <c>.claude</c> with a skill
    /// update, or a restored package under <c>artifacts</c>, would present to the inventory
    /// gate as an unexplained production project and fail a build nobody had changed.
    /// </summary>
    private static readonly string[] ExcludedDirectories =
        ["bin", "obj", "artifacts", "node_modules", ".git", ".vs", ".claude", "_bmad", "_bmad-output"];

    /// <summary>
    /// The repository root, found by walking up to the directory holding Yello.slnx.
    /// </summary>
    public static DirectoryInfo Root { get; } = FindRoot();

    /// <summary>
    /// The XML-format solution file. See ProjectFileGateTests for why the extension matters.
    /// </summary>
    public static FileInfo SolutionFile { get; } = new(Path.Combine(Root.FullName, "Yello.slnx"));

    public static FileInfo DirectoryPackagesProps { get; } =
        new(Path.Combine(Root.FullName, "Directory.Packages.props"));

    public static FileInfo DirectoryBuildProps { get; } =
        new(Path.Combine(Root.FullName, "Directory.Build.props"));

    public static FileInfo GlobalJson { get; } = new(Path.Combine(Root.FullName, "global.json"));

    public static FileInfo DotnetToolsManifest { get; } =
        new(Path.Combine(Root.FullName, ".config", "dotnet-tools.json"));

    /// <summary>
    /// Every project file in the repository, excluding build output and non-source trees.
    /// Ordered so failure messages are stable between runs.
    /// </summary>
    public static IReadOnlyList<FileInfo> AllProjectFiles { get; } =
        EnumerateSourceFiles("*.csproj")
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Every MSBuild file that applies to projects implicitly by directory position, anywhere
    /// in the source tree. A gate asserts there is exactly one of each: MSBuild imports only
    /// the nearest <c>Directory.Build.props</c>, so a second one is not an addition to the
    /// root file but a replacement of it.
    /// </summary>
    public static IReadOnlyList<FileInfo> MsBuildImportFiles { get; } =
    [
        .. EnumerateSourceFiles("Directory.Build.props")
            .Concat(EnumerateSourceFiles("Directory.Build.targets"))
            .Concat(EnumerateSourceFiles("Directory.Packages.props"))
            .OrderBy(f => RelativePath(f), StringComparer.Ordinal),
    ];

    /// <summary>
    /// The project name, which by convention equals the file name without its extension.
    /// </summary>
    public static string ProjectName(FileInfo project) =>
        Path.GetFileNameWithoutExtension(project.Name);

    /// <summary>
    /// Parses an XML build file, turning a malformed or unreadable one into a failure that
    /// names the file.
    /// </summary>
    /// <remarks>
    /// Unguarded, <c>XDocument.Load</c> aborts the enclosing iteration on the first bad file,
    /// so partial coverage becomes indistinguishable from full coverage - the gate reports
    /// green over the projects it happened to reach before throwing, or reports an exception
    /// that names no file. Both are worse than a named failure.
    /// </remarks>
    public static XDocument LoadXml(FileInfo file)
    {
        try
        {
            return XDocument.Load(file.FullName);
        }
        catch (Exception exception) when (exception is System.Xml.XmlException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"Could not parse '{RelativePath(file)}' as XML: {exception.Message}. Gate A " +
                "reads the repository's build files from disk, so an unreadable one is a gate " +
                "that cannot answer rather than a gate that passes.",
                exception);
        }
    }

    /// <summary>
    /// The names of the projects a given project file declares a
    /// <c>&lt;ProjectReference&gt;</c> to. Duplicates are preserved: a doubly-declared
    /// reference is a defect the caller should be able to see, and de-duplicating here would
    /// hide it.
    /// </summary>
    public static IReadOnlyList<string> DeclaredProjectReferences(FileInfo project) =>
        ItemIncludes(project, "ProjectReference")
            .Select(v => Path.GetFileNameWithoutExtension(v.Replace('\\', '/')))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The package ids a given project file declares a <c>&lt;PackageReference&gt;</c> or a
    /// <c>&lt;GlobalPackageReference&gt;</c> to.
    /// </summary>
    /// <remarks>
    /// <c>GlobalPackageReference</c> is included deliberately. It is already the established
    /// idiom in this repository (the coding standard is declared that way), and it applies a
    /// package to <i>every</i> project in the solution - so a ban that read only
    /// <c>PackageReference</c> would be bypassed by the one form that has the widest reach.
    /// </remarks>
    public static IReadOnlyList<string> DeclaredPackageReferences(FileInfo project) =>
        ItemIncludes(project, "PackageReference")
            .Concat(ItemIncludes(project, "GlobalPackageReference"))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The <c>Include</c> values of a given item type, split on semicolons and trimmed.
    /// </summary>
    /// <remarks>
    /// MSBuild accepts <c>Include="..\A\A.csproj;..\B\B.csproj"</c> as two items. Reading the
    /// raw attribute yields one string, and any per-item transform applied to it then
    /// describes only the last entry - which is a silent bypass of every gate downstream:
    /// <c>Include="..\Yello.Infrastructure\...csproj;..\Yello.Domain\Yello.Domain.csproj"</c>
    /// would present as a lone permitted edge to Domain while genuinely referencing
    /// Infrastructure.
    /// </remarks>
    public static IEnumerable<string> ItemIncludes(FileInfo project, string itemName) =>
        LoadXml(project)
            .Descendants()
            .Where(e => e.Name.LocalName.Equals(itemName, StringComparison.Ordinal))
            .Select(e => e.Attribute("Include")?.Value ?? e.Attribute("Update")?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .SelectMany(v => v!.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>
    /// Parses a JSON build file, turning a malformed or unreadable one into a failure that
    /// names the file. The same contract as <see cref="LoadXml"/>, for the same reason.
    /// </summary>
    /// <remarks>
    /// Unguarded, <c>JsonDocument.Parse</c> throws a <c>JsonException</c> naming a line and a
    /// column but neither the file nor the remedy - which makes the carefully-worded failure
    /// message of whichever assertion called it unreachable for every malformation. A gate that
    /// cannot read its input has to say which input.
    /// </remarks>
    public static JsonDocument LoadJson(FileInfo file)
    {
        try
        {
            return JsonDocument.Parse(File.ReadAllText(file.FullName));
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"Could not parse '{RelativePath(file)}' as JSON: {exception.Message}. Gate A " +
                "reads the repository's build files from disk, so an unreadable one is a gate " +
                "that cannot answer rather than a gate that passes.",
                exception);
        }
    }

    /// <summary>
    /// A JSON property's value as text, whether it was written as a string or as a bare literal.
    /// </summary>
    /// <remarks>
    /// <c>"version": 13.4</c> is legal JSON and throws <c>InvalidOperationException</c> from
    /// <c>GetString()</c> - an exception that reads like a bug in the gate rather than a
    /// malformed pin.
    /// </remarks>
    public static string? JsonValueText(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.GetRawText(),
        };

    /// <summary>
    /// True when neither the element nor any ancestor carries a <c>Condition</c>.
    /// </summary>
    /// <remarks>
    /// No gate here evaluates MSBuild conditions, so a conditional declaration is neither
    /// safely "declared" nor safely "absent": treating it as declared lets a condition that
    /// never fires satisfy a pin, and treating it as absent lets a condition that does fire
    /// override one unseen. Gates therefore read <i>unconditional</i> declarations to learn
    /// what the build takes, and report conditional ones separately as their own problem.
    /// Lives here rather than in one test class because both Gate A files need the same rule -
    /// having it in only one of them is how the pin gate came to accept a conditional switch.
    /// </remarks>
    public static bool IsUnconditional(XElement element)
    {
        for (var current = element; current is not null; current = current.Parent)
        {
            if (current.Attribute("Condition") is not null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// <c>Include</c> values of the given item types that MSBuild expands and this class
    /// cannot, because they contain a property reference.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every ban in Gate A compares the literal attribute text, so
    /// <c>&lt;PackageReference Include="$(Orm)" /&gt;</c> restores
    /// <c>Microsoft.EntityFrameworkCore</c> while the gate matches the string <c>$(Orm)</c>
    /// against the banned list and finds nothing. The same indirection defeats the VSTest ban,
    /// the row-level-security provider ban, the per-ring package ban, and the cross-ring
    /// <c>Compile</c> check - where <c>Path.Combine</c> treats <c>$(Shared)</c> as an ordinary
    /// directory name and so never climbs out of the project directory.
    /// </para>
    /// <para>
    /// Reported, not expanded. Expanding means evaluating each project through MSBuild, which
    /// is a materially larger gate than this file is; refusing to gate what cannot be read
    /// keeps the guarantee honest, and nothing in this repository needs the indirection today.
    /// If a later story has a real use for it, that is the point to build the evaluating gate -
    /// not the point to widen the blind spot.
    /// </para>
    /// </remarks>
    public static IEnumerable<string> UnresolvableIncludes(FileInfo file, params string[] itemNames) =>
        from itemName in itemNames
        from include in ItemIncludes(file, itemName)
        where include.Contains("$(", StringComparison.Ordinal)
        select $"<{itemName} Include=\"{include}\" />";

    /// <summary>
    /// The absolute path each <c>&lt;ProjectReference&gt;</c> resolves to, with the
    /// <c>Include</c> it came from.
    /// </summary>
    /// <remarks>
    /// <see cref="DeclaredProjectReferences"/> reduces an <c>Include</c> to its file name, so
    /// <c>..\..\vendored\Yello.Domain\Yello.Domain.csproj</c> reads as the permitted
    /// <c>Yello.Domain</c> edge. That reduction is right for comparing an edge against the
    /// table, which is keyed by name, and wrong for deciding whether the edge points at a
    /// project in this repository at all.
    /// </remarks>
    public static IEnumerable<(string Include, string ResolvedPath)> ResolvedProjectReferences(FileInfo project) =>
        project.Directory is null
            ? []
            : ItemIncludes(project, "ProjectReference")
                .Select(v => (v, Path.GetFullPath(Path.Combine(project.Directory.FullName, v.Replace('\\', '/')))));

    /// <summary>
    /// The <c>Project</c> attribute of every explicit <c>&lt;Import&gt;</c> in a build file.
    /// </summary>
    /// <remarks>
    /// <see cref="MsBuildImportFiles"/> covers the files MSBuild imports by directory
    /// position, and a gate asserts there is exactly one of each. An explicit
    /// <c>&lt;Import&gt;</c> is the ordinary way to share MSBuild logic and is subject to
    /// neither: it can carry a reference, a <c>GlobalPackageReference</c> or a framework
    /// property into a project from a file no gate reads, which makes every "declared fact"
    /// this class returns something less than the facts the build actually uses.
    /// </remarks>
    public static IEnumerable<string> DeclaredImports(FileInfo file) =>
        LoadXml(file)
            .Descendants()
            .Where(e => e.Name.LocalName.Equals("Import", StringComparison.Ordinal))
            .Select(e => e.Attribute("Project")?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!);

    /// <summary>
    /// The absolute path of every project the <c>.slnx</c> declares.
    /// </summary>
    /// <remarks>
    /// The <c>.slnx</c> schema uses a <c>Path</c> attribute rather than MSBuild's
    /// <c>Include</c>, and nests entries inside <c>&lt;Folder&gt;</c> elements, so it is read
    /// here rather than through <see cref="ItemIncludes"/>. Parsed as XML deliberately:
    /// a substring search over the raw text matches an entry that has been commented out,
    /// which is a release-gating suite silently dropped from the build.
    /// </remarks>
    public static IEnumerable<string> SolutionProjectPaths() =>
        LoadXml(SolutionFile)
            .Descendants()
            .Where(e => e.Name.LocalName.Equals("Project", StringComparison.Ordinal))
            .Select(e => e.Attribute("Path")?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => Path.GetFullPath(Path.Combine(Root.FullName, v!.Replace('\\', '/'))));

    /// <summary>
    /// True when the project lives under the <c>tests/</c> directory.
    /// </summary>
    /// <remarks>
    /// Case-insensitive. On Windows a rename of <c>tests</c> to <c>Tests</c> changes nothing
    /// about where the files are, and an Ordinal comparison would quietly report every suite
    /// as production code - which makes the test-project checks that read this vacuous.
    /// </remarks>
    public static bool IsUnderTestsDirectory(FileInfo project) =>
        RelativePath(project).StartsWith("tests/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The number of directory levels between the repository root and the given file.
    /// A project at <c>Yello.Domain/Yello.Domain.csproj</c> is at depth 1.
    /// </summary>
    public static int DepthBelowRoot(FileInfo file) =>
        RelativePath(file).Count(c => c == '/');

    /// <summary>
    /// The repository-relative path, for failure messages a human has to act on.
    /// </summary>
    public static string RelativePath(FileInfo file) =>
        Path.GetRelativePath(Root.FullName, file.FullName).Replace('\\', '/');

    /// <summary>
    /// Every <c>.cs</c> file belonging to a project, by directory containment.
    /// </summary>
    public static IReadOnlyList<FileInfo> SourceFilesOf(FileInfo project) =>
        project.Directory is null
            ? []
            : [.. project.Directory
                .EnumerateFiles("*.cs", SearchOption.AllDirectories)
                .Where(f => !IsExcluded(f))
                .OrderBy(f => f.FullName, StringComparer.Ordinal)];

    /// <summary>
    /// Every file in the source tree matching a pattern, with build output and the non-source
    /// trees in <see cref="ExcludedDirectories"/> left out.
    /// </summary>
    /// <remarks>
    /// Public because a gate doing its own <c>EnumerateFiles</c> re-introduces the hazard the
    /// exclusion list exists to prevent: a vendored file arriving under <c>.claude</c> with a
    /// skill update fails a build nobody had changed. Route every tree walk through here.
    /// </remarks>
    public static IEnumerable<FileInfo> EnumerateSourceFiles(string pattern) =>
        Root.EnumerateFiles(pattern, SearchOption.AllDirectories).Where(f => !IsExcluded(f));

    private static bool IsExcluded(FileInfo file)
    {
        var segments = Path.GetRelativePath(Root.FullName, file.FullName)
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        // The file name itself is the last segment and is never a directory.
        return segments
            .Take(segments.Length - 1)
            .Any(segment => ExcludedDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

    private static DirectoryInfo FindRoot()
    {
        var candidate = new DirectoryInfo(AppContext.BaseDirectory);

        while (candidate is not null)
        {
            if (candidate.EnumerateFiles("Yello.slnx").Any())
            {
                return candidate;
            }

            candidate = candidate.Parent;
        }

        // AppContext.BaseDirectory is CWD-independent, so an IDE launch or a `cd tests` does
        // not reach here. What does: a custom ArtifactsPath, a published test project, or CI
        // downloading only the test artifact - cases where the binary genuinely sits outside
        // the repository. Throwing from a static initialiser surfaces as
        // TypeInitializationException across every test in the suite, which names neither the
        // cause nor the remedy, so the environment variable below is the documented way to
        // run the gate from outside the tree it asserts.
        var configured = Environment.GetEnvironmentVariable("YELLO_REPOSITORY_ROOT");

        if (!string.IsNullOrWhiteSpace(configured) && Directory.Exists(configured))
        {
            return new DirectoryInfo(configured);
        }

        throw new InvalidOperationException(
            "Could not locate the repository root: no Yello.slnx found walking up from " +
            $"'{AppContext.BaseDirectory}', and YELLO_REPOSITORY_ROOT is unset or does not " +
            "point at a directory. Gate A reads the repository's build files from disk, so it " +
            "cannot run without knowing where the repository is.");
    }
}
