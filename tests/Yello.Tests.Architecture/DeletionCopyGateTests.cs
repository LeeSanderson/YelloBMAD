using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.5's copy gates, beside <see cref="LocalisationGateTests"/>: the word "archive" appears nowhere in the product
/// (AC5, UX-DR37), and the deletion copy the story fixes is externalised once each, with a translator's comment (AC1,
/// AC2, AC4, AC11, AC12; story Task 11). Written by the story 2.5 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why "archive" is gated rather than reviewed.</b> Yello has no archive (PRD §6.2: no trash, no restore), and the
/// products users arrive from use the word for a recoverable hide - borrowing it would promise a safety net that does not
/// exist. The gate reads every source a user-visible string can come from: the client's components, code, styles, pages
/// and resources (values <b>and</b> comments, since a comment is a translator's brief), the contracts (problem types and
/// their titles) and the Host (problem details it writes). It matches the word's stem at a word start, case-insensitively
/// (<c>archive</c>, <c>Archived</c>, <c>ARCHIVING</c>), and not inside another word ("search", "March").
/// </para>
/// <para>
/// <b>Green today, and a standing gate</b>: nothing in the product says "archive" (story "What exists today"). Its oracle
/// is a permanent test - the matcher must report a planted "Archive" in a resource value, a comment and markup, and must
/// not report words that merely contain the letters - so a later edit cannot disarm it quietly.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "UX-DR37")]
[Trait("Requirement", "FR-17")]
public sealed class DeletionCopyGateTests
{
    private static readonly Regex ArchiveWord = new(@"\barchiv", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));

    private static readonly string[] ClientExtensions = [".razor", ".cs", ".resx", ".html", ".css", ".json"];

    private static readonly FileInfo ClientCopy =
        new(Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client", "Localisation", "ClientCopy.resx"));

    /// <summary>
    /// The deletion copy story Task 11 fixes, each a whole resource value: sentence case, no exclamation, no apology,
    /// never "Are you sure". <c>"This cannot be undone."</c> is one value reused by every confirm.
    /// </summary>
    private static readonly string[] DeletionCopy =
    [
        "Delete Task",
        "Delete {0}?",
        "This cannot be undone.",
        "Delete Project",
        "It holds no Tasks.",
        "It holds 1 Task.",
        "It holds {0} Tasks.",
        "Task deleted.",
        "Project deleted.",
    ];

    /// <summary>
    /// AC5, UX-DR37 - no source a user-visible string can come from uses the word "archive" in any form. Anchored: the
    /// scan reads the client's resource file and at least one component, contract and Host source.
    /// </summary>
    [Fact]
    public void No_copy_uses_the_word_archive()
    {
        var sources = ProductSources().ToList();

        Assert.Contains(sources, source => source.Path.EndsWith("ClientCopy.resx", StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Path.EndsWith(".razor", StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Path.EndsWith("ProblemTypes.cs", StringComparison.Ordinal));
        Assert.Contains(sources, source => source.Path.StartsWith("Yello.Host/", StringComparison.Ordinal));

        var found = ArchiveMentions(sources);

        Assert.True(
            found.Count == 0,
            "Yello has no archive, so no copy may say it does (AC5, UX-DR37):" + Environment.NewLine + string.Join(Environment.NewLine, found));
    }

    /// <summary>
    /// The gate's own oracle, a permanent plant: the matcher reports "Archive" in a resource value, "archived" in a
    /// translator's comment and "ARCHIVING" in markup, and does not report "search", "March" or "Marchive".
    /// </summary>
    [Fact]
    public void The_archive_gate_reports_every_form_of_the_word_and_nothing_that_merely_contains_its_letters()
    {
        (string Path, string Text)[] planted =
        [
            ("planted/ClientCopy.resx", "<data name=\"X\"><value>Archive this Project</value></data>"),
            ("planted/Comment.resx", "<comment>Shown once a Task is archived.</comment>"),
            ("planted/Row.razor", "<span>ARCHIVING</span>"),
            ("planted/Innocent.razor", "<span>Search in March for Marchive</span>"),
        ];

        var found = ArchiveMentions(planted);

        Assert.Equal(3, found.Count);
        Assert.DoesNotContain(found, line => line.StartsWith("planted/Innocent.razor", StringComparison.Ordinal));
    }

    /// <summary>
    /// AC1, AC2, AC4, AC11, AC12, story Task 11 - every deletion sentence is a resource value exactly once (the
    /// confirms reuse one "This cannot be undone.", never concatenate it) and carries a non-empty translator's comment.
    /// </summary>
    [Fact(Skip = "red: AC4 - the Task and Project deletion copy is not in ClientCopy.resx yet (story Task 11)")]
    [Trait("Requirement", "UX-DR36")]
    [Trait("Requirement", "UX-DR42")]
    public void The_deletion_copy_is_externalised_once_each_with_a_comment()
    {
        Assert.True(ClientCopy.Exists, $"{ClientCopy.FullName} does not exist.");

        var entries = XDocument.Load(ClientCopy.FullName).Root!.Elements("data")
            .Select(data => (Value: data.Element("value")?.Value ?? string.Empty, Comment: data.Element("comment")?.Value ?? string.Empty))
            .ToList();

        foreach (var copy in DeletionCopy)
        {
            var matches = entries.Where(entry => string.Equals(entry.Value, copy, StringComparison.Ordinal)).ToList();

            Assert.True(matches.Count == 1, $"\"{copy}\" is a ClientCopy value {matches.Count} times; expected exactly once.");
            Assert.False(string.IsNullOrWhiteSpace(matches[0].Comment), $"\"{copy}\" has no translator's comment.");
        }
    }

    /// <summary>
    /// Every product source a user-visible string can come from, as (repository-relative path, text): the client's
    /// components, code, styles, pages and resources; the contracts; the Host. Build output is not source.
    /// </summary>
    private static IEnumerable<(string Path, string Text)> ProductSources()
    {
        var client = Path.Combine(RepositoryLayout.Root.FullName, "Yello.Client");
        var contracts = Path.Combine(RepositoryLayout.Root.FullName, "Yello.Contracts");
        var host = Path.Combine(RepositoryLayout.Root.FullName, "Yello.Host");

        var files = Directory.EnumerateFiles(client, "*", SearchOption.AllDirectories)
            .Where(path => ClientExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Concat(Directory.EnumerateFiles(contracts, "*.cs", SearchOption.AllDirectories))
            .Concat(Directory.EnumerateFiles(host, "*.cs", SearchOption.AllDirectories))
            .Where(path => !IsBuildOutput(path));

        foreach (var path in files)
        {
            yield return (Path.GetRelativePath(RepositoryLayout.Root.FullName, path).Replace('\\', '/'), File.ReadAllText(path));
        }
    }

    private static bool IsBuildOutput(string path) =>
        Array.Exists(path.Split(PathSeparators), segment => segment is "bin" or "obj" or "node_modules");

    private static readonly char[] PathSeparators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>
    /// One line - "path:line: text" - per line of any source that uses the word's stem at a word start.
    /// </summary>
    private static List<string> ArchiveMentions(IEnumerable<(string Path, string Text)> sources) =>
        [
            .. sources.SelectMany(source => source.Text
                .Split('\n')
                .Select((line, index) => (Line: line, Number: index + 1))
                .Where(line => ArchiveWord.IsMatch(line.Line))
                .Select(line => $"{source.Path}:{line.Number}: {line.Line.Trim()}")),
        ];
}
