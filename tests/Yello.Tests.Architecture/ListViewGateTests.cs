using System.Collections;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Boards;
using Yello.Contracts.Tasks;
using Yello.Domain.Tasks;
using Yello.Infrastructure.Persistence;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 2.10's static gates on the List View: every offered sort has its composite index (test design P-4, treated as
/// P0), the route is stated once beside the Board's, the contract is exactly what open question 1 (a) decides and carries
/// no <c>SpaceId</c>, the page size is one named constant, the two refusals have problem types of their own, the
/// Assignee dimension is absent (AC2), and the existing virtualisation gate's scan covers the new view. AC2, AC6, AC12,
/// AC18, AC22; AR-22, AD-29, AD-19. Written red by the story 2.10 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a sort without its index is a defect, not a slow query</b> (AR-22, spine AD-29 <c>:258</c>): at 5,000 Tasks a sort
/// the index does not serve reads and sorts the Project's rows on every page - a stated-budget failure (NFR-5), not a
/// tuning matter. So the gate is structural: it reads the EF model, needs no database, and fails the build's first suite.
/// </para>
/// <para>
/// <b>Members not yet written are reached by name</b> (reflection), as story 2.9's gates were: <c>Yello.Contracts.Lists</c>'
/// <c>ListSorts</c>, <c>ListRoutes</c>, <c>ListPageResponse</c> and <c>ListStatusResponse</c>; Application's
/// <c>ListPage</c>; and the sort-to-column map Task 9 asks to be stated once in Infrastructure, assumed here as a static
/// <c>IReadOnlyDictionary&lt;string, string&gt;</c> (sort code to <c>TaskItem</c> property) on a type named
/// <c>ListSortColumns</c>. A different name or shape is corrected here and recorded in the Debug Log.
/// </para>
/// <para>
/// <b>The index detector is proved against planted signals today</b> (<c>TESTING-CONVENTIONS.md</c>: an absence assertion
/// is validated against a plant): a sort mapped to a column with no index is reported, and the existing Board index is
/// found by the same column-list match - so the gate cannot pass by reading nothing.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-22")]
[Trait("Requirement", "AD-29")]
[Trait("Requirement", "FR-30")]
public sealed class ListViewGateTests
{
    private const string ListRoute = "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/list";

    private const string InvalidListQuery = "urn:yello:problem:invalid-list-query";

    private const string InvalidListCursor = "urn:yello:problem:invalid-list-cursor";

    private static readonly string[] ListPageMembers = ["NextAfter", "Project", "Statuses", "Tasks", "Total"];

    private static readonly string[] ListStatusMembers = ["Id", "Name"];

    private static readonly string[] BoardIndexColumns = ["SpaceId", "ProjectId", "StatusId", "PositionKey"];

    private static readonly string[] BoardIndexColumnsReordered = ["SpaceId", "ProjectId", "PositionKey", "StatusId"];

    /// <summary>
    /// P-4 (AC6, AR-22; P0) - for every sort code <c>ListSorts</c> offers, the EF model holds an index on <c>Task</c> whose
    /// key columns are exactly <c>(SpaceId, ProjectId, &lt;sort column&gt;, Id)</c>, in that order, with the sort column
    /// read from the one map Infrastructure states.
    /// </summary>
    [Fact(Skip = "red: AC6 - ListSorts, the sort-to-column map and the three composite indexes do not exist yet (story Tasks 2 (b), 4 (b), 9)")]
    public void Every_offered_sort_has_a_composite_index_on_SpaceId_ProjectId_its_column_and_Id()
    {
        var sorts = SortCodes();
        var columns = SortColumnMap();

        Assert.Equal(sorts.Order(StringComparer.Ordinal), columns.Keys.Order(StringComparer.Ordinal));

        using var context = new YelloDbContextFactory().CreateDbContext([]);

        Assert.Equal([], MissingSortIndexes(context.Model, columns));
    }

    /// <summary>
    /// The detector behind <see cref="Every_offered_sort_has_a_composite_index_on_SpaceId_ProjectId_its_column_and_Id"/>,
    /// fired today on a planted map: a sort mapped to <c>Title</c>, which no index will ever lead with, is reported; the
    /// column-list match finds the existing Board index and refuses the same columns in another order. Green
    /// today; a standing gate on the detector.
    /// </summary>
    [Fact]
    public void The_sort_index_detector_reports_a_planted_sort_with_no_index_and_finds_an_existing_one()
    {
        using var context = new YelloDbContextFactory().CreateDbContext([]);

        var planted = new Dictionary<string, string>(StringComparer.Ordinal) { ["planted-title"] = nameof(TaskItem.Title) };

        Assert.Equal(["planted-title"], MissingSortIndexes(context.Model, planted));
        Assert.True(HasIndexOn(context.Model, BoardIndexColumns), "The column-list match does not find the Board's own index, so it would report every sort.");
        Assert.False(HasIndexOn(context.Model, BoardIndexColumnsReordered), "The column-list match ignores column order.");
    }

    /// <summary>
    /// AC2 (AR-22's staging; FR-30 completes in 4.6) - the sorts offered are exactly Status, due date and Label: three
    /// stable codes, and none names the Assignee, which no Task holds until Epic 4.
    /// </summary>
    [Fact(Skip = "red: AC2 - ListSorts does not exist yet (story Task 4 (b))")]
    public void The_offered_sorts_are_exactly_three_and_none_is_the_Assignee()
    {
        var sorts = SortCodes();

        Assert.Equal(3, sorts.Count);
        Assert.Equal(sorts.Count, sorts.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(sorts, code => code.Contains("assignee", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// AC22, Q1 (a) - the List route is stated once in <c>Yello.Contracts</c>, beside the Board's.
    /// </summary>
    [Fact(Skip = "red: AC22 - the List route is not stated in Yello.Contracts yet (story Task 4 (a))")]
    public void The_List_route_is_stated_once_in_Contracts()
    {
        var routes = typeof(BoardRoutes).Assembly.GetTypes()
            .SelectMany(type => type.GetMembers(BindingFlags.Public | BindingFlags.Static))
            .Select(StaticString)
            .Where(value => string.Equals(value, ListRoute, StringComparison.Ordinal))
            .ToList();

        Assert.Single(routes);
    }

    /// <summary>
    /// AC22, AD-19, Q1 (a) - <c>ListPageResponse</c> has exactly <c>{NextAfter, Project, Statuses, Tasks, Total}</c>: the
    /// Project is the Board's <see cref="BoardProjectResponse"/>, each row is <see cref="TaskResponse"/> reused unchanged,
    /// each Status is <c>ListStatusResponse(Id, Name)</c>, the cursor a nullable string, and nothing carries a
    /// <c>SpaceId</c>.
    /// </summary>
    [Fact(Skip = "red: AC22 - ListPageResponse and ListStatusResponse do not exist yet (story Task 4 (b))")]
    [Trait("Requirement", "AD-19")]
    public void The_List_page_contract_has_exactly_its_members_reuses_TaskResponse_and_carries_no_SpaceId()
    {
        var page = typeof(BoardRoutes).Assembly.GetType("Yello.Contracts.Lists.ListPageResponse");
        var status = typeof(BoardRoutes).Assembly.GetType("Yello.Contracts.Lists.ListStatusResponse");

        Assert.NotNull(page);
        Assert.NotNull(status);

        var members = page.GetProperties(BindingFlags.Public | BindingFlags.Instance).ToDictionary(property => property.Name, StringComparer.Ordinal);

        Assert.Equal(ListPageMembers, members.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(ListStatusMembers, status.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(typeof(BoardProjectResponse), members["Project"].PropertyType);
        Assert.Equal(typeof(IReadOnlyList<TaskResponse>), members["Tasks"].PropertyType);
        Assert.Equal(typeof(int), members["Total"].PropertyType);
        Assert.Equal(typeof(string), members["NextAfter"].PropertyType);
        Assert.True(new NullabilityInfoContext().Create(members["NextAfter"]).ReadState == NullabilityState.Nullable, "NextAfter must be nullable: null exactly when nothing follows.");
        Assert.DoesNotContain(page.GetProperties().Concat(status.GetProperties()), property => property.Name.Contains("Space", StringComparison.Ordinal));
    }

    /// <summary>
    /// AC12, Q2 (a) (AR-22 "the page size is not the invariant") - the List page size is one named constant,
    /// <c>ListPage.Size</c>, of 50, and the List route has no size parameter.
    /// </summary>
    [Fact(Skip = "red: AC12 - ListPage does not exist yet (story Task 3 (b))")]
    [Trait("Assumption", "AR-22")]
    public void The_List_page_size_is_one_named_constant_of_fifty()
    {
        var candidates = new[] { typeof(BoardRoutes).Assembly, typeof(Yello.Application.Boards.ViewBoard.ViewBoardHandler).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.Name == "ListPage")
            .ToList();

        var size = Assert.Single(candidates).GetMember("Size", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).Single();
        var value = size switch { FieldInfo field => field.GetValue(null), PropertyInfo property => property.GetValue(null), _ => null };

        Assert.Equal(50, Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture));
        Assert.DoesNotContain("size", ListRoute, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC12, Task 4 (c) - a malformed query and a cursor not issued for this sort each have a problem type of their own,
    /// distinct from the Board's cursor refusal and from every other type.
    /// </summary>
    [Fact(Skip = "red: AC12 - the two List problem types do not exist yet (story Task 4 (c))")]
    public void The_malformed_query_and_the_foreign_cursor_each_have_their_own_problem_type()
    {
        var types = typeof(ProblemTypes).GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => property.GetValue(null) as string)
            .ToList();

        Assert.Contains(InvalidListQuery, types);
        Assert.Contains(InvalidListCursor, types);
        Assert.Contains("urn:yello:problem:invalid-board-cursor", types);
        Assert.Equal(types.Count, types.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// AC18 (AD-29: append, never recycle) - the existing virtualisation gate's scan covers the new view: the client's
    /// List View files exist under <c>Yello.Client/</c>, so <c>BoardPagingGateTests.Nothing_in_the_client_virtualises_a_list</c>
    /// reads them, and none of them uses <c>Virtualize</c> or an <c>ItemsProvider</c>.
    /// </summary>
    [Fact(Skip = "red: AC18 - the List View component does not exist yet (story Task 5 (c))")]
    [Trait("Requirement", "UX-DR24")]
    public void The_List_View_is_inside_the_virtualisation_scan_and_virtualises_nothing()
    {
        var files = RepositoryLayout.EnumerateSourceFiles("*.razor")
            .Concat(RepositoryLayout.EnumerateSourceFiles("*.cs"))
            .Where(file => RepositoryLayout.RelativePath(file).StartsWith("Yello.Client/", StringComparison.Ordinal))
            .Where(file => file.Name.StartsWith("TaskList", StringComparison.Ordinal))
            .ToList();

        Assert.Contains(files, file => file.Name == "TaskListView.razor");
        Assert.All(files, file => Assert.DoesNotMatch(@"<Virtualize\b|\bItemsProvider\w*", File.ReadAllText(file.FullName)));
    }

    /// <summary>
    /// The sort codes for which no index on <c>Task</c> has exactly <c>(SpaceId, ProjectId, column, Id)</c> as its key
    /// columns, in order.
    /// </summary>
    private static List<string> MissingSortIndexes(IModel model, IReadOnlyDictionary<string, string> columns) =>
        [.. columns
            .Where(entry => !HasIndexOn(model, ["SpaceId", "ProjectId", entry.Value, "Id"]))
            .Select(entry => entry.Key)
            .Order(StringComparer.Ordinal)];

    private static bool HasIndexOn(IModel model, IReadOnlyList<string> properties)
    {
        var task = model.FindEntityType(typeof(TaskItem));

        Assert.NotNull(task);

        return task.GetIndexes().Any(index => index.Properties.Select(property => property.Name).SequenceEqual(properties, StringComparer.Ordinal));
    }

    private static List<string> SortCodes()
    {
        var sorts = typeof(BoardRoutes).Assembly.GetType("Yello.Contracts.Lists.ListSorts");

        Assert.NotNull(sorts);

        return [.. sorts.GetMembers(BindingFlags.Public | BindingFlags.Static).Select(StaticString).OfType<string>()];
    }

    private static Dictionary<string, string> SortColumnMap()
    {
        var holder = typeof(YelloDbContext).Assembly.GetTypes().SingleOrDefault(type => type.Name == "ListSortColumns");

        Assert.NotNull(holder);

        var map = holder.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Select(member => member switch { FieldInfo field => field.GetValue(null), PropertyInfo property => property.GetValue(null), _ => null })
            .OfType<IEnumerable>()
            .Single();

        return map.Cast<object>()
            .Select(entry => (Key: entry.GetType().GetProperty("Key")?.GetValue(entry) as string, Value: entry.GetType().GetProperty("Value")?.GetValue(entry) as string))
            .Where(entry => entry.Key is not null && entry.Value is not null)
            .ToDictionary(entry => entry.Key!, entry => entry.Value!, StringComparer.Ordinal);
    }

    private static string? StaticString(MemberInfo member) => member switch
    {
        FieldInfo { IsLiteral: true } field when field.FieldType == typeof(string) => field.GetRawConstantValue() as string,
        FieldInfo field when field.FieldType == typeof(string) => field.GetValue(null) as string,
        PropertyInfo property when property.PropertyType == typeof(string) && property.GetIndexParameters().Length == 0 => property.GetValue(null) as string,
        _ => null,
    };
}
