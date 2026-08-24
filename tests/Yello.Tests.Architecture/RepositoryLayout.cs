using System.Xml.Linq;

namespace Yello.Tests.Architecture;

/// <summary>
/// Locates the repository on disk and reads its build files as text.
/// </summary>
/// <remarks>
/// Gate A reads <c>.csproj</c>, <c>Directory.Packages.props</c> and <c>global.json</c> as
/// files rather than as compiled metadata, and that is the whole point of it. ArchUnitNET
/// analyses compiled bytecode through Mono.Cecil, and Roslyn emits no <c>AssemblyRef</c>
/// for a referenced assembly whose types are never used. A ring-violating
/// <c>&lt;ProjectReference&gt;</c> that nobody has written code against yet is therefore
/// invisible to Gate B. AC2 requires the build to fail when <i>a project reference is
/// added</i> - that is a project-file fact, and only a project-file gate sees it.
/// </remarks>
internal static class RepositoryLayout
{
    /// <summary>The repository root, found by walking up to the directory holding Yello.sln.</summary>
    public static DirectoryInfo Root { get; } = FindRoot();

    /// <summary>The classic-format solution file. See below for why the extension matters.</summary>
    public static FileInfo SolutionFile { get; } = new(Path.Combine(Root.FullName, "Yello.sln"));

    public static FileInfo DirectoryPackagesProps { get; } =
        new(Path.Combine(Root.FullName, "Directory.Packages.props"));

    public static FileInfo DirectoryBuildProps { get; } =
        new(Path.Combine(Root.FullName, "Directory.Build.props"));

    public static FileInfo GlobalJson { get; } = new(Path.Combine(Root.FullName, "global.json"));

    /// <summary>
    /// Every project file in the repository, excluding build output. Ordered so failure
    /// messages are stable between runs.
    /// </summary>
    public static IReadOnlyList<FileInfo> AllProjectFiles { get; } = Root
        .EnumerateFiles("*.csproj", SearchOption.AllDirectories)
        .Where(f => !IsBuildOutput(f))
        .OrderBy(f => f.Name, StringComparer.Ordinal)
        .ToList();

    /// <summary>The project name, which by convention equals the file name without its extension.</summary>
    public static string ProjectName(FileInfo project) =>
        Path.GetFileNameWithoutExtension(project.Name);

    /// <summary>
    /// The names of the projects a given project file declares a
    /// <c>&lt;ProjectReference&gt;</c> to.
    /// </summary>
    public static IReadOnlyList<string> DeclaredProjectReferences(FileInfo project) =>
        XDocument.Load(project.FullName)
            .Descendants()
            .Where(e => e.Name.LocalName.Equals("ProjectReference", StringComparison.Ordinal))
            .Select(e => e.Attribute("Include")?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => Path.GetFileNameWithoutExtension(v!.Replace('\\', '/')))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The package ids a given project file declares a <c>&lt;PackageReference&gt;</c> to.
    /// </summary>
    public static IReadOnlyList<string> DeclaredPackageReferences(FileInfo project) =>
        XDocument.Load(project.FullName)
            .Descendants()
            .Where(e => e.Name.LocalName.Equals("PackageReference", StringComparison.Ordinal))
            .Select(e => e.Attribute("Include")?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

    /// <summary>True when the project lives under the <c>tests/</c> directory.</summary>
    public static bool IsUnderTestsDirectory(FileInfo project)
    {
        var relative = Path.GetRelativePath(Root.FullName, project.FullName).Replace('\\', '/');
        return relative.StartsWith("tests/", StringComparison.Ordinal);
    }

    /// <summary>The repository-relative path, for failure messages a human has to act on.</summary>
    public static string RelativePath(FileInfo file) =>
        Path.GetRelativePath(Root.FullName, file.FullName).Replace('\\', '/');

    private static bool IsBuildOutput(FileInfo file)
    {
        var relative = Path.GetRelativePath(Root.FullName, file.FullName).Replace('\\', '/');
        return relative.Contains("/bin/", StringComparison.Ordinal)
            || relative.Contains("/obj/", StringComparison.Ordinal)
            || relative.StartsWith("bin/", StringComparison.Ordinal)
            || relative.StartsWith("obj/", StringComparison.Ordinal);
    }

    private static DirectoryInfo FindRoot()
    {
        var candidate = new DirectoryInfo(AppContext.BaseDirectory);

        while (candidate is not null)
        {
            if (candidate.EnumerateFiles("Yello.sln").Any())
            {
                return candidate;
            }

            candidate = candidate.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root: no Yello.sln found walking up from " +
            $"'{AppContext.BaseDirectory}'. Gate A reads the repository's build files from " +
            $"disk, so it cannot run without knowing where the repository is.");
    }
}
