using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Boards;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.9's static gates on the paged Board: nothing in the client virtualises a list (AC3 - append, never recycle),
/// the wire changes are appends (AD-19), the page route and the page size are each stated once, and the two new refusals
/// have problem types of their own. AC3, AC9, AC18; AR-22, AD-29, AD-19. Written by the story 2.9 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why append and never recycle</b> (the reason AC3 requires to be stated): a virtualised list reuses a DOM row for
/// whichever Task scrolls into it, and keyboard focus sits on an element, not on a Task - so when the window shifts, the
/// focused element silently comes to represent a different Task and the next keyboard move operates on the wrong one: a
/// data-corrupting defect reachable only by keyboard and invisible to pointer testing (spine AD-29). Append-only rendering
/// with <c>@key</c> by Task id means an element only ever represents one Task.
/// </para>
/// <para>
/// <b>The contract gates reach unwritten members by name</b> (reflection), so they compile today and go green when story
/// Task 2 and Task 10 add them. The virtualisation gate is green today and stays a standing gate; its detector is proved
/// against planted text, as every absence assertion here is (<c>TESTING-CONVENTIONS.md</c>).
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AD-29")]
[Trait("Requirement", "AR-22")]
public sealed partial class BoardPagingGateTests
{
    private const string ColumnRoute = "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/board/columns/{statusId:guid}";

    private static readonly string[] ClientSourceExtensions = [".razor", ".cs"];

    private static readonly string[] ColumnPageMembers = ["NextAfter", "StatusId", "Tasks", "Total"];

    private static readonly string[] BoardColumnMembersBefore29 = ["Name", "StatusId", "Tasks", "Total"];

    private static readonly string[] BoardColumnMembersAfter29 = ["Name", "NextAfter", "StatusId", "Tasks", "Total"];

    /// <summary>
    /// AC3 - no component, page or code-behind in the client uses <c>Virtualize</c> or an <c>ItemsProvider</c>: rows are
    /// appended and never recycled. Green today; a standing gate. (The template's <c>@using</c> of the virtualisation
    /// namespace in <c>_Imports.razor</c> is an import, not a use.)
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR24")]
    public void Nothing_in_the_client_virtualises_a_list()
    {
        var files = ClientSourceFiles().ToList();

        // Anchor: the scan reads the Board's own column, so "nothing found" is about the files that matter.
        Assert.Contains(files, file => file.Name == "BoardColumn.razor");

        var problems = files
            .Where(file => Virtualisation.IsMatch(File.ReadAllText(file.FullName)))
            .Select(RepositoryLayout.RelativePath)
            .ToList();

        Assert.True(
            problems.Count == 0,
            $"The client virtualises a list in: {string.Join(", ", problems)}. AD-29: append, never recycle - a recycled row " +
            "re-points keyboard focus at a different Task.");
    }

    /// <summary>
    /// The detector behind <see cref="Nothing_in_the_client_virtualises_a_list"/>, fired on planted text (the permanent
    /// plant story Task 4 asks for) and on the clean form.
    /// </summary>
    [Fact]
    public void The_virtualisation_scan_detects_a_plant()
    {
        Assert.Matches(Virtualisation, """<Virtualize Items="@column.Tasks" Context="task">""");
        Assert.Matches(Virtualisation, """<Virtualize ItemsProvider="@LoadAsync">""");
        Assert.Matches(Virtualisation, "private ValueTask<ItemsProviderResult<TaskResponse>> LoadAsync(ItemsProviderRequest request)");
        // The template's `@using` of the namespace in _Imports.razor is an import, not a use: it virtualises nothing.
        Assert.DoesNotMatch(Virtualisation, "@using Microsoft.AspNetCore.Components.Web.Virtualization");
        Assert.DoesNotMatch(Virtualisation, """@foreach (var task in Tasks) { <TaskCard @key="task.Id" /> }""");
    }

    /// <summary>
    /// AC9, AD-19 - <c>BoardColumnResponse</c> keeps every member it had and gains only the cursor, a nullable string.
    /// </summary>
    [Fact(Skip = "red: AC9 - BoardColumnResponse has no NextAfter until story Task 2 appends it")]
    [Trait("Requirement", "AD-19")]
    public void The_Board_column_contract_keeps_every_member_and_appends_only_the_cursor()
    {
        var members = typeof(BoardColumnResponse).GetProperties(BindingFlags.Public | BindingFlags.Instance).ToDictionary(property => property.Name, StringComparer.Ordinal);

        Assert.All(BoardColumnMembersBefore29, name => Assert.True(members.ContainsKey(name), $"BoardColumnResponse lost {name} (AD-19: append, never remove)."));
        Assert.Equal(BoardColumnMembersAfter29, members.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(typeof(string), members["NextAfter"].PropertyType);
        Assert.True(IsNullable(members["NextAfter"]), "NextAfter must be nullable: null exactly when no Task follows the page.");
    }

    /// <summary>
    /// AC9, Q1 (a) - the column page is its own contract type, <c>BoardColumnPageResponse</c>, with exactly
    /// <c>{StatusId, Total, Tasks, NextAfter}</c>.
    /// </summary>
    [Fact(Skip = "red: AC9 - BoardColumnPageResponse does not exist until story Task 2")]
    public void The_column_page_contract_is_its_own_type_with_its_Status_total_Tasks_and_cursor()
    {
        var page = typeof(BoardResponse).Assembly.GetType("Yello.Contracts.Boards.BoardColumnPageResponse");

        Assert.NotNull(page);
        Assert.Equal(ColumnPageMembers, page.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// AC9, Q1 (a) - the column-page route is stated once, beside the Board route, in <c>BoardRoutes</c>.
    /// </summary>
    [Fact(Skip = "red: AC9 - BoardRoutes has no column-page route until story Task 2")]
    public void The_column_page_route_is_stated_once_beside_the_Board_route()
    {
        var routes = typeof(BoardRoutes).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => property.GetValue(null) as string)
            .ToList();

        Assert.Single(routes, route => string.Equals(route, ColumnRoute, StringComparison.Ordinal));
    }

    /// <summary>
    /// AR-22 (an assumption, spine <c>:259</c>: "the seek shape is the invariant, the page size is not") - the page size is
    /// one named constant, <c>BoardPage.Size</c>, of 50, in Application or Contracts.
    /// </summary>
    [Fact(Skip = "red: AC9 - no BoardPage.Size until story Task 2")]
    [Trait("Assumption", "AR-22")]
    public void The_page_size_is_one_named_constant_of_fifty()
    {
        var candidates = new[] { typeof(BoardResponse).Assembly, typeof(Yello.Application.Boards.ViewBoard.ViewBoardHandler).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Name == "BoardPage")
            .ToList();

        var size = Assert.Single(candidates).GetMember("Size", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Single();

        var value = size switch { FieldInfo field => field.GetValue(null), PropertyInfo property => property.GetValue(null), _ => null };

        Assert.Equal(50, Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// AC9, AC18 - the malformed cursor and the full gap each have a problem type of their own, beside the changed
    /// neighbour's <c>task-position-unavailable</c>.
    /// </summary>
    [Fact(Skip = "red: AC9, AC18 - the two problem types arrive with story Tasks 2 and 10")]
    public void The_malformed_cursor_and_the_full_gap_each_have_their_own_problem_type()
    {
        var types = typeof(ProblemTypes).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => property.GetValue(null) as string)
            .ToList();

        Assert.Contains("urn:yello:problem:invalid-board-cursor", types);
        Assert.Contains("urn:yello:problem:task-position-full", types);
        Assert.Contains(ProblemTypes.TaskPositionUnavailable, types);
        Assert.Equal(types.Count, types.Distinct(StringComparer.Ordinal).Count());
    }

    private static bool IsNullable(PropertyInfo property) =>
        new NullabilityInfoContext().Create(property).ReadState == NullabilityState.Nullable;

    private static IEnumerable<FileInfo> ClientSourceFiles() =>
        ClientSourceExtensions
            .SelectMany(extension => RepositoryLayout.EnumerateSourceFiles("*" + extension))
            .Where(file => RepositoryLayout.RelativePath(file).StartsWith("Yello.Client/", StringComparison.Ordinal));

    [GeneratedRegex(@"<Virtualize\b|\bItemsProvider\w*", RegexOptions.None, 1000)]
    private static partial Regex Virtualisation { get; }
}
