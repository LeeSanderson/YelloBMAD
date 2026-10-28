using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using Yello.Tests.Slices.Tasks;
using static Yello.Tests.Slices.Lists.ListWire;
using static Yello.Tests.Slices.Tasks.LabelWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Lists;

/// <summary>
/// Story 2.10 over the wire, PR tier: the List View's pair keyset <c>(sortColumn, TaskId)</c> walked to the end under each
/// of the three sorts with page boundaries inside runs of ties and across the <c>NULL</c> segment (test design P-3), the
/// filters and the true total from its own <c>COUNT</c>, a cursor that outlives its Task, the route's refusals, the page
/// size the server owns, a Viewer served a Member's bytes, the Label sort key the Labels edit maintains, and the
/// deterministic half of AC10: the last page reads no more than the first. AC1, AC4-AC6, AC10-AC16, AC22; FR-30, AR-22,
/// AD-29, AD-16, AD-19, NFR-5 (deterministic half), NFR-8. Written red by the story 2.10 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Expected orders come from SQL Server</b> (<see cref="ListWire.ExpectedOrderAsync"/>), never from
/// <see cref="Guid.CompareTo(Guid)"/>: SQL Server orders a <c>uniqueidentifier</c> by its last six bytes first, so a .NET
/// sort of Task ids is not the tiebreaker's order (spine <c>:268</c>, story Task 3 (d)).
/// </para>
/// <para>
/// <b>Labels in the correctness cases are applied through the product's Labels edit</b>, so the order the walk is held to
/// (computed from <c>TaskLabel</c> and <c>Label</c>) also proves the product kept <c>FirstLabelName</c> true. The 5,000-row
/// cost cases seed their Labels by SQL and backfill the key in the migration's shape.
/// </para>
/// <para>
/// <b>One class, one database</b> on the Slices suite's one engine - no new engine, no new fixture type
/// (<c>ContainerTopologyGateTests</c>). Nothing here measures time; the timed half of AC10 is
/// <c>ListScaleTimingTests</c> (Nightly). The race case holds a transaction and waits on <c>LCK_M_%</c>, never on a clock.
/// </para>
/// <para>
/// <b>Contract members</b>: the scaffold constants in <see cref="ListWire"/> are swapped for <c>ListRoutes</c>,
/// <c>ListSorts</c>, <c>ListPage.Size</c> and the two <c>ProblemTypes</c> entries on activation.
/// </para>
/// </remarks>
/// <param name="fixture">This class's migrated database on the shared engine.</param>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-30")]
[Trait("Requirement", "AR-22")]
[Trait("Requirement", "AD-29")]
public sealed class ListPagingTests(MigratedDatabaseFixture fixture) : IClassFixture<MigratedDatabaseFixture>
{
    private const int Scale = 5_000;

    private static readonly DateTimeOffset TiedDay = new(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);

    private static readonly string[] PageProperties = ["nextAfter", "project", "statuses", "tasks", "total"];

    private static readonly string[] TaskProperties = ["createdAt", "dueDate", "id", "labelIds", "positionKey", "statusId", "title"];

    private static readonly string[] IdAndName = ["id", "name"];

    private static readonly string[] LabelWords = ["Alpha", "Bravo", "Charlie"];

    /// <summary>
    /// LV-01 (P-3, AC4, AC14; P0) - sorted by Status, 155 Tasks over three Statuses walk in the Board's column order and,
    /// within a Status, by <c>TaskId</c> in SQL Server's order; page boundaries at 50, 100 and 150 land inside a Status's run;
    /// every Task appears exactly once, every page states the true total, and the last cursor is <c>null</c>. The served
    /// Statuses are the effective set in effective order - not name order, which the default set's names would give
    /// differently (the anchor that the case can tell them apart).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-16")]
    public async Task Sorted_by_Status_every_Task_appears_once_in_effective_order_then_TaskId()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        IReadOnlyList<StatusRow> statuses = [];
        IReadOnlyList<ListPage> pages = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = await CreateProjectAsync(owner, "Listed by Status");
            statuses = await StatusesAsync(database, owner.SpaceId);

            await SeedListAsync(database, owner.SpaceId, projectId, [.. statuses.Select(status => status.Id)], 155);

            pages = await WalkAsync(owner, owner.SpaceId, projectId, ListQuery.By(SortStatus));
        });

        var expected = await ExpectedOrderAsync(database, owner!.SpaceId, projectId, SortStatus);

        AssertWalk(pages, expected);
        Assert.Equal(statuses.Select(status => status.Id), pages[0].StatusIds);
        Assert.NotEqual(statuses.Select(status => status.Name), statuses.Select(status => status.Name).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// LV-02 (P-3, AC4, AC5; P0) - sorted by due date with 120 Tasks sharing one day, so the boundaries at 50 and 100 land
    /// inside the run of ties: the pair keyset compares <c>(DueDate, TaskId)</c>, so no tied Task is skipped or repeated.
    /// 20 Tasks are due earlier, one per day, and 20 have no due date and come last.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Sorted_by_due_date_a_page_boundary_inside_120_tied_dates_skips_and_repeats_nothing()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        IReadOnlyList<ListPage> pages = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = await CreateProjectAsync(owner, "Tied on a day");

            var statusIds = (await StatusesAsync(database, owner.SpaceId)).Select(status => status.Id).ToList();

            await SeedListAsync(database, owner.SpaceId, projectId, statusIds, 160);

            var ids = await IdsOfAsync(database, owner.SpaceId, projectId);

            await SetDueDateAsync(database, owner.SpaceId, ids.Take(120), TiedDay);

            for (var index = 0; index < 20; index++)
            {
                await SetDueDateAsync(database, owner.SpaceId, [ids[120 + index]], TiedDay.AddDays(-20 + index));
            }

            pages = await WalkAsync(owner, owner.SpaceId, projectId, ListQuery.By(SortDueDate));
        });

        var expected = await ExpectedOrderAsync(database, owner!.SpaceId, projectId, SortDueDate);

        AssertWalk(pages, expected);
        Assert.All(pages.SelectMany(page => page.Tasks).Take(140), task => Assert.NotNull(task.DueDate));
        Assert.All(pages.SelectMany(page => page.Tasks).Skip(140), task => Assert.Null(task.DueDate));
    }

    /// <summary>
    /// LV-03 (P-3, AC5; P0) - sorted by due date, 70 dated Tasks then 60 with none: the second page crosses from the dated
    /// segment into the undated one at row 71, and <c>NULL</c> is fixed last explicitly (Q5 (a)), never left to SQL
    /// Server's default of first.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Sorted_by_due_date_a_page_crossing_into_the_undated_Tasks_keeps_them_last_and_skips_nothing()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        IReadOnlyList<ListPage> pages = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = await CreateProjectAsync(owner, "Undated last");

            var statusIds = (await StatusesAsync(database, owner.SpaceId)).Select(status => status.Id).ToList();

            await SeedListAsync(database, owner.SpaceId, projectId, statusIds, 130);

            var ids = await IdsOfAsync(database, owner.SpaceId, projectId);

            // 70 dated, seven days of ten ties each; the other 60 stay NULL.
            for (var day = 0; day < 7; day++)
            {
                await SetDueDateAsync(database, owner.SpaceId, ids.Skip(day * 10).Take(10), TiedDay.AddDays(day));
            }

            pages = await WalkAsync(owner, owner.SpaceId, projectId, ListQuery.By(SortDueDate));
        });

        var expected = await ExpectedOrderAsync(database, owner!.SpaceId, projectId, SortDueDate);
        var walked = pages.SelectMany(page => page.Tasks).ToList();

        AssertWalk(pages, expected);
        Assert.All(walked.Take(70), task => Assert.NotNull(task.DueDate));
        Assert.All(walked.Skip(70), task => Assert.Null(task.DueDate));
    }

    /// <summary>
    /// LV-04 (P-3, AC4, AC13; P0) - sorted by Label: 70 Tasks whose first Label is "Alpha" (half of them carry "Bravo" too),
    /// 40 whose only Label is "Bravo", 30 with none - boundaries at 50 and 100 inside the runs of ties. Each Task appears
    /// exactly once (Q3 (a): by its first Label's name in chip order, never once per Label), and the key the walk follows is
    /// the one the product's Labels edit maintained.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Sorted_by_Label_a_page_boundary_inside_70_tied_first_Labels_lists_each_Task_once()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        IReadOnlyList<ListPage> pages = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = await CreateProjectAsync(owner, "Tied on a Label");

            var statusIds = (await StatusesAsync(database, owner.SpaceId)).Select(status => status.Id).ToList();
            var alpha = await SeedAsync(owner, owner.SpaceId, "Alpha " + Unique());
            var bravo = await SeedAsync(owner, owner.SpaceId, "Bravo " + Unique());

            await SeedListAsync(database, owner.SpaceId, projectId, statusIds, 140);

            var ids = await IdsOfAsync(database, owner.SpaceId, projectId);

            for (var index = 0; index < 110; index++)
            {
                if (index >= 70)
                {
                    await SetLabelsAsync(owner, owner.SpaceId, ids[index], bravo);
                }
                else if (index % 2 == 0)
                {
                    await SetLabelsAsync(owner, owner.SpaceId, ids[index], alpha, bravo);
                }
                else
                {
                    await SetLabelsAsync(owner, owner.SpaceId, ids[index], alpha);
                }
            }

            pages = await WalkAsync(owner, owner.SpaceId, projectId, ListQuery.By(SortLabel));
        });

        var expected = await ExpectedOrderAsync(database, owner!.SpaceId, projectId, SortLabel);

        AssertWalk(pages, expected);
        Assert.Equal(140, pages[0].Total);
    }

    /// <summary>
    /// LV-05 (P-3, AC5, AC13; P0) - sorted by Label, 70 labelled Tasks then 50 with no Label: the second page crosses into
    /// the unlabelled segment at row 71, which is fixed last (AC13: the same rule as AC5).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Sorted_by_Label_a_page_crossing_into_the_unlabelled_Tasks_keeps_them_last_and_skips_nothing()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        IReadOnlyList<ListPage> pages = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = await CreateProjectAsync(owner, "Unlabelled last");

            var statusIds = (await StatusesAsync(database, owner.SpaceId)).Select(status => status.Id).ToList();
            var labels = new[]
            {
                await SeedAsync(owner, owner.SpaceId, "Alpha " + Unique()),
                await SeedAsync(owner, owner.SpaceId, "Bravo " + Unique()),
                await SeedAsync(owner, owner.SpaceId, "Charlie " + Unique()),
            };

            await SeedListAsync(database, owner.SpaceId, projectId, statusIds, 120);

            var ids = await IdsOfAsync(database, owner.SpaceId, projectId);

            for (var index = 0; index < 70; index++)
            {
                await SetLabelsAsync(owner, owner.SpaceId, ids[index], labels[index % 3]);
            }

            pages = await WalkAsync(owner, owner.SpaceId, projectId, ListQuery.By(SortLabel));
        });

        var expected = await ExpectedOrderAsync(database, owner!.SpaceId, projectId, SortLabel);
        var walked = pages.SelectMany(page => page.Tasks).ToList();

        AssertWalk(pages, expected);
        Assert.All(walked.Take(70), task => Assert.NotEmpty(task.LabelIds));
        Assert.All(walked.Skip(70), task => Assert.Empty(task.LabelIds));
    }

    /// <summary>
    /// LV-06 (AC11, AC15, Q4 (a); P0) - each filter alone and the three combined: under every sort, the walk is exactly the
    /// Project's Tasks matching the predicate, in that sort's SQL order, and every page's total is that number - from its
    /// own <c>COUNT</c>, never the rows served. Labels match any-of: a Task carrying any chosen Label matches.
    /// </summary>
    /// <param name="filter">Which filter.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("status")]
    [InlineData("label-any-of")]
    [InlineData("due-range")]
    [InlineData("due-none")]
    [InlineData("combined")]
    public async Task A_filter_serves_exactly_its_matching_Tasks_and_their_true_total_under_every_sort(string filter)
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        ListQuery? query = null;
        var predicate = string.Empty;
        var walks = new Dictionary<string, IReadOnlyList<ListPage>>(StringComparer.Ordinal);

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = await CreateProjectAsync(owner, "Filtered");

            var statusIds = (await StatusesAsync(database, owner.SpaceId)).Select(status => status.Id).ToList();
            var a = await SeedAsync(owner, owner.SpaceId, "Alpha " + Unique());
            var b = await SeedAsync(owner, owner.SpaceId, "Bravo " + Unique());
            var c = await SeedAsync(owner, owner.SpaceId, "Charlie " + Unique());

            await SeedListAsync(database, owner.SpaceId, projectId, statusIds, 72);
            await SetDueDatesAsync(database, owner.SpaceId, projectId, distinctDays: 7);

            var ids = await IdsOfAsync(database, owner.SpaceId, projectId);

            // Labels by ordinal: [A], [B, C], [C], none - so "A or C" matches three in four.
            for (var index = 0; index < ids.Count; index++)
            {
                Guid[] labels = (index % 4) switch { 0 => [a], 1 => [b, c], 2 => [c], _ => [] };

                if (labels.Length > 0)
                {
                    await SetLabelsAsync(owner, owner.SpaceId, ids[index], labels);
                }
            }

            var from = TiedDay.AddDays(-2);
            var to = TiedDay.AddDays(1);
            var range = $"t.DueDate >= '{from:O}' AND t.DueDate < '{to:O}'";
            var anyOf = $"EXISTS (SELECT 1 FROM dbo.[TaskLabel] x WHERE x.TaskId = t.Id AND x.LabelId IN ('{a:D}', '{c:D}'))";
            var inStatuses = $"t.StatusId IN ('{statusIds[0]:D}', '{statusIds[1]:D}')";

            (query, predicate) = filter switch
            {
                "status" => (new ListQuery(SortStatus, [statusIds[0], statusIds[1]], []), inStatuses),
                "label-any-of" => (new ListQuery(SortStatus, [], [a, c]), anyOf),
                "due-range" => (new ListQuery(SortStatus, [], [], DueFrom: from, DueTo: to), range),
                "due-none" => (new ListQuery(SortStatus, [], [], DueNone: true), "t.DueDate IS NULL"),
                _ => (new ListQuery(SortStatus, [statusIds[0], statusIds[1]], [a, c], DueFrom: from, DueTo: to), $"{inStatuses} AND {anyOf} AND {range}"),
            };

            foreach (var sort in Sorts)
            {
                walks[sort] = await WalkAsync(owner, owner.SpaceId, projectId, query with { Sort = sort });
            }
        });

        foreach (var sort in Sorts)
        {
            var expected = await ExpectedOrderAsync(database, owner!.SpaceId, projectId, sort, predicate);

            Assert.NotEmpty(expected);
            Assert.True(expected.Count < 72, $"The {filter} filter excludes nothing, so the case proves nothing about it.");
            AssertWalk(walks[sort], expected);
        }
    }

    /// <summary>
    /// LV-07 (Q4 (a), AC15) - a Label deleted while chosen just stops matching: filtering by it alone answers <c>200</c> with
    /// nothing, and beside a live Label it changes nothing - never a <c>400</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_Label_deleted_while_chosen_stops_matching_and_is_never_refused()
    {
        var database = Available();
        ListPage? deletedAlone = null;
        WireResponse? withLive = null;
        WireResponse? liveAlone = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Stale filter");
            var statusIds = (await StatusesAsync(database, owner.SpaceId)).Select(status => status.Id).ToList();
            var live = await SeedAsync(owner, owner.SpaceId, "Live " + Unique());
            var doomed = await SeedAsync(owner, owner.SpaceId, "Doomed " + Unique());

            await SeedListAsync(database, owner.SpaceId, projectId, statusIds, 6);

            foreach (var taskId in (await IdsOfAsync(database, owner.SpaceId, projectId)).Take(3))
            {
                await SetLabelsAsync(owner, owner.SpaceId, taskId, live);
            }

            Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(owner, owner.SpaceId, doomed)).StatusCode);

            deletedAlone = PageOf(await PageAsync(owner, owner.SpaceId, projectId, new ListQuery(SortStatus, [], [doomed])));
            withLive = await PageAsync(owner, owner.SpaceId, projectId, new ListQuery(SortStatus, [], [doomed, live]));
            liveAlone = await PageAsync(owner, owner.SpaceId, projectId, new ListQuery(SortStatus, [], [live]));
        });

        Assert.Equal(0, deletedAlone!.Total);
        Assert.Empty(deletedAlone.Tasks);
        Assert.Equal(3, PageOf(liveAlone!).Total);
        Assert.Equal(liveAlone!.Body, withLive?.Body);
    }

    /// <summary>
    /// LV-08 (AC12, Q1 (a); P0) - a cursor is a pair, not an id: when the cursor's Task is deleted, or its due date or its
    /// Labels change, between pages, the next page still starts at the first pair after the cursor's, so the Tasks after
    /// it are served exactly as they were going to be - none skipped, none repeated.
    /// </summary>
    /// <param name="change">What happens to the cursor's Task between the pages.</param>
    /// <param name="sort">The sort the walk is under.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("deleted", SortDueDate)]
    [InlineData("due-date-changed", SortDueDate)]
    [InlineData("labels-changed", SortLabel)]
    [InlineData("deleted", SortStatus)]
    public async Task A_cursor_whose_Task_changed_or_went_still_seeks_to_the_first_pair_after_it(string change, string sort)
    {
        var database = Available();
        IReadOnlyList<Guid> expected = [];
        ListPage? first = null;
        ListPage? next = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Cursor outlives its Task");
            var statusIds = (await StatusesAsync(database, owner.SpaceId)).Select(status => status.Id).ToList();
            var alpha = await SeedAsync(owner, owner.SpaceId, "Alpha " + Unique());
            var zulu = await SeedAsync(owner, owner.SpaceId, "Zulu " + Unique());

            await SeedListAsync(database, owner.SpaceId, projectId, statusIds, 120);

            var ids = await IdsOfAsync(database, owner.SpaceId, projectId);

            await SetDueDateAsync(database, owner.SpaceId, ids, TiedDay);

            if (sort == SortLabel)
            {
                foreach (var taskId in ids)
                {
                    await SetLabelsAsync(owner, owner.SpaceId, taskId, alpha);
                }
            }

            expected = await ExpectedOrderAsync(database, owner.SpaceId, projectId, sort);
            first = PageOf(await PageAsync(owner, owner.SpaceId, projectId, ListQuery.By(sort)));

            var cursorTask = first.Tasks[^1].Id;
            var changed = change switch
            {
                "deleted" => await DeleteWire.DeleteTaskAsync(owner, owner.SpaceId, cursorTask),
                "due-date-changed" => await TaskEditWire.EditTaskAsync(owner, owner.SpaceId, cursorTask, TaskEditWire.Body(new { dueDate = TiedDay.AddYears(1) })),
                _ => await TaskEditWire.EditTaskAsync(owner, owner.SpaceId, cursorTask, ApplyBody(zulu)),
            };

            Assert.True(changed.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK, $"{changed.StatusCode} {changed.Body}");

            next = PageOf(await PageAsync(owner, owner.SpaceId, projectId, ListQuery.By(sort), first.NextAfter));
        });

        Assert.Equal(expected.Take(PageSize), first!.Tasks.Select(task => task.Id));
        Assert.Equal(expected.Skip(PageSize).Take(PageSize), next!.Tasks.Select(task => task.Id));
    }

    /// <summary>
    /// LV-09 (AC12, Task 4 (c); P1) - a cursor this route did not issue for this sort is refused <c>400
    /// invalid-list-cursor</c>, which depends only on what was sent, and records no refusal: garbage, one far too long,
    /// and a real cursor issued for another sort. Anchor: the same real cursor under its own sort is served.
    /// </summary>
    /// <param name="cursor">Which bad cursor.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("garbage")]
    [InlineData("too-long")]
    [InlineData("another-sorts")]
    public async Task A_cursor_not_issued_for_this_sort_is_refused_400_invalid_list_cursor(string cursor)
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? refused = null;
        WireResponse? anchor = null;
        IReadOnlyList<string> refusals = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projectId = await CreateProjectAsync(owner, "Cursor rules");
            var statusIds = (await StatusesAsync(database, owner.SpaceId)).Select(status => status.Id).ToList();

            await SeedListAsync(database, owner.SpaceId, projectId, statusIds, PageSize + 5);

            var issued = PageOf(await PageAsync(owner, owner.SpaceId, projectId, ListQuery.By(SortDueDate))).NextAfter;

            Assert.NotNull(issued);

            var (sort, sent) = cursor switch
            {
                "garbage" => (SortDueDate, "not a cursor at all"),
                "too-long" => (SortDueDate, new string('A', 4_000)),
                _ => (SortLabel, issued),
            };

            refused = await PageAsync(owner, owner.SpaceId, projectId, ListQuery.By(sort), sent);
            refusals = await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId);
            anchor = await PageAsync(owner, owner.SpaceId, projectId, ListQuery.By(SortDueDate), issued);
        });

        Assert.True(refused?.StatusCode == HttpStatusCode.BadRequest, $"{refused?.StatusCode} {refused?.Body}");
        Assert.Equal(InvalidListCursor, Problem(refused!)?.Type);
        Assert.Empty(refusals);
        Assert.Equal(HttpStatusCode.OK, anchor?.StatusCode);
    }

    /// <summary>
    /// LV-10 (AC12, AC2, Task 4 (c); P1) - an unknown sort (Assignee among them: AC2, not offered until 4.6), a malformed
    /// Status or Label id, and a malformed or out-of-window due bound (2.3's 2000-2100) are refused <c>400
    /// invalid-list-query</c>, recording nothing.
    /// </summary>
    /// <param name="query">The raw query string.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("sort=assignee")]
    [InlineData("sort=")]
    [InlineData("sort=title")]
    [InlineData("sort=status&status=not-a-guid")]
    [InlineData("sort=status&label=not-a-guid")]
    [InlineData("sort=due-date&dueFrom=yesterday")]
    [InlineData("sort=due-date&dueFrom=1999-12-31T00%3A00%3A00Z")]
    [InlineData("sort=due-date&dueTo=2101-01-02T00%3A00%3A00Z")]
    public async Task A_malformed_query_is_refused_400_invalid_list_query(string query)
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? refused = null;
        IReadOnlyList<string> refusals = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projectId = await CreateProjectAsync(owner, "Query rules");

            refused = await owner.Session.GetAsync($"/api/v1/spaces/{owner.SpaceId:D}/projects/{projectId:D}/list?{query}");
            refusals = await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId);
        });

        Assert.True(refused?.StatusCode == HttpStatusCode.BadRequest, $"{refused?.StatusCode} {refused?.Body}");
        Assert.Equal(InvalidListQuery, Problem(refused!)?.Type);
        Assert.Empty(refusals);
    }

    /// <summary>
    /// LV-11 (AC12, Task 4 (c)) - the checks run in a stated order, each on what was sent alone: a bad query and a bad
    /// cursor together answer the query's refusal; and a bad cursor naming a Project that does not exist answers the
    /// cursor's refusal, not the 404, because the handler never ran.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_query_is_checked_before_the_cursor_and_both_before_the_Project()
    {
        var database = Available();
        WireResponse? both = null;
        WireResponse? cursorOnUnknownProject = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Checked in order");

            both = await owner.Session.GetAsync($"/api/v1/spaces/{owner.SpaceId:D}/projects/{projectId:D}/list?sort=assignee&after=garbage");
            cursorOnUnknownProject = await PageAsync(owner, owner.SpaceId, Guid.NewGuid(), ListQuery.By(SortStatus), "garbage");
        });

        Assert.Equal(InvalidListQuery, Problem(both!)?.Type);
        Assert.Equal(InvalidListCursor, Problem(cursorOnUnknownProject!)?.Type);
    }

    /// <summary>
    /// LV-12 (AC12, Q2 (a); P1) - the server owns the page size: a <c>limit</c>, <c>pageSize</c> or <c>size</c> the client
    /// sends is not a lever on an unbounded read.
    /// </summary>
    /// <param name="parameter">A page-size parameter a client might try.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("limit")]
    [InlineData("pageSize")]
    [InlineData("size")]
    public async Task A_client_cannot_choose_the_List_page_size(string parameter)
    {
        var database = Available();
        WireResponse? page = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Sized by the server");
            var statusIds = (await StatusesAsync(database, owner.SpaceId)).Select(status => status.Id).ToList();

            await SeedListAsync(database, owner.SpaceId, projectId, statusIds, 300);

            page = await owner.Session.GetAsync(PathOf(owner.SpaceId, projectId, ListQuery.By(SortStatus)) + $"&{parameter}=500");
        });

        var served = PageOf(page!);

        Assert.Equal(PageSize, served.Tasks.Count);
        Assert.Equal(300, served.Total);
        Assert.NotNull(served.NextAfter);
    }

    /// <summary>
    /// LV-13 (AC16, AD-4, FR-35; P0) - a Viewer is served exactly a Member's List - same rows, same total, same cursor - on
    /// its Session and on its Token: the route requires <c>ReadProjectsAndTasks</c>, which every Role holds.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-4")]
    public async Task A_Viewer_is_served_a_Members_List_on_both_surfaces()
    {
        var database = Available();
        WireResponse? member = null;
        WireResponse? viewerSession = null;
        WireResponse? viewerToken = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var memberActor = await RegisterAsync(client, database);
            var viewer = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, memberActor.AccountId, "Member");
            await AddMemberAsync(database, owner.SpaceId, viewer.AccountId, "Viewer");

            var projectId = await CreateProjectAsync(owner, "Seen alike, listed");
            var statusIds = (await StatusesAsync(database, owner.SpaceId)).Select(status => status.Id).ToList();

            await SeedListAsync(database, owner.SpaceId, projectId, statusIds, PageSize + 5);
            await SetDueDatesAsync(database, owner.SpaceId, projectId, distinctDays: 3);

            var token = await IssueTokenAsync(viewer, owner.SpaceId);
            var query = ListQuery.By(SortDueDate);

            member = await PageAsync(memberActor, owner.SpaceId, projectId, query);
            viewerSession = await PageAsync(viewer, owner.SpaceId, projectId, query);
            viewerToken = await viewer.Session.GetWithTokenAsync(PathOf(owner.SpaceId, projectId, query), token);
        });

        Assert.Equal(PageSize, PageOf(member!).Tasks.Count);
        Assert.Equal(member!.Body, viewerSession?.Body);
        Assert.Equal(member.Body, viewerToken?.Body);
    }

    /// <summary>
    /// LV-14 (AC22, AD-19, Q1 (a)) - a List page answers exactly <c>{nextAfter, project, statuses, tasks, total}</c>; the
    /// Project is <c>{id, name}</c>, each Status <c>{id, name}</c> in the Board's column order, each Task exactly the
    /// Board's <c>TaskResponse</c> shape; the Space's id appears nowhere in it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-19")]
    public async Task A_List_page_answers_exactly_the_contracts_properties_and_no_SpaceId()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? page = null;
        WireResponse? board = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projectId = await CreateProjectAsync(owner, "Shaped list");

            _ = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "One"));
            page = await PageAsync(owner, owner.SpaceId, projectId, ListQuery.By(SortStatus));
            board = await BoardAsync(owner, owner.SpaceId, projectId);
        });

        Assert.Equal(HttpStatusCode.OK, page?.StatusCode);

        using var document = JsonDocument.Parse(page!.Body);
        using var boardDocument = JsonDocument.Parse(board!.Body);
        var root = document.RootElement;

        Assert.Equal(PageProperties, Names(root));
        Assert.Equal(IdAndName, Names(root.GetProperty("project")));
        Assert.All(root.GetProperty("statuses").EnumerateArray(), status => Assert.Equal(IdAndName, Names(status)));
        Assert.Equal(
            boardDocument.RootElement.GetProperty("columns").EnumerateArray().Select(column => column.GetProperty("statusId").GetGuid()),
            root.GetProperty("statuses").EnumerateArray().Select(status => status.GetProperty("id").GetGuid()));
        Assert.Equal(TaskProperties, Names(Assert.Single(root.GetProperty("tasks").EnumerateArray())));
        Assert.Equal(
            boardDocument.RootElement.GetProperty("columns")[0].GetProperty("tasks")[0].GetRawText(),
            root.GetProperty("tasks")[0].GetRawText());
        Assert.DoesNotContain(owner!.SpaceId.ToString("D"), page.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// LV-15 (AC3, AD-3, AD-20) - a Project id that matches nothing in the caller's own Space answers the boundary's <c>404
    /// not-available</c>, byte-identical to a Space that does not exist, and records nothing. Anchor: the own Project is
    /// served.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-3")]
    public async Task An_unknown_Project_answers_the_boundary_404_and_records_nothing()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? unknownProject = null;
        WireResponse? absentSpace = null;
        WireResponse? anchor = null;
        IReadOnlyList<string> refusals = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projectId = await CreateProjectAsync(owner, "Bounded list");

            unknownProject = await PageAsync(owner, owner.SpaceId, Guid.NewGuid(), ListQuery.By(SortStatus));
            refusals = await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId);
            absentSpace = await PageAsync(owner, Guid.NewGuid(), projectId, ListQuery.By(SortStatus));
            anchor = await PageAsync(owner, owner.SpaceId, projectId, ListQuery.By(SortStatus));
        });

        Assert.Equal(HttpStatusCode.NotFound, unknownProject!.StatusCode);
        Assert.Equal(absentSpace!.ContentType, unknownProject.ContentType);
        Assert.Equal(absentSpace.Body, unknownProject.Body);
        Assert.Equal(absentSpace.HeaderNames, unknownProject.HeaderNames);
        Assert.Equal(ProblemTypes.NotAvailable, Problem(unknownProject)?.Type);
        Assert.Empty(refusals);
        Assert.Equal(HttpStatusCode.OK, anchor?.StatusCode);
    }

    /// <summary>
    /// LV-16 (AC13, Task 2 (d); P0) - the Labels edit keeps <c>FirstLabelName</c> true in the same transaction: after
    /// applying to a bare Task, removing the first of two, replacing all, and clearing, the stored key is the first name in
    /// chip order (name, then id) of the Labels the Task carries - <c>NULL</c> when it carries none. A new Task starts
    /// <c>NULL</c>.
    /// </summary>
    /// <param name="edit">Which Labels edit.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("apply")]
    [InlineData("remove-first")]
    [InlineData("replace-all")]
    [InlineData("clear")]
    public async Task The_Labels_edit_keeps_the_Tasks_Label_sort_key_equal_to_its_first_chip(string edit)
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        var atCreation = new List<string?>();
        var names = new Dictionary<string, string>(StringComparer.Ordinal);

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projectId = await CreateProjectAsync(owner, "Kept true");

            taskId = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Relabelled"));
            atCreation.Add(await FirstLabelNameAsync(database, owner.SpaceId, taskId));

            var ids = new Dictionary<string, Guid>(StringComparer.Ordinal);

            foreach (var name in LabelWords)
            {
                names[name] = name + " " + Unique();
                ids[name] = await SeedAsync(owner, owner.SpaceId, names[name]);
            }

            switch (edit)
            {
                case "apply":
                    await SetLabelsAsync(owner, owner.SpaceId, taskId, ids["Bravo"]);
                    break;
                case "remove-first":
                    await SetLabelsAsync(owner, owner.SpaceId, taskId, ids["Alpha"], ids["Bravo"]);
                    await SetLabelsAsync(owner, owner.SpaceId, taskId, ids["Bravo"]);
                    break;
                case "replace-all":
                    await SetLabelsAsync(owner, owner.SpaceId, taskId, ids["Alpha"], ids["Bravo"]);
                    await SetLabelsAsync(owner, owner.SpaceId, taskId, ids["Charlie"]);
                    break;
                default:
                    await SetLabelsAsync(owner, owner.SpaceId, taskId, ids["Alpha"]);
                    await SetLabelsAsync(owner, owner.SpaceId, taskId);
                    break;
            }
        });

        // "clear" names no key, so the expectation is NULL.
        var expected = names.GetValueOrDefault(edit switch
        {
            "apply" or "remove-first" => "Bravo",
            "replace-all" => "Charlie",
            _ => "none",
        });

        Assert.Null(Assert.Single(atCreation));
        Assert.Equal(expected, await FirstAppliedLabelNameAsync(database, owner!.SpaceId, taskId));
        Assert.Equal(expected, await FirstLabelNameAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// LV-17 (AC13, Task 2 (d); P1) - two Labels edits of one Task, made to queue behind a held Labels lock and released
    /// together, leave the key equal to the first name of whichever set was written last: the recompute reads the final
    /// set inside its own transaction, never a snapshot taken before it (2.9: RCSI). Controlled, never timed - the test
    /// holds the Task's row and Label range (<see cref="LabelWire.HoldTaskLabelsAsync"/>) and waits until both are blocked
    /// or answered.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Two_Labels_edits_released_together_leave_the_key_equal_to_the_final_sets_first_name()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var taskId = Guid.Empty;
        WireResponse[] answers = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projectId = await CreateProjectAsync(owner, "Raced labels");

            taskId = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Twice relabelled"));

            var zulu = await SeedAsync(owner, owner.SpaceId, "Zulu " + Unique());
            var alpha = await SeedAsync(owner, owner.SpaceId, "Alpha " + Unique());

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, projectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldTaskLabelsAsync(holder, held, owner.SpaceId, taskId, cancellationToken);

            var toZulu = TaskEditWire.EditTaskAsync(owner, owner.SpaceId, taskId, ApplyBody(zulu));

            await WaitUntilBlockedOrAnsweredAsync(observer, 1, toZulu, cancellationToken);

            var toAlpha = TaskEditWire.EditTaskAsync(owner, owner.SpaceId, taskId, ApplyBody(alpha));

            await WaitUntilBlockedOrAnsweredAsync(observer, 2, toAlpha, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answers = await Task.WhenAll(toZulu, toAlpha);
        });

        Assert.All(answers, answer => Assert.True(answer.StatusCode == HttpStatusCode.OK, $"{answer.StatusCode} {answer.Body}"));

        var final = await FirstAppliedLabelNameAsync(database, owner!.SpaceId, taskId);

        Assert.NotNull(final);
        Assert.Equal(final, await FirstLabelNameAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// LV-18 (AC10 deterministic half, AC6, P-3/P-4; P0) - at 5,000 Tasks with ties on every sort column, about a third with
    /// no due date and two fifths with no Label, the last page of each sort - unfiltered and under the heaviest filter
    /// combination - reads no more logical pages than its first, within 2.9's tolerance of 5 (PG-11: each warmed first).
    /// An <c>OFFSET</c>, or a sort whose index is missing, reads every row it skips.
    /// </summary>
    /// <remarks>
    /// Plants owed by the dev phase (story Task 8): an <c>OFFSET</c>/<c>Skip()</c> in the reader, and one sort's index
    /// dropped - each must fail this case. Restored in a <c>finally</c>, never by <c>git checkout</c>.
    /// </remarks>
    /// <param name="sort">The sort.</param>
    /// <param name="filtered">Whether under the heaviest filter combination (Q4).</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(SortStatus, false)]
    [InlineData(SortDueDate, false)]
    [InlineData(SortLabel, false)]
    [InlineData(SortStatus, true)]
    [InlineData(SortDueDate, true)]
    [InlineData(SortLabel, true)]
    [Trait("Requirement", "NFR-5")]
    [Trait("Requirement", "NFR-8")]
    public async Task The_last_page_of_each_sort_reads_no_more_than_its_first_at_5000_Tasks(string sort, bool filtered)
    {
        const long tolerance = 5;

        var database = Available();
        long first = 0;
        long last = 0;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Five thousand, listed");
            var (query, _) = await SeedAtScaleAsync(database, owner, projectId, sort, filtered);

            var walked = await WalkAsync(owner, owner.SpaceId, projectId, query);

            Assert.True(walked.Count >= 3, $"The walk is {walked.Count} pages; the case is about depth.");

            var lastCursor = walked[^2].NextAfter;

            // Warm both statements' plans: a first execution carries compilation's reads (2.9 PG-11).
            _ = PageOf(await PageAsync(owner, owner.SpaceId, projectId, query));
            _ = PageOf(await PageAsync(owner, owner.SpaceId, projectId, query, lastCursor));

            first = await LogicalReadsOfAsync(database, async () => _ = PageOf(await PageAsync(owner, owner.SpaceId, projectId, query)));
            last = await LogicalReadsOfAsync(database, async () => _ = PageOf(await PageAsync(owner, owner.SpaceId, projectId, query, lastCursor)));
        });

        TestContext.Current.TestOutputHelper?.WriteLine($"{sort}{(filtered ? " (heaviest filter)" : string.Empty)}: first page {first}, last page {last} logical reads");

        Assert.True(first > 0, "The first page reported no logical reads on the Task table.");
        Assert.True(last <= first + tolerance, $"{sort}: the last page read {last} logical pages against the first's {first} (AC10).");
    }

    /// <summary>
    /// LV-19 (AC6, P-4 at run time, Task 3 (d) "parameter types"; P0) - each sort's page is served by a seek on its own
    /// composite index, and no plan converts the sort column implicitly: <c>DueDate</c> is compared as
    /// <c>datetimeoffset</c>, <c>FirstLabelName</c> as its own <c>nvarchar(50)</c>, <c>StatusId</c> as
    /// <c>uniqueidentifier</c> (2.9 PG-12's check).
    /// </summary>
    /// <param name="sort">The sort.</param>
    /// <param name="index">The composite index story Task 2 (b) names for it, by EF's naming convention.</param>
    /// <param name="column">The sort column.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(SortStatus, "IX_Task_SpaceId_ProjectId_StatusId_Id", "StatusId")]
    [InlineData(SortDueDate, "IX_Task_SpaceId_ProjectId_DueDate_Id", "DueDate")]
    [InlineData(SortLabel, "IX_Task_SpaceId_ProjectId_FirstLabelName_Id", "FirstLabelName")]
    [Trait("Requirement", "AR-22")]
    public async Task Each_sort_seeks_its_own_composite_index_and_never_converts_its_column(string sort, string index, string column)
    {
        var database = Available();
        IReadOnlyList<string> plans = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Seeks, never scans");
            var (query, _) = await SeedAtScaleAsync(database, owner, projectId, sort, filtered: false, count: 600);

            var firstPage = PageOf(await PageAsync(owner, owner.SpaceId, projectId, query));

            _ = PageOf(await PageAsync(owner, owner.SpaceId, projectId, query, firstPage.NextAfter));
            plans = await PlansNamingAsync(database, index);
        });

        Assert.NotEmpty(plans);
        Assert.Contains(plans, plan => plan.Contains("SeekPredicates", StringComparison.Ordinal));
        Assert.All(plans, plan => Assert.False(ConvertsImplicitly(plan, column), $"A plan converts {column} implicitly: the parameter is not the column's own type."));
    }

    /// <summary>
    /// LV-20 (Task 2 (c), AC13; story 1.10's expand-only rule) - the sort-key migration is safe on data that exists before
    /// it: taken down to the migration before it, the column and the three indexes are gone and nothing else; brought up
    /// again, the backfill gives every existing labelled Task its first chip's name - whatever Space it is in, although a
    /// migration's connection names no Space - leaves an unlabelled Task <c>NULL</c>, and leaves the Space policy on.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-2")]
    public async Task The_sort_key_migration_backfills_existing_Labels_and_its_Down_removes_exactly_what_it_added()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var labelled = Guid.Empty;
        var bare = Guid.Empty;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projectId = await CreateProjectAsync(owner, "Backfilled");

            labelled = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Labelled before"));
            bare = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Bare before"));

            var zulu = await SeedAsync(owner, owner.SpaceId, "Zulu " + Unique());
            var alpha = await SeedAsync(owner, owner.SpaceId, "Alpha " + Unique());

            await SetLabelsAsync(owner, owner.SpaceId, labelled, zulu, alpha);
        });

        var expected = await FirstAppliedLabelNameAsync(database, owner!.SpaceId, labelled);
        IReadOnlyList<string> afterDown;

        try
        {
            await using (var context = database.CreateContext())
            {
                await context.GetService<IMigrator>().MigrateAsync(MigrationBeforeTheSortKeys, cancellationToken: cancellationToken);
            }

            afterDown = await SortKeySchemaAsync(database);
        }
        finally
        {
            // Back up whatever happened, so every later case in this class's database meets the current schema.
            await database.MigrateAsync(cancellationToken);
        }

        Assert.Empty(afterDown);
        Assert.Equal(SortKeySchema, await SortKeySchemaAsync(database));
        Assert.NotNull(expected);
        Assert.Equal(expected, await FirstLabelNameAsync(database, owner.SpaceId, labelled));
        Assert.Null(await FirstLabelNameAsync(database, owner.SpaceId, bare));
        Assert.True(await SpacePolicyIsOnAsync(database), "The migration left the Space isolation policy switched off.");
    }

    /// <summary>
    /// Seeds <paramref name="count"/> Tasks for the cost cases: Statuses by <c>N % 3</c>; due dates on 7 tied days with
    /// about a third <c>NULL</c>; Labels by SQL - Alpha on <c>N % 5</c> in {0, 1}, Bravo and Charlie on 2, Charlie on 3,
    /// none on 4 - with the key backfilled in the migration's shape. Answers the query and, when filtered, the heaviest
    /// combination: two of three Statuses, two Labels, and a four-day range.
    /// </summary>
    private static async Task<(ListQuery Query, string Predicate)> SeedAtScaleAsync(RegistrationDatabase database, Actor owner, Guid projectId, string sort, bool filtered, int count = Scale)
    {
        var statusIds = (await StatusesAsync(database, owner.SpaceId)).Select(status => status.Id).ToList();
        var alpha = await SeedAsync(owner, owner.SpaceId, "Alpha " + Unique());
        var bravo = await SeedAsync(owner, owner.SpaceId, "Bravo " + Unique());
        var charlie = await SeedAsync(owner, owner.SpaceId, "Charlie " + Unique());

        await SeedListAsync(database, owner.SpaceId, projectId, statusIds, count);
        await SetDueDatesAsync(database, owner.SpaceId, projectId, distinctDays: 7);
        await ApplyLabelAsync(database, owner.SpaceId, projectId, alpha, modulus: 5, remainder: 0);
        await ApplyLabelAsync(database, owner.SpaceId, projectId, alpha, modulus: 5, remainder: 1);
        await ApplyLabelAsync(database, owner.SpaceId, projectId, bravo, modulus: 5, remainder: 2);
        await ApplyLabelAsync(database, owner.SpaceId, projectId, charlie, modulus: 5, remainder: 2);
        await ApplyLabelAsync(database, owner.SpaceId, projectId, charlie, modulus: 5, remainder: 3);
        await BackfillFirstLabelNamesAsync(database, owner.SpaceId, projectId);

        if (!filtered)
        {
            return (ListQuery.By(sort), string.Empty);
        }

        var from = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(4);

        return (
            new ListQuery(sort, [statusIds[0], statusIds[1]], [alpha, charlie], DueFrom: from, DueTo: to),
            $"t.StatusId IN ('{statusIds[0]:D}', '{statusIds[1]:D}') AND t.DueDate >= '{from:O}' AND t.DueDate < '{to:O}'");
    }

    /// <summary>
    /// The Project's Task ids by title ordinal (<c>Listed 1</c>, <c>Listed 2</c>, …), read under the Space's key - the
    /// handle a case uses to give particular Tasks particular values. Not an order any sort serves.
    /// </summary>
    private static async Task<List<Guid>> IdsOfAsync(RegistrationDatabase database, Guid spaceId, Guid projectId) =>
        [.. (await TaskRowsAsync(database, spaceId))
            .Where(row => row.ProjectId == projectId)
            .OrderBy(row => row.PositionKey, StringComparer.Ordinal)
            .Select(row => row.Id)];

    /// <summary>
    /// P-3's assertion: every page at most a page, every page the true total, a cursor on every page but the last, every
    /// Task exactly once, and the walk exactly the order SQL Server computed.
    /// </summary>
    private static void AssertWalk(IReadOnlyList<ListPage> pages, IReadOnlyList<Guid> expected)
    {
        var walked = pages.SelectMany(page => page.Tasks).Select(task => task.Id).ToList();

        Assert.All(pages, page => Assert.Equal(expected.Count, page.Total));
        Assert.All(pages, page => Assert.InRange(page.Tasks.Count, expected.Count == 0 ? 0 : 1, PageSize));
        Assert.All(pages.Take(pages.Count - 1), page => Assert.NotNull(page.NextAfter));
        Assert.Null(pages[^1].NextAfter);
        Assert.Equal(walked.Count, walked.Distinct().Count());
        Assert.Equal(expected, walked);
    }

    /// <summary>
    /// What story 2.10's migration adds to <c>dbo.[Task]</c>, by name, as <see cref="SortKeySchemaAsync"/> reads it.
    /// </summary>
    private static readonly string[] SortKeySchema =
    [
        "column FirstLabelName",
        "index IX_Task_SpaceId_ProjectId_DueDate_Id",
        "index IX_Task_SpaceId_ProjectId_FirstLabelName_Id",
        "index IX_Task_SpaceId_ProjectId_StatusId_Id",
    ];

    /// <summary>
    /// The migration before <c>ListViewSortKeys</c> - the schema the revision before story 2.10 runs against.
    /// </summary>
    private const string MigrationBeforeTheSortKeys = "20261028123715_TaskLockEscalationDisabled";

    /// <summary>
    /// Which of story 2.10's column and indexes <c>dbo.[Task]</c> holds now, read from the catalogue.
    /// </summary>
    private static async Task<IReadOnlyList<string>> SortKeySchemaAsync(RegistrationDatabase database)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT CONCAT('column ', c.name) FROM sys.columns c WHERE c.object_id = OBJECT_ID(N'dbo.[Task]') AND c.name = N'FirstLabelName'
            UNION ALL
            SELECT CONCAT('index ', i.name) FROM sys.indexes i
            WHERE i.object_id = OBJECT_ID(N'dbo.[Task]')
              AND i.name IN (N'IX_Task_SpaceId_ProjectId_StatusId_Id', N'IX_Task_SpaceId_ProjectId_DueDate_Id', N'IX_Task_SpaceId_ProjectId_FirstLabelName_Id')
            """;

        var found = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            found.Add(reader.GetString(0));
        }

        return [.. found.Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// Whether the Space isolation policy is enabled.
    /// </summary>
    private static async Task<bool> SpacePolicyIsOnAsync(RegistrationDatabase database)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT is_enabled FROM sys.security_policies WHERE name = N'SpaceIsolationPolicy'";

        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) is true;
    }

    private static string Unique() => Guid.NewGuid().ToString("N")[..8];

    private static List<string> Names(JsonElement element) =>
        [.. element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)];

    private RegistrationDatabase Available()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        return fixture.Database!;
    }
}
