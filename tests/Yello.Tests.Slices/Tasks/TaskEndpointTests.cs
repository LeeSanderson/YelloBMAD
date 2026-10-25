using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Story 2.2 over the wire: create a Task in a Project of the active Space, on both surfaces, with the
/// Role, the Tasks-per-Project bound, idempotency and the schema that hold it. AC2, AC3, AC7-AC9, AC11;
/// FR-19, AR-30, AD-2, AD-3, AD-15, AD-18, AD-25. Red-phase scaffolds written by the story 2.2 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Routes and the problem type come from <see cref="TaskRouteScaffold"/> until <c>Yello.Contracts</c>
/// states them</b>; on activation replace each use with <c>TaskRoutes</c> and
/// <c>ProblemTypes.InvalidTaskTitle</c>. Answers are read as raw JSON, so an added property cannot hide
/// behind a DTO.
/// </para>
/// <para>
/// <b>One class, one database</b> on the suite's shared engine (story 1.9 Q1 (a)): no new engine. The cases
/// are split across partial files by subject - this file (create, title, Role, the in-Space not-found),
/// <c>.Bound.cs</c> (AC7, AC11), <c>.Idempotency.cs</c> (AC12's wire half, Task 6) and <c>.Schema.cs</c> (AC2,
/// AC8, AD-15, AD-17). Seed constraints are stated on <see cref="TaskWire"/>.
/// </para>
/// <para>
/// <b>Dependencies on undecided questions, stated so activation corrects the scaffold rather than the
/// product:</b> Q2 (a) - <c>positionKey</c> in the body and the column; Q3 (a) - titles trimmed, 1-200
/// characters; Q5 (a) - the routes. If any is decided otherwise, correct the case and record it.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-19")]
[Trait("Requirement", "AD-2")]
[Trait("Requirement", "AD-3")]
public sealed partial class TaskEndpointTests(MigratedDatabaseFixture fixture)
    : IClassFixture<MigratedDatabaseFixture>
{
    private static readonly string[] TaskProperties = ["createdAt", "id", "positionKey", "statusId", "title"];

    private static readonly string[] CreationOrderTitles = ["Charlie", "Alpha", "Bravo"];

    /// <summary>
    /// AC2 - an Owner creates a Task on either surface: <c>201</c>, the body is exactly
    /// <c>{createdAt, id, positionKey, statusId, title}</c> with the title trimmed, the <c>Location</c> is the
    /// Task's own path under its Space (never its Project, FR-41), and the row belongs to that Space and that
    /// Project and takes the first Status of the effective set - the lowest <c>Position</c> of the Space's
    /// three. A Token creates without anti-forgery (AD-4).
    /// </summary>
    /// <param name="surface">The surface the creation is sent on.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC2 - no Task table, slice or route yet (Tasks 2-4)")]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "AD-4")]
    [Trait("Requirement", "AD-16")]
    public async Task Creating_a_Task_answers_201_and_it_takes_the_first_Status_of_its_Project(IsolationSurface surface)
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse? created = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var token = await IssueTokenAsync(owner, owner.SpaceId);

            projectId = await CreateProjectAsync(owner, "Northwind Redesign");
            created = await CreateTaskOnAsync(surface, owner, token, projectId, "  Draft the brief  ");
        });

        Assert.NotNull(owner);
        Assert.NotNull(created);
        Assert.True(created.StatusCode == HttpStatusCode.Created, $"{created.StatusCode} {created.Body}");

        var statuses = await StatusesAsync(database, owner.SpaceId);

        // Anchor: registration seeded three Statuses (story 1.3), so "the first" picks one of three.
        Assert.Equal(3, statuses.Count);

        using var document = JsonDocument.Parse(created.Body);
        var id = document.RootElement.GetProperty("id").GetGuid();

        Assert.Equal(TaskProperties, document.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Draft the brief", document.RootElement.GetProperty("title").GetString());
        Assert.Equal(statuses[0].Id, document.RootElement.GetProperty("statusId").GetGuid());
        Assert.False(string.IsNullOrEmpty(document.RootElement.GetProperty("positionKey").GetString()));
        Assert.Equal(TaskRouteScaffold.TaskOf(owner.SpaceId, id), created.Header("Location"));
        Assert.DoesNotContain(projectId.ToString("D"), created.Header("Location") ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        var row = Assert.Single(await TaskRowsAsync(database, owner.SpaceId));

        Assert.Equal(id, row.Id);
        Assert.Equal(owner.SpaceId, row.SpaceId);
        Assert.Equal(projectId, row.ProjectId);
        Assert.Equal(statuses[0].Id, row.StatusId);
        Assert.Equal("Draft the brief", row.Title);
        Assert.Equal(TimeSpan.Zero, row.CreatedAt.Offset);
    }

    /// <summary>
    /// AC2, AR-34 - a Task's id is generated application-side by the sequential generator: the column has no
    /// database default, and ids taken in creation order sort in creation order under SQL Server's own
    /// <c>uniqueidentifier</c> ordering.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2 - no Task table or create route yet (Tasks 2-4)")]
    [Trait("Requirement", "AR-34")]
    [Trait("Priority", "P1")]
    public async Task Task_ids_are_generated_by_the_application_in_sequential_order()
    {
        var database = Available();
        Actor? owner = null;
        var created = new List<Guid>();

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Ordering");

            foreach (var title in CreationOrderTitles)
            {
                created.Add(IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, title)));
            }
        });

        Assert.NotNull(owner);
        Assert.Equal(3, created.Count);

        Assert.Empty(await CatalogueAsync(
            database,
            """
            SELECT dc.name FROM sys.default_constraints dc
            JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'dbo.[Task]') AND c.name = N'Id'
            """));

        // SQL Server's uniqueidentifier order, not .NET's: that is the order the sequential generator serves.
        var bySqlOrder = await TaskIdsInSqlOrderAsync(database, owner.SpaceId);

        Assert.Equal(created, bySqlOrder);
    }

    /// <summary>
    /// AC2, Q3 (a) - a title that is blank after trimming, or longer than 200 characters, is refused
    /// <c>400 invalid-task-title</c> and nothing is written. Anchor: exactly 200 characters is created, so the
    /// refusal is the rule's and not the route's.
    /// </summary>
    /// <param name="length">The title's length, in characters; 0 and a run of spaces are blank.</param>
    /// <param name="blank">Whether the title is spaces only.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC2 - no title rule or create route yet (Tasks 2-4; Q3)")]
    [InlineData(0, false)]
    [InlineData(3, true)]
    [InlineData(201, false)]
    [Trait("Priority", "P1")]
    public async Task An_invalid_title_is_refused_400_and_nothing_is_written(int length, bool blank)
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? refused = null;
        WireResponse? anchor = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Titles");

            refused = await CreateTaskAsync(owner, owner.SpaceId, projectId, new string(blank ? ' ' : 't', length));
            anchor = await CreateTaskAsync(owner, owner.SpaceId, projectId, new string('t', TaskRouteScaffold.TitleMaximumLength));
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.BadRequest, refused?.StatusCode);
        Assert.Equal(TaskRouteScaffold.InvalidTaskTitle, Problem(refused!)?.Type);
        Assert.Equal(HttpStatusCode.Created, anchor?.StatusCode);
        Assert.Equal([new string('t', TaskRouteScaffold.TitleMaximumLength)], (await TaskRowsAsync(database, owner.SpaceId)).Select(row => row.Title));
    }

    /// <summary>
    /// AC3, AC9, V-1, NFR-7 - a Viewer added to an Owner's Space (not 2.1's demoted-own-Space variance) is
    /// refused <c>403 insufficient-role</c> naming <c>manage-tasks</c> on its Session and on its Token, each
    /// recorded once as <c>InsufficientRole</c> and never as <c>CrossSpace</c>, and nothing is written.
    /// Anchor: a Member of the same Space creates one (AC2: Owner, Admin or Member).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 - no Task create route declaring manage-tasks yet (Task 4)")]
    [Trait("Requirement", "AR-11")]
    [Trait("Requirement", "AD-20")]
    [Trait("Requirement", "NFR-7")]
    public async Task A_Viewer_is_refused_403_on_create_on_both_surfaces_and_nothing_is_written()
    {
        var database = Available();
        Actor? owner = null;
        Actor? viewer = null;
        WireResponse? bySession = null;
        WireResponse? byToken = null;
        WireResponse? anchor = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            viewer = await RegisterAsync(client, database);
            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, viewer.AccountId, "Viewer");
            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            var projectId = await CreateProjectAsync(owner, "Shared work");
            var token = await IssueTokenAsync(viewer, owner.SpaceId);

            bySession = await CreateTaskAsync(viewer, owner.SpaceId, projectId, "Viewer's attempt");
            byToken = await CreateTaskWithTokenAsync(viewer, token, owner.SpaceId, projectId, "Viewer's script");
            anchor = await CreateTaskAsync(member, owner.SpaceId, projectId, "Member's Task");
        });

        Assert.NotNull(owner);
        Assert.NotNull(viewer);

        foreach (var refused in new[] { bySession!, byToken! })
        {
            Assert.True(refused.StatusCode == HttpStatusCode.Forbidden, $"{refused.StatusCode} {refused.Body}");
            Assert.Equal(ProblemTypes.InsufficientRole, Problem(refused)?.Type);
            Assert.Equal(TaskRouteScaffold.ManageTasks, Problem(refused)?.Capability);
        }

        Assert.Equal([InsufficientRoleKind, InsufficientRoleKind], await RefusalKindsAsync(database, viewer.AccountId, owner.SpaceId));
        Assert.Equal(HttpStatusCode.Created, anchor?.StatusCode);
        Assert.Equal(["Member's Task"], (await TaskRowsAsync(database, owner.SpaceId)).Select(row => row.Title));
    }

    /// <summary>
    /// AC9, AD-3, AD-20, Task 4 - creating a Task in a Project id that matches nothing in the active Space
    /// answers the boundary's <c>404 not-available</c>, byte-identical to a Space that does not exist, records
    /// nothing, and never surfaces the foreign key as a <c>500</c>. Anchor: the same Owner's real Project
    /// takes a Task.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - no Task create route yet (Task 4)")]
    [Trait("Requirement", "AD-20")]
    public async Task Creating_in_a_Project_that_matches_nothing_answers_the_boundary_404_and_records_nothing()
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

            inSpace = await CreateTaskAsync(owner, owner.SpaceId, Guid.NewGuid(), "Nowhere");
            refusalsAfterInSpace = await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId);
            absentSpace = await CreateTaskAsync(owner, Guid.NewGuid(), Guid.NewGuid(), "Nowhere");
            anchor = await CreateTaskAsync(owner, owner.SpaceId, projectId, "Somewhere");
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
        Assert.Equal(HttpStatusCode.Created, anchor?.StatusCode);
        Assert.Equal(["Somewhere"], (await TaskRowsAsync(database, owner!.SpaceId)).Select(row => row.Title));
    }

    private RegistrationDatabase Available()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        return fixture.Database!;
    }
}
