using System.Data.Common;
using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Projects;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using Yello.Tests.Slices.Tasks;
using static Yello.Tests.Slices.Boards.BoardPageWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Boards;

/// <summary>
/// Story 2.9 over the wire, PR tier: the Board's first page per column and the column-page route (Q1 (a)), keyset
/// seek on the position key at 5,000 Tasks proved by logical reads rather than by time, every Task reachable exactly
/// once, the page contract and its refusals, reads that never wait on writers (RCSI), locks at scale that stay inside
/// one Space, and the full-gap refusal. AC1, AC2 (deterministic half), AC4, AC5, AC9, AC10 (server half), AC13, AC17,
/// AC18; FR-28, AR-22, AD-29, AD-15, AD-19, NFR-8; test design P-2 (deterministic half), I-3. Written red by the story
/// 2.9 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>One class, one database</b> on the Slices suite's one engine (no new engine, no new fixture type -
/// <c>ContainerTopologyGateTests</c>' budget of 5 is unchanged). 5,000 Tasks are one set-based statement
/// (<see cref="TaskWire.SeedTasksAsync"/>, milliseconds), and 15,000 Label applications one more per Label
/// (<see cref="BoardPageWire.ApplyLabelsToEveryTaskAsync"/>): a few MB of a 2 GB engine. Nothing here measures time, so
/// the class needs no serial collection; the timed Nightly and Weekly cases are <c>BoardScaleTimingTests</c> and
/// <c>EnvelopeLoadTests</c>.
/// </para>
/// <para>
/// <b>Races are held, never hoped for</b> (<c>TESTING-CONVENTIONS.md</c>): every "does not wait" case holds the other
/// writer's transaction open on a <c>WireDatabase</c>-shaped connection, sends the request, and waits until it is blocked
/// on a lock or has answered (<see cref="LabelWire.WaitUntilBlockedOrAnsweredAsync"/>); answering while the other side is
/// still held is the pass, being blocked is the failure. The held transaction is rolled back in a <c>finally</c>.
/// </para>
/// <para>
/// <b>Scaffold constants</b> (<see cref="BoardPageWire"/>): swap them for the contract members when Task 2 and Task 10
/// add them, and change nothing else.
/// </para>
/// </remarks>
/// <param name="fixture">This class's migrated database on the shared engine.</param>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-28")]
[Trait("Requirement", "AR-22")]
[Trait("Requirement", "AD-29")]
public sealed class BoardPagingTests(MigratedDatabaseFixture fixture) : IClassFixture<MigratedDatabaseFixture>
{
    private const int Scale = 5_000;

    private const string OneLongerThanTheLongestKey = "one longer than the longest key";

    private static readonly string[] ColumnProperties = ["name", "nextAfter", "statusId", "tasks", "total"];
    private static readonly string[] PageProperties = ["nextAfter", "statusId", "tasks", "total"];

    /// <summary>
    /// PG-01 (AC9, AC4) - the Board answers each column's first page: at most 50 Tasks, the true total, and a cursor that
    /// is the last returned Task's key exactly when a Task follows. Columns of 120, exactly 50 (the boundary: full page, no
    /// cursor) and 3.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - the Board read returns every Task and no cursor until story Task 2 pages it")]
    public async Task The_Board_answers_each_columns_first_page_with_its_true_total_and_a_cursor_only_when_more_follow()
    {
        var database = Available();
        WireResponse? board = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Paged");
            var statuses = await StatusesAsync(database, owner.SpaceId);

            await SeedTasksAsync(database, owner.SpaceId, projectId, statuses[0].Id, 120);
            await SeedTasksAsync(database, owner.SpaceId, projectId, statuses[1].Id, PageSize);
            await SeedTasksAsync(database, owner.SpaceId, projectId, statuses[2].Id, 3);

            board = await BoardAsync(owner, owner.SpaceId, projectId);
        });

        var columns = ColumnsOf(board!);

        Assert.Equal([PageSize, PageSize, 3], columns.Select(column => column.Tasks.Count));
        Assert.Equal([120, PageSize, 3], columns.Select(column => column.Total));
        Assert.Equal(columns[0].Tasks[^1].PositionKey, columns[0].NextAfter);
        Assert.Null(columns[1].NextAfter);
        Assert.Null(columns[2].NextAfter);
    }

    /// <summary>
    /// PG-02 (AC9, AD-19) - a Board column keeps every field it had and gains only the cursor: exactly
    /// <c>{name, nextAfter, statusId, tasks, total}</c>. The successor of <c>BoardEndpointTests</c>' column pin, which
    /// widens by exactly <c>nextAfter</c> in the same change.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - BoardColumnResponse has no NextAfter until story Task 2 appends it")]
    [Trait("Requirement", "AD-19")]
    public async Task A_Board_column_keeps_every_field_it_had_and_gains_only_the_cursor()
    {
        var database = Available();
        WireResponse? board = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Appended");

            _ = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "One"));
            board = await BoardAsync(owner, owner.SpaceId, projectId);
        });

        using var document = JsonDocument.Parse(board!.Body);

        Assert.All(document.RootElement.GetProperty("columns").EnumerateArray(), column => Assert.Equal(ColumnProperties, Names(column)));
    }

    /// <summary>
    /// PG-03 (AC1, AC4, AC5, AC9; P0) - a column of 5,000 is walked from the Board's first page by its cursor: 100 pages,
    /// each answering the true total of 5,000 and at most 50 Tasks, every Task exactly once, keys strictly ascending in
    /// ordinal order across page boundaries, the last cursor <c>null</c> - and the walk is the database's own order.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 - no column-page route and no cursor until story Task 2")]
    [Trait("Requirement", "NFR-8")]
    public async Task Walking_a_column_of_5000_reads_100_pages_and_every_Task_exactly_once_in_key_order()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        Guid statusId = default;
        IReadOnlyList<ColumnPage> pages = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = await CreateProjectAsync(owner, "Five thousand");
            statusId = (await StatusesAsync(database, owner.SpaceId))[0].Id;

            await SeedTasksAsync(database, owner.SpaceId, projectId, statusId, Scale);

            var first = ColumnsOf(await BoardAsync(owner, owner.SpaceId, projectId))[0];

            pages = await WalkAsync(owner, owner.SpaceId, projectId, first);
        });

        var walked = pages.SelectMany(page => page.Tasks).ToList();
        var stored = (await TaskRowsAsync(database, owner!.SpaceId))
            .Where(row => row.ProjectId == projectId && row.StatusId == statusId)
            .OrderBy(row => row.PositionKey, StringComparer.Ordinal)
            .Select(row => row.Id)
            .ToList();

        Assert.Equal(Scale / PageSize, pages.Count);
        Assert.All(pages, page => Assert.Equal(Scale, page.Total));
        Assert.All(pages, page => Assert.InRange(page.Tasks.Count, 1, PageSize));
        Assert.Null(pages[^1].NextAfter);
        Assert.All(pages.Take(pages.Count - 1), page => Assert.Equal(page.Tasks[^1].PositionKey, page.NextAfter));
        Assert.Equal(walked.Count, walked.Select(task => task.Id).Distinct().Count());
        Assert.True(
            walked.Zip(walked.Skip(1)).All(pair => string.CompareOrdinal(pair.First.PositionKey, pair.Second.PositionKey) < 0),
            "The walked keys are not strictly ascending in ordinal order.");
        Assert.Equal(stored, walked.Select(task => task.Id));
    }

    /// <summary>
    /// PG-04 (AC5) - 5,000 Tasks over three Statuses (2,600, 1,700, 700): walking every column from the Board finds every
    /// Task of the Project, each in exactly one column, the column of its own Status.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC5 - no column-page route until story Task 2")]
    [Trait("Requirement", "NFR-8")]
    public async Task Five_thousand_Tasks_over_three_Statuses_each_appear_in_exactly_one_column()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        var placed = new List<(Guid ColumnStatus, PagedTask Task)>();
        int[] split = [2_600, 1_700, 700];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = await CreateProjectAsync(owner, "Split");

            var statuses = await StatusesAsync(database, owner.SpaceId);

            for (var index = 0; index < statuses.Count; index++)
            {
                await SeedTasksAsync(database, owner.SpaceId, projectId, statuses[index].Id, split[index]);
            }

            foreach (var first in ColumnsOf(await BoardAsync(owner, owner.SpaceId, projectId)))
            {
                foreach (var page in await WalkAsync(owner, owner.SpaceId, projectId, first))
                {
                    placed.AddRange(page.Tasks.Select(task => (first.StatusId, task)));
                }
            }
        });

        var own = (await TaskRowsAsync(database, owner!.SpaceId)).Where(row => row.ProjectId == projectId).ToList();

        Assert.Equal(Scale, own.Count);
        Assert.Equal(own.Count, placed.Count);
        Assert.Equal(own.Select(row => row.Id).Order(), placed.Select(entry => entry.Task.Id).Order());
        Assert.All(placed, entry => Assert.Equal(entry.ColumnStatus, entry.Task.StatusId));
    }

    /// <summary>
    /// PG-05 (AC9) - a column page answers exactly <c>{nextAfter, statusId, tasks, total}</c>, each Task in the Board's own
    /// Task shape, and the Space's id appears nowhere in it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - no column-page route until story Task 2")]
    [Trait("Requirement", "AD-19")]
    public async Task A_column_page_answers_exactly_the_contracts_properties_and_no_SpaceId()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? page = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projectId = await CreateProjectAsync(owner, "Shaped page");
            var statusId = (await StatusesAsync(database, owner.SpaceId))[0].Id;

            await SeedTasksAsync(database, owner.SpaceId, projectId, statusId, PageSize + 1);

            var first = ColumnsOf(await BoardAsync(owner, owner.SpaceId, projectId))[0];

            page = await PageAsync(owner, owner.SpaceId, projectId, statusId, first.NextAfter);
        });

        Assert.Equal(HttpStatusCode.OK, page?.StatusCode);

        using var document = JsonDocument.Parse(page!.Body);

        Assert.Equal(PageProperties, Names(document.RootElement));
        Assert.Equal(["createdAt", "dueDate", "id", "labelIds", "positionKey", "statusId", "title"], Names(Assert.Single(document.RootElement.GetProperty("tasks").EnumerateArray())));
        Assert.DoesNotContain(owner!.SpaceId.ToString("D"), page.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// PG-06 (AC9) - a cursor outside <c>PositionKey</c>'s base-62 alphabet or longer than its 128 characters is refused
    /// <c>400 invalid-board-cursor</c>, machine-readably, and records no refusal. Anchor: a well-formed cursor is served.
    /// </summary>
    /// <param name="cursor">A malformed cursor.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC9 - no column-page route and no invalid-board-cursor type until story Task 2")]
    [InlineData("V 000")]
    [InlineData("V000-")]
    [InlineData("Vé00")]
    [InlineData(OneLongerThanTheLongestKey)]
    public async Task A_malformed_cursor_is_refused_400_invalid_board_cursor(string cursor)
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? refused = null;
        WireResponse? anchor = null;
        IReadOnlyList<string> refusals = [];

        // 129 characters of the alphabet: one longer than PositionKey.MaxLength.
        var sent = string.Equals(cursor, OneLongerThanTheLongestKey, StringComparison.Ordinal) ? new string('V', 129) : cursor;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projectId = await CreateProjectAsync(owner, "Cursor rules");
            var statusId = (await StatusesAsync(database, owner.SpaceId))[0].Id;

            refused = await PageAsync(owner, owner.SpaceId, projectId, statusId, sent);
            refusals = await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId);
            anchor = await PageAsync(owner, owner.SpaceId, projectId, statusId, "V0000");
        });

        Assert.True(refused?.StatusCode == HttpStatusCode.BadRequest, $"{refused?.StatusCode} {refused?.Body}");
        Assert.Equal(InvalidBoardCursor, Problem(refused!)?.Type);
        Assert.Empty(refusals);
        Assert.Equal(HttpStatusCode.OK, anchor?.StatusCode);
    }

    /// <summary>
    /// PG-07 (AC9, AD-3, AD-20) - a Status outside the Project's effective set, and a Project id that matches nothing in
    /// the Space, each answer the boundary's <c>404 not-available</c>, byte-identical to a Space that does not exist, and
    /// record nothing. Anchor: the Project's own Status is served.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - no column-page route until story Task 2")]
    [Trait("Requirement", "AD-3")]
    public async Task A_Status_outside_the_effective_set_or_an_unknown_Project_answers_the_boundary_404_and_records_nothing()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? foreignStatus = null;
        WireResponse? unknownProject = null;
        WireResponse? absentSpace = null;
        WireResponse? anchor = null;
        IReadOnlyList<string> refusals = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projectId = await CreateProjectAsync(owner, "Bounded");
            var statusId = (await StatusesAsync(database, owner.SpaceId))[0].Id;

            foreignStatus = await PageAsync(owner, owner.SpaceId, projectId, Guid.NewGuid());
            unknownProject = await PageAsync(owner, owner.SpaceId, Guid.NewGuid(), statusId);
            refusals = await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId);
            absentSpace = await PageAsync(owner, Guid.NewGuid(), projectId, statusId);
            anchor = await PageAsync(owner, owner.SpaceId, projectId, statusId);
        });

        foreach (var answer in new[] { foreignStatus!, unknownProject! })
        {
            Assert.Equal(HttpStatusCode.NotFound, answer.StatusCode);
            Assert.Equal(absentSpace!.ContentType, answer.ContentType);
            Assert.Equal(absentSpace.Body, answer.Body);
            Assert.Equal(absentSpace.HeaderNames, answer.HeaderNames);
            Assert.Equal(ProblemTypes.NotAvailable, Problem(answer)?.Type);
        }

        Assert.Empty(refusals);
        Assert.Equal(HttpStatusCode.OK, anchor?.StatusCode);
    }

    /// <summary>
    /// PG-08 (AC9, AD-4) - a Viewer is served exactly the page a Member is, on its Session and on its Token: the page route
    /// requires <c>ReadProjectsAndTasks</c>, which every Role holds.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - no column-page route until story Task 2")]
    [Trait("Requirement", "AD-4")]
    public async Task A_Viewer_is_served_the_same_page_as_a_Member_on_both_surfaces()
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

            var projectId = await CreateProjectAsync(owner, "Seen alike, paged");
            var statusId = (await StatusesAsync(database, owner.SpaceId))[0].Id;

            await SeedTasksAsync(database, owner.SpaceId, projectId, statusId, PageSize + 5);

            var token = await IssueTokenAsync(viewer, owner.SpaceId);
            var cursor = ColumnsOf(await BoardAsync(memberActor, owner.SpaceId, projectId))[0].NextAfter;

            member = await PageAsync(memberActor, owner.SpaceId, projectId, statusId, cursor);
            viewerSession = await PageAsync(viewer, owner.SpaceId, projectId, statusId, cursor);
            viewerToken = await viewer.Session.GetWithTokenAsync(ColumnOf(owner.SpaceId, projectId, statusId, cursor), token);
        });

        Assert.Equal(HttpStatusCode.OK, member?.StatusCode);
        Assert.Equal(5, PageOf(member!).Tasks.Count);
        Assert.Equal(member!.Body, viewerSession?.Body);
        Assert.Equal(member.Body, viewerToken?.Body);
    }

    /// <summary>
    /// PG-09 (AC9, Q1 (a)) - the server owns the page size: a <c>limit</c> or <c>pageSize</c> the client sends is not a
    /// lever on an unbounded read, and the page still holds at most 50.
    /// </summary>
    /// <param name="parameter">A page-size parameter a client might try.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC9 - no column-page route until story Task 2")]
    [InlineData("limit")]
    [InlineData("pageSize")]
    [InlineData("size")]
    public async Task A_client_cannot_choose_the_page_size(string parameter)
    {
        var database = Available();
        WireResponse? page = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Sized by the server");
            var statusId = (await StatusesAsync(database, owner.SpaceId))[0].Id;

            await SeedTasksAsync(database, owner.SpaceId, projectId, statusId, 300);

            page = await owner.Session.GetAsync(ColumnOf(owner.SpaceId, projectId, statusId) + $"?{parameter}=500");
        });

        var served = PageOf(page!);

        Assert.Equal(PageSize, served.Tasks.Count);
        Assert.Equal(300, served.Total);
        Assert.NotNull(served.NextAfter);
    }

    /// <summary>
    /// PG-10 (AC10 server half, AC1) - the cursor is a key, not an id: when the Task whose key it is has been deleted, the
    /// next page still starts at the first key after it, and no Task is skipped or repeated.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC10 - no column-page route until story Task 2")]
    public async Task A_cursor_naming_a_key_no_Task_holds_any_more_still_seeks_to_the_next_key()
    {
        var database = Available();
        ColumnPage? first = null;
        ColumnPage? next = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Cursor outlives its Task");
            var statusId = (await StatusesAsync(database, owner.SpaceId))[0].Id;

            await SeedTasksAsync(database, owner.SpaceId, projectId, statusId, 120);

            first = ColumnsOf(await BoardAsync(owner, owner.SpaceId, projectId))[0];

            Assert.Equal(HttpStatusCode.NoContent, (await DeleteWire.DeleteTaskAsync(owner, owner.SpaceId, first.Tasks[^1].Id)).StatusCode);

            next = PageOf(await PageAsync(owner, owner.SpaceId, projectId, statusId, first.NextAfter));
        });

        Assert.Equal(119, next!.Total);
        Assert.Equal(PageSize, next.Tasks.Count);
        Assert.True(string.CompareOrdinal(first!.NextAfter, next.Tasks[0].PositionKey) < 0, "The page after a deleted cursor does not start after its key.");
        Assert.Empty(next.Tasks.Select(task => task.Id).Intersect(first.Tasks.Select(task => task.Id)));
    }

    /// <summary>
    /// PG-11 (AC1, AC2 deterministic half; P-2; P0) - the page seek reads as much at the end of a column of 5,000 as near
    /// its start: the logical reads of the product's own statement for page 2 and for page 100 differ by at most a small
    /// constant (the B-tree's depth is the same; only the cursor moves). An <c>OFFSET</c> or <c>Skip()</c> reads every
    /// row it skips, so page 100's reads grow by the 4,950 rows above it.
    /// </summary>
    /// <remarks>
    /// <b>Plant owed by the dev phase</b> (story Task 3): an <c>OFFSET</c>/<c>Skip()</c> implementation - page 100's reads
    /// must grow and this case fail; and an <c>nvarchar</c> cursor parameter - PG-12 must fail. Restored in a
    /// <c>finally</c>, never by <c>git checkout</c>.
    /// </remarks>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 - no keyset page seek runs until story Task 2")]
    [Trait("Requirement", "NFR-5")]
    public async Task The_page_seek_reads_as_much_at_the_end_of_a_column_as_near_its_start()
    {
        const long smallConstant = 2;

        var database = Available();
        long nearTheStart = 0;
        long atTheEnd = 0;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Depth independent");
            var statusId = (await StatusesAsync(database, owner.SpaceId))[0].Id;

            await SeedTasksAsync(database, owner.SpaceId, projectId, statusId, Scale);
            _ = await ApplyLabelsToEveryTaskAsync(database, owner, projectId);

            var first = ColumnsOf(await BoardAsync(owner, owner.SpaceId, projectId))[0];

            _ = PageOf(await PageAsync(owner, owner.SpaceId, projectId, statusId, first.NextAfter));
            nearTheStart = await LastLogicalReadsOfThePageSeekAsync(database);

            // The cursor of page 99 is the key of the 4,950th Task: "0" then 04950 (TaskWire.SeedTasksAsync's keys).
            var last = PageOf(await PageAsync(owner, owner.SpaceId, projectId, statusId, "004950"));

            atTheEnd = await LastLogicalReadsOfThePageSeekAsync(database);

            Assert.Equal(PageSize, last.Tasks.Count);
            Assert.Null(last.NextAfter);
        });

        Assert.True(nearTheStart > 0, "The page seek reported no logical reads.");
        Assert.True(
            Math.Abs(atTheEnd - nearTheStart) <= smallConstant,
            $"Page 2 read {nearTheStart} pages and page 100 read {atTheEnd}: the seek's work depends on its depth (AC1).");
    }

    /// <summary>
    /// PG-12 (AC1, AD-15; story Task 2 (b), (c)) - the page seek's plan seeks
    /// <c>IX_Task_SpaceId_ProjectId_StatusId_PositionKey</c> and never converts the position key: the cursor parameter is
    /// <c>varchar(128)</c>, the column's own type under BIN2.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 - no keyset page seek runs until story Task 2")]
    [Trait("Requirement", "AD-15")]
    public async Task The_page_seek_uses_the_position_index_and_never_converts_the_key()
    {
        var database = Available();
        var plan = string.Empty;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Seeks, never scans");
            var statusId = (await StatusesAsync(database, owner.SpaceId))[0].Id;

            await SeedTasksAsync(database, owner.SpaceId, projectId, statusId, PageSize * 3);

            _ = PageOf(await PageAsync(owner, owner.SpaceId, projectId, statusId, "000050"));
            plan = await PlanOfThePageSeekAsync(database);
        });

        Assert.Contains("IX_Task_SpaceId_ProjectId_StatusId_PositionKey", plan, StringComparison.Ordinal);
        Assert.Contains("SeekPredicates", plan, StringComparison.Ordinal);
        Assert.False(ConvertsThePositionKeyImplicitly(plan), "The plan converts PositionKey implicitly: the cursor parameter is not varchar(128).");
        Assert.DoesNotContain("OFFSET", plan, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// PG-13 (AC9; <c>deferred-work.md:523</c>'s measurement half) - every paged Task carries its Labels, so the Labels read
    /// is inside the measured read: at 5,000 Tasks with three Labels each, the first page and the last page each carry
    /// exactly the three applied.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - no column-page route until story Task 2")]
    public async Task Each_paged_Task_carries_its_Labels_at_the_first_page_and_the_last()
    {
        var database = Available();
        IReadOnlyList<Guid> labels = [];
        ColumnPage? first = null;
        ColumnPage? last = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Labelled at scale");
            var statusId = (await StatusesAsync(database, owner.SpaceId))[0].Id;

            await SeedTasksAsync(database, owner.SpaceId, projectId, statusId, Scale);
            labels = await ApplyLabelsToEveryTaskAsync(database, owner, projectId);

            first = ColumnsOf(await BoardAsync(owner, owner.SpaceId, projectId))[0];
            last = PageOf(await PageAsync(owner, owner.SpaceId, projectId, statusId, "004950"));
        });

        Assert.All(first!.Tasks.Concat(last!.Tasks), task => Assert.Equal(labels.Order(), task.LabelIds.Order()));
    }

    /// <summary>
    /// PG-14 (AC13; <c>deferred-work.md:495</c>, <c>:547</c>, <c>:548</c>) - with a deletion of a 5,000-Task Project held
    /// uncommitted in the Space, a Board read of another Project and the Project listing both answer while it is still
    /// held: <c>READ_COMMITTED_SNAPSHOT</c> is on, so a read never waits on a writer.
    /// </summary>
    /// <remarks>
    /// Plant owed by the dev phase (story Task 8): RCSI off for this database - the reads wait (<c>LCK_M_S</c>) and the case
    /// fails.
    /// </remarks>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC13 - READ_COMMITTED_SNAPSHOT is off until story Task 8's migration")]
    [Trait("Requirement", "NFR-5")]
    public async Task A_Board_read_and_the_Project_listing_do_not_wait_on_an_uncommitted_Project_deletion()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        WireResponse? board = null;
        WireResponse? listing = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var doomed = await CreateProjectAsync(owner, "Being deleted");
            var other = await CreateProjectAsync(owner, "Being read");
            var statusId = (await StatusesAsync(database, owner.SpaceId))[0].Id;

            await SeedTasksAsync(database, owner.SpaceId, doomed, statusId, Scale);
            _ = await ApplyLabelsToEveryTaskAsync(database, owner, doomed);
            await SeedTasksAsync(database, owner.SpaceId, other, statusId, 3);

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, other)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            try
            {
                await DeleteWire.DeleteProjectRowAsync(holder, held, doomed, cancellationToken);

                var reading = BoardAsync(owner, owner.SpaceId, other);

                await LabelWire.WaitUntilBlockedOrAnsweredAsync(observer, 1, reading, cancellationToken);
                Assert.True(reading.IsCompleted, "The Board read waited on the uncommitted Project deletion (AC13).");
                board = await reading;

                var listingRead = owner.Session.GetAsync(ProjectRoutes.ListIn(owner.SpaceId));

                await LabelWire.WaitUntilBlockedOrAnsweredAsync(observer, 1, listingRead, cancellationToken);
                Assert.True(listingRead.IsCompleted, "The Project listing waited on the uncommitted Project deletion (AC13).");
                listing = await listingRead;
            }
            finally
            {
                await held.RollbackAsync(CancellationToken.None);
            }
        });

        Assert.Equal(HttpStatusCode.OK, board?.StatusCode);
        Assert.Equal(HttpStatusCode.OK, listing?.StatusCode);
    }

    /// <summary>
    /// PG-15 (AC13; <c>deferred-work.md:313</c>) - with another Account's presence insert held uncommitted in the Space, the
    /// Owner's Board read answers while it is still held: the presence count does not wait behind it.
    /// </summary>
    /// <remarks>
    /// If the presence count is taken under <c>UPDLOCK, HOLDLOCK</c> (the presence bound, <c>:298</c>), RCSI does not
    /// release it and this case stays red after Task 8: record that, and the decision, rather than loosening the case.
    /// <b>Seed constraints checked</b>: <c>dbo.[SpacePresence] (SpaceId, AccountId, ExpiresAt)</c>, key
    /// <c>(SpaceId, AccountId)</c> - the member has never been present in the Owner's Space, so the key is free;
    /// <c>FK SpaceId</c> to <c>Space</c> holds (the Owner's Personal Space); the row is written under that Space's key.
    /// </remarks>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC13 - READ_COMMITTED_SNAPSHOT is off until story Task 8's migration")]
    public async Task A_Board_read_does_not_wait_on_another_Accounts_uncommitted_presence()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        WireResponse? board = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var member = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Read beside a presence");

            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");
            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, projectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            try
            {
                await using (DbCommand insert = holder.CreateCommand())
                {
                    insert.Transaction = held;
                    insert.CommandText = "INSERT dbo.[SpacePresence] (SpaceId, AccountId, ExpiresAt) VALUES (@space, @account, DATEADD(HOUR, 1, SYSDATETIMEOFFSET()))";
                    insert.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@space", owner.SpaceId));
                    insert.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@account", member.AccountId));

                    Assert.Equal(1, await insert.ExecuteNonQueryAsync(cancellationToken));
                }

                var reading = BoardAsync(owner, owner.SpaceId, projectId);

                await LabelWire.WaitUntilBlockedOrAnsweredAsync(observer, 1, reading, cancellationToken);
                Assert.True(reading.IsCompleted, "The Board read waited on another Account's uncommitted presence (AC13, :313).");
                board = await reading;
            }
            finally
            {
                await held.RollbackAsync(CancellationToken.None);
            }
        });

        Assert.Equal(HttpStatusCode.OK, board?.StatusCode);
    }

    /// <summary>
    /// PG-16 (AC17; <c>deferred-work.md:541</c>) - with Space A's deletion of a 5,000-Task Project, three Labels on each
    /// Task, held uncommitted in the product's own statement shape (<see cref="DeleteWire.DeleteProjectRowAsync"/>), a Task
    /// creation in Space B answers <c>201</c> while A is still held: the deletion escalated to no table lock that another
    /// Space's writer queues on. Deterministic - A held, B completes - never timed.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC17 - a 5,000-Task deletion may escalate to an OBJECT X lock until story Task 9 / Q15 decides")]
    [Trait("Requirement", "NFR-1")]
    public async Task A_Task_write_in_another_Space_completes_while_a_5000_Task_Project_deletion_is_held()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        WireResponse? created = null;
        IReadOnlyList<string> objectLocks = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var spaceA = await RegisterAsync(client, database);
            var spaceB = await RegisterAsync(client, database);
            var doomed = await CreateProjectAsync(spaceA, "Large and leaving");
            var elsewhere = await CreateProjectAsync(spaceB, "Unaffected");
            var statusId = (await StatusesAsync(database, spaceA.SpaceId))[0].Id;

            await SeedTasksAsync(database, spaceA.SpaceId, doomed, statusId, Scale);
            _ = await ApplyLabelsToEveryTaskAsync(database, spaceA, doomed);
            _ = IdOf(await CreateTaskAsync(spaceB, spaceB.SpaceId, elsewhere, "Warm"));

            await using var holder = await database.OpenForSpaceAsync(spaceA.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            try
            {
                await DeleteWire.DeleteProjectRowAsync(holder, held, doomed, cancellationToken);
                objectLocks = await ObjectLocksHeldAsync(observer, cancellationToken);

                var writing = CreateTaskAsync(spaceB, spaceB.SpaceId, elsewhere, "Written beside a large deletion");

                await LabelWire.WaitUntilBlockedOrAnsweredAsync(observer, 1, writing, cancellationToken);
                Assert.True(
                    writing.IsCompleted,
                    $"Space B's creation waited on Space A's held deletion (AC17). Object-level locks held: [{string.Join(", ", objectLocks)}].");
                created = await writing;
            }
            finally
            {
                await held.RollbackAsync(CancellationToken.None);
            }
        });

        TestContext.Current.TestOutputHelper?.WriteLine($"Object-level locks held by the deletion: [{string.Join(", ", objectLocks)}]");
        Assert.True(created?.StatusCode == HttpStatusCode.Created, $"{created?.StatusCode} {created?.Body}");
    }

    /// <summary>
    /// PG-17 (AC17; <c>deferred-work.md:471</c>, <c>:476-477</c>; Q14 (a)) - the Projects-per-Space count serialises one
    /// Space's creations and no other's: with Space A's bound lock held (the transaction-owned applock Q14 (a) names), A's
    /// own creation queues on it - the anchor that the product takes it - while a creation in Space B answers <c>201</c>.
    /// </summary>
    /// <remarks>
    /// Today A's creation takes no applock and answers at once, so the anchor fails ("answered without waiting"). The
    /// cross-Space wait the ledger records (B queued on <c>LCK_M_RS_U</c> at the end-of-range key) is the plant the dev
    /// phase owes (story Task 9): the old <c>UPDLOCK, HOLDLOCK</c> range count restored in the product for B - B then waits
    /// on A's real creation and the case fails. If Q14 is decided otherwise, rewrite <see cref="ProjectsPerSpaceLock"/>'s
    /// hold to the product's new shape and record it.
    /// </remarks>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC17 - the Projects-per-Space count takes a range lock that reaches other Spaces until story Task 9 / Q14")]
    [Trait("Requirement", "NFR-1")]
    public async Task A_Project_creation_in_another_Space_does_not_wait_on_one_counting_its_Projects()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        WireResponse? inA = null;
        WireResponse? inB = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var spaceA = await RegisterAsync(client, database);
            var spaceB = await RegisterAsync(client, database);

            _ = await CreateProjectAsync(spaceA, "A was here");
            _ = await CreateProjectAsync(spaceB, "B was here");

            await using var holder = await database.OpenForSpaceAsync(spaceA.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);
            Task<WireResponse>? creatingInA = null;

            try
            {
                await using (DbCommand take = holder.CreateCommand())
                {
                    take.Transaction = held;
                    take.CommandText = "EXEC sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction'";
                    take.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@resource", ProjectsPerSpaceLock(spaceA.SpaceId)));
                    await take.ExecuteNonQueryAsync(cancellationToken);
                }

                creatingInA = CreateProjectRequestAsync(spaceA, "Queued in A");

                await WaitUntilBlockedOnALockAsync(observer, 1, creatingInA, cancellationToken);

                var creatingInB = CreateProjectRequestAsync(spaceB, "Not queued in B");

                await LabelWire.WaitUntilBlockedOrAnsweredAsync(observer, 2, creatingInB, cancellationToken);
                Assert.True(creatingInB.IsCompleted, "Space B's Project creation waited while Space A's count was held (AC17).");
                inB = await creatingInB;
            }
            finally
            {
                await held.RollbackAsync(CancellationToken.None);
            }

            inA = await creatingInA;
        });

        Assert.True(inB?.StatusCode == HttpStatusCode.Created, $"{inB?.StatusCode} {inB?.Body}");
        Assert.True(inA?.StatusCode == HttpStatusCode.Created, $"{inA?.StatusCode} {inA?.Body}");
    }

    /// <summary>
    /// PG-18 (AC18; <c>deferred-work.md:557</c>, <c>:562</c>; Q11 (a)) - a move into a gap where no key of at most 128
    /// characters fits is refused <c>409 task-position-full</c>, a type of its own, and nothing moves. The neighbours'
    /// keys are <c>A</c> and <c>A0</c>: nothing sorts strictly between a key and that key followed by the alphabet's first
    /// character. The changed-neighbour refusal keeps <c>task-position-unavailable</c> (2.6's MOV-07, unchanged).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC18 - a full gap is refused as task-position-unavailable until story Task 10")]
    [Trait("Requirement", "AD-15")]
    public async Task A_move_into_a_gap_no_key_fits_is_refused_with_its_own_type_and_moves_nothing()
    {
        var database = Available();
        Actor? owner = null;
        var moverId = Guid.Empty;
        WireResponse? refused = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projectId = await CreateProjectAsync(owner, "No room");
            var statuses = await StatusesAsync(database, owner.SpaceId);
            var lower = await MoveWire.SeedTaskAtAsync(database, owner.SpaceId, projectId, statuses[1].Id, "A", "Lower");

            _ = await MoveWire.SeedTaskAtAsync(database, owner.SpaceId, projectId, statuses[1].Id, "A0", "Upper");
            moverId = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Nowhere to go"));

            refused = await MoveWire.MoveTaskAsync(owner, owner.SpaceId, moverId, statuses[1].Id, lower);
        });

        Assert.True(refused?.StatusCode == HttpStatusCode.Conflict, $"{refused?.StatusCode} {refused?.Body}");
        Assert.Equal(TaskPositionFull, Problem(refused!)?.Type);
        Assert.NotEqual(ProblemTypes.TaskPositionUnavailable, Problem(refused!)?.Type);
        Assert.Equal("V0000", await MoveWire.KeyOfAsync(database, owner!.SpaceId, moverId));
    }

    /// <summary>
    /// The applock Q14 (a) names for one Space's Projects-per-Space count.
    /// </summary>
    private static string ProjectsPerSpaceLock(Guid spaceId) => $"Yello/ProjectsPerSpace/{spaceId:D}";

    private static Task<WireResponse> CreateProjectRequestAsync(Actor actor, string name) =>
        actor.Session.SendWithHeadersAsync(
            HttpMethod.Post,
            ProjectRoutes.ListIn(actor.SpaceId),
            System.Net.Http.Json.JsonContent.Create(new { name }, options: JsonOptions),
            NoHeaders);

    /// <summary>
    /// Every <c>OBJECT</c>-level <c>X</c> lock held on <c>Task</c> or <c>TaskLabel</c> in this database, as
    /// "table mode" - an escalated deletion (Q15).
    /// </summary>
    private static async Task<IReadOnlyList<string>> ObjectLocksHeldAsync(Microsoft.Data.SqlClient.SqlConnection observer, CancellationToken cancellationToken)
    {
        await using var command = observer.CreateCommand();

        command.CommandText =
            """
            SELECT OBJECT_NAME(l.resource_associated_entity_id) + N' ' + l.request_mode
            FROM sys.dm_tran_locks l
            WHERE l.resource_database_id = DB_ID() AND l.resource_type = N'OBJECT' AND l.request_mode = N'X'
              AND OBJECT_NAME(l.resource_associated_entity_id) IN (N'Task', N'TaskLabel')
            """;

        var locks = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            locks.Add(reader.GetString(0));
        }

        return locks;
    }

    private static List<string> Names(JsonElement element) =>
        [.. element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)];

    private RegistrationDatabase Available()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        return fixture.Database!;
    }
}
