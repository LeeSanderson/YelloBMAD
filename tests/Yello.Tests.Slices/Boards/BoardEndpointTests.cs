using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Boards;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using Yello.Tests.Slices.Tasks;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Boards;

/// <summary>
/// Story 2.2's Board read over the wire: <c>GET …/projects/{projectId}/board</c> answers the Project and one
/// column per Status of its effective set, in effective order, each with the true total for that Status
/// counted separately from the rows returned, and every Task of the Project in exactly one column. AC1,
/// AC3-AC5, AC9; FR-28, AD-3, AD-16, AD-29. Written red by the story 2.2 ATDD run; activated by its dev phase.
/// </summary>
/// <remarks>
/// <para>
/// <b>One class, one database</b> on the Slices engine, beside <see cref="TaskEndpointTests"/>'s. Tasks in
/// several Statuses are seeded by SQL (<see cref="TaskWire.SeedTasksAsync"/>), because a creation always
/// takes the first Status (AC2) and no route moves a Task until 2.6. Seed constraints are stated on
/// <see cref="TaskWire"/>.
/// </para>
/// <para>
/// <b>The shape is the story's Task 3:</b> <c>{project: {id, name}, columns: [{statusId, name, total,
/// tasks}]}</c>, each Task <c>{createdAt, id, positionKey, statusId, title}</c>, and nothing carries a
/// <c>spaceId</c>. Q6 (a): every Task is returned, so here <c>total</c> equals the rows - the case that tells
/// them apart is the handler-level one Task 13's plant needs (see the ATDD checklist).
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-28")]
[Trait("Requirement", "AD-29")]
public sealed class BoardEndpointTests(MigratedDatabaseFixture fixture)
    : IClassFixture<MigratedDatabaseFixture>
{
    private static readonly string[] BoardProperties = ["columns", "project"];
    private static readonly string[] ProjectProperties = ["id", "name"];
    private static readonly string[] ColumnProperties = ["name", "statusId", "tasks", "total"];
    // Story 2.3 appended dueDate (AD-19: adding a field is legal in v1); the set is still pinned exactly.
    private static readonly string[] TaskProperties = ["createdAt", "dueDate", "id", "positionKey", "statusId", "title"];

    /// <summary>
    /// AC1, AD-16 - the Board of a Project holding no Tasks answers the Project and one column per Status of
    /// the Space's default set, in <c>Position</c> order, each with <c>total: 0</c> and no Tasks.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-16")]
    public async Task An_empty_Projects_Board_answers_its_columns_in_effective_order_each_with_total_0()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse? board = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = await CreateProjectAsync(owner, "Empty");
            board = await BoardAsync(owner, owner.SpaceId, projectId);
        });

        Assert.NotNull(owner);
        Assert.True(board?.StatusCode == HttpStatusCode.OK, $"{board?.StatusCode} {board?.Body}");

        var statuses = await StatusesAsync(database, owner.SpaceId);

        using var document = JsonDocument.Parse(board!.Body);
        var columns = document.RootElement.GetProperty("columns").EnumerateArray().ToList();

        Assert.Equal(projectId, document.RootElement.GetProperty("project").GetProperty("id").GetGuid());
        Assert.Equal("Empty", document.RootElement.GetProperty("project").GetProperty("name").GetString());
        Assert.Equal(statuses.Select(status => status.Id), columns.Select(column => column.GetProperty("statusId").GetGuid()));
        Assert.Equal(statuses.Select(status => status.Name), columns.Select(column => column.GetProperty("name").GetString()));
        Assert.All(columns, column =>
        {
            Assert.Equal(0, column.GetProperty("total").GetInt32());
            Assert.Empty(column.GetProperty("tasks").EnumerateArray());
        });
    }

    /// <summary>
    /// Task 3 - the Board answer is exactly <c>{columns, project}</c>, a column exactly
    /// <c>{name, statusId, tasks, total}</c>, a Task exactly <c>{createdAt, id, positionKey, statusId, title}</c>,
    /// and the Space's id appears nowhere in it (a Space-scoped answer carries no <c>spaceId</c>).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Priority", "P1")]
    public async Task The_Board_answer_has_exactly_the_contracts_properties_and_no_SpaceId()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? board = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Shaped");

            _ = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Has a shape"));
            board = await BoardAsync(owner, owner.SpaceId, projectId);
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.OK, board?.StatusCode);

        using var document = JsonDocument.Parse(board!.Body);
        var root = document.RootElement;
        var columns = root.GetProperty("columns").EnumerateArray().ToList();
        var tasks = columns.SelectMany(column => column.GetProperty("tasks").EnumerateArray()).ToList();

        Assert.Equal(BoardProperties, Names(root));
        Assert.Equal(ProjectProperties, Names(root.GetProperty("project")));
        Assert.All(columns, column => Assert.Equal(ColumnProperties, Names(column)));
        Assert.Equal(TaskProperties, Names(Assert.Single(tasks)));
        Assert.DoesNotContain(owner.SpaceId.ToString("D"), board.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC5, AD-29 - each column's <c>total</c> is the true count for its Status, equal to a
    /// <c>SELECT COUNT(*)</c> over that Status in the Project, with the three Statuses holding different
    /// numbers so a total taken from the wrong column, or from the whole Project, cannot pass.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-29")]
    public async Task Each_columns_total_is_the_true_count_for_its_Status()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse? board = null;
        int[] seeded = [3, 1, 2];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = await CreateProjectAsync(owner, "Counted");

            var statuses = await StatusesAsync(database, owner.SpaceId);

            for (var index = 0; index < statuses.Count; index++)
            {
                await SeedTasksAsync(database, owner.SpaceId, projectId, statuses[index].Id, seeded[index]);
            }

            board = await BoardAsync(owner, owner.SpaceId, projectId);
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.OK, board?.StatusCode);

        var rows = await TaskRowsAsync(database, owner.SpaceId);

        using var document = JsonDocument.Parse(board!.Body);

        foreach (var column in document.RootElement.GetProperty("columns").EnumerateArray())
        {
            var statusId = column.GetProperty("statusId").GetGuid();

            Assert.Equal(rows.Count(row => row.ProjectId == projectId && row.StatusId == statusId), column.GetProperty("total").GetInt32());
        }

        Assert.Equal(seeded, document.RootElement.GetProperty("columns").EnumerateArray().Select(column => column.GetProperty("total").GetInt32()));
    }

    /// <summary>
    /// AC4 - every Task of the Project appears in exactly one column, the column of its own Status, ordered by
    /// position key then id; no Task appears twice or in none; and a Task of another Project in the same Space
    /// appears in no column.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Every_Task_of_the_Project_appears_in_exactly_one_column_and_no_other_Projects_does()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse? board = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = await CreateProjectAsync(owner, "This one");
            var otherProject = await CreateProjectAsync(owner, "Another one");

            foreach (var status in await StatusesAsync(database, owner.SpaceId))
            {
                await SeedTasksAsync(database, owner.SpaceId, projectId, status.Id, 2);
                await SeedTasksAsync(database, owner.SpaceId, otherProject, status.Id, 1);
            }

            board = await BoardAsync(owner, owner.SpaceId, projectId);
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.OK, board?.StatusCode);

        var own = (await TaskRowsAsync(database, owner.SpaceId)).Where(row => row.ProjectId == projectId).ToList();

        using var document = JsonDocument.Parse(board!.Body);
        var placed = new List<Guid>();

        foreach (var column in document.RootElement.GetProperty("columns").EnumerateArray())
        {
            var statusId = column.GetProperty("statusId").GetGuid();
            var tasks = column.GetProperty("tasks").EnumerateArray().ToList();

            Assert.All(tasks, task => Assert.Equal(statusId, task.GetProperty("statusId").GetGuid()));
            Assert.Equal(
                own.Where(row => row.StatusId == statusId).OrderBy(row => row.PositionKey, StringComparer.Ordinal).Select(row => row.Id),
                tasks.Select(task => task.GetProperty("id").GetGuid()));

            placed.AddRange(tasks.Select(task => task.GetProperty("id").GetGuid()));
        }

        // Anchored: the Project holds Tasks in every Status, so "each exactly once" is about something.
        Assert.Equal(6, own.Count);
        Assert.Equal(placed.Count, placed.Distinct().Count());
        Assert.Equal(own.Select(row => row.Id).Order(), placed.Order());
    }

    /// <summary>
    /// AC3 (server half), FR-28 - a Viewer is served exactly the Board a Member is, on its Session and on its
    /// Token: Role changes what can be done to a Board, never what it shows.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-4")]
    public async Task A_Viewer_is_served_the_same_Board_as_a_Member_on_both_surfaces()
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

            var projectId = await CreateProjectAsync(owner, "Seen alike");
            _ = IdOf(await CreateTaskAsync(memberActor, owner.SpaceId, projectId, "Visible to all"));

            var token = await IssueTokenAsync(viewer, owner.SpaceId);

            member = await BoardAsync(memberActor, owner.SpaceId, projectId);
            viewerSession = await BoardAsync(viewer, owner.SpaceId, projectId);
            viewerToken = await viewer.Session.GetWithTokenAsync(BoardRoutes.BoardOf(owner.SpaceId, projectId), token);
        });

        Assert.Equal(HttpStatusCode.OK, member?.StatusCode);
        Assert.Contains("Visible to all", member!.Body, StringComparison.Ordinal);
        Assert.Equal(member.Body, viewerSession?.Body);
        Assert.Equal(member.Body, viewerToken?.Body);
    }

    /// <summary>
    /// AC9, AD-3, AD-20 - the Board of a Project id that matches nothing in the active Space answers the
    /// boundary's <c>404 not-available</c>, byte-identical to a Space that does not exist, and records
    /// nothing. Anchor: the same Owner's real Project's Board is served.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-20")]
    public async Task The_Board_of_a_Project_that_matches_nothing_answers_the_boundary_404_and_records_nothing()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? inSpace = null;
        WireResponse? absentSpace = null;
        WireResponse? anchor = null;
        IReadOnlyList<string> refusalsAfterInSpace = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Present");

            inSpace = await BoardAsync(owner, owner.SpaceId, Guid.NewGuid());
            refusalsAfterInSpace = await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId);
            absentSpace = await BoardAsync(owner, Guid.NewGuid(), Guid.NewGuid());
            anchor = await BoardAsync(owner, owner.SpaceId, projectId);
        });

        Assert.NotNull(inSpace);
        Assert.NotNull(absentSpace);
        Assert.Equal(HttpStatusCode.NotFound, inSpace.StatusCode);
        Assert.Equal(absentSpace.StatusCode, inSpace.StatusCode);
        Assert.Equal(absentSpace.ContentType, inSpace.ContentType);
        Assert.Equal(absentSpace.Body, inSpace.Body);
        Assert.Equal(absentSpace.HeaderNames, inSpace.HeaderNames);
        Assert.Equal(ProblemTypes.NotAvailable, Problem(inSpace)?.Type);
        Assert.Empty(refusalsAfterInSpace);
        Assert.Equal(HttpStatusCode.OK, anchor?.StatusCode);
    }

    private static List<string> Names(JsonElement element) =>
        [.. element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)];

    private RegistrationDatabase Available()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        return fixture.Database!;
    }
}
