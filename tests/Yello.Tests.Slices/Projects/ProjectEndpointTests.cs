using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Accounts;
using Yello.Contracts.ApiTokens;
using Yello.Contracts.Spaces;
using Yello.Domain.Accounts;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// Story 2.1 over the wire: create, rename and list Projects in the active Space, on both surfaces, with
/// the Role, the bound, idempotency and the schema that hold them. AC2-AC7, AC9-AC11; FR-17, FR-18,
/// AR-25, AR-30, AD-2, AD-3, AD-18, AD-25.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 2.1 ATDD run on 2026-10-25.</b> Every case is skipped
/// with <c>red: AC&lt;n&gt;</c> until dev-story activates it. Routes and problem types come from
/// <see cref="ProjectRouteScaffold"/> until <c>Yello.Contracts.Projects</c> exists; bodies are anonymous
/// objects and answers are read as raw JSON, so an added property cannot hide behind a DTO. On
/// activation, replace the scaffold constants with the contract ones and keep reading raw JSON.
/// </para>
/// <para>
/// <b>One class, one database</b> on the suite's shared engine (story 1.9 Q1 (a)): no new engine. The
/// cases are split across partial files by subject - this file (create, rename, list, Role, method
/// table, the Token's first write), <c>.Bound.cs</c> (AC5, AC10), <c>.Idempotency.cs</c> (AC9) and
/// <c>.Schema.cs</c> (AC6, AC7).
/// </para>
/// <para>
/// <b>Seed constraints checked</b> against <c>20260828164716_InitialSchema.cs</c>,
/// <c>20261024121107_SpaceBoundaryRefusals.cs</c>, <c>20261024143930_AccountScopedMembership.cs</c> and
/// <c>20261024164849_ApiTokens.cs</c>, and against story 2.1 Task 3 for the table that does not exist
/// yet. Accounts, Personal Spaces, Owner Memberships and Tokens are written by the product
/// (<c>POST /accounts</c>, <c>POST /spaces/{id}/api-tokens</c>), so <c>UX_Account_NormalizedEmailAddress</c>
/// (every address random), <c>FK_Membership_Account_AccountId</c>, <c>FK_Membership_Space_SpaceId</c>,
/// <c>UX_Membership_SpaceId_Owner</c> and <c>FK_ApiToken_Membership_BoundSpaceId_AccountId</c> hold by
/// construction. A second member is added by <see cref="AddMemberAsync"/> under the Space's own key
/// (RLS <c>BLOCK AFTER INSERT</c> on <c>Membership</c>), for an Account the product registered, as a
/// non-Owner, once per Space (<c>AK_Membership_SpaceId_AccountId</c>). Projects seeded by SQL
/// (<see cref="SeedProjectsAsync"/>) are written under the Space's key into an existing Space, with the
/// four columns Task 3 names (<c>Id</c>, <c>SpaceId</c>, <c>Name</c>, <c>CreatedAt</c>) and an id
/// supplied by the test, because Task 3 maps <c>Id</c> with <c>ValueGeneratedNever</c>; if the migration
/// adds a further non-nullable column without a default, correct that one statement.
/// </para>
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-17")]
[Trait("Requirement", "FR-18")]
[Trait("Requirement", "AD-2")]
[Trait("Requirement", "AD-3")]
public sealed partial class ProjectEndpointTests(MigratedDatabaseFixture fixture)
    : IClassFixture<MigratedDatabaseFixture>
{
    private const string Password = "a-password-nobody-else-uses-1!";
    private const string ClientOrigin = "https://client.yello.test";
    private const string CapabilityManageProjects = "manage-projects";
    private const string InsufficientRoleKind = "InsufficientRole";
    private const string CrossSpaceKind = "CrossSpace";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, string> Configured = new(StringComparer.Ordinal)
    {
        ["ClientOrigin"] = ClientOrigin,
    };

    private static readonly Dictionary<string, string> NoHeaders = new(StringComparer.Ordinal);

    private static readonly string[] ProjectProperties = ["createdAt", "id", "name"];

    private static readonly string[] CreationOrderNames = ["Charlie", "Alpha", "Bravo"];

    private static readonly string[] ListingNames = ["Delta", "Bravo", "Delta", "Alpha"];

    /// <summary>
    /// AC2 - a Member (here the Owner of a Personal Space) creates a Project: <c>201</c>, the body is
    /// exactly <c>{createdAt, id, name}</c> with the name trimmed, the <c>Location</c> is the Project's
    /// own path, and the row belongs to exactly that Space.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2 - the create route does not exist until story 2.1 Task 5")]
    public async Task Creating_a_Project_answers_201_with_its_Location_and_fixes_it_to_the_active_Space()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? created = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            created = await CreateAsync(owner, "  Northwind Redesign  ");
        });

        Assert.NotNull(owner);
        Assert.NotNull(created);
        Assert.True(created.StatusCode == HttpStatusCode.Created, $"{created.StatusCode} {created.Body}");

        using var document = JsonDocument.Parse(created.Body);
        var id = document.RootElement.GetProperty("id").GetGuid();

        Assert.Equal(ProjectProperties, document.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Northwind Redesign", document.RootElement.GetProperty("name").GetString());
        Assert.Equal(ProjectRouteScaffold.ProjectOf(owner.SpaceId, id), created.Header("Location"));

        var row = Assert.Single(await ProjectRowsAsync(database, owner.SpaceId));

        Assert.Equal(id, row.Id);
        Assert.Equal(owner.SpaceId, row.SpaceId);
        Assert.Equal("Northwind Redesign", row.Name);
        Assert.Equal(TimeSpan.Zero, row.CreatedAt.Offset);
    }

    /// <summary>
    /// AC2, Q2 (a), AD-16 - creating a Project leaves the Space's default Status set exactly as it was,
    /// and nothing stores a Project's effective set: no Status column on <c>Project</c>, no delta table.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2 - the create route and the Project table do not exist until story 2.1 Tasks 3 and 5")]
    [Trait("Requirement", "AD-16")]
    public async Task Creating_a_Project_leaves_the_Space_default_Statuses_unchanged_and_stores_no_effective_set()
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<Guid> before = [];
        WireResponse? created = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            before = await StatusIdsAsync(database, owner.SpaceId);
            created = await CreateAsync(owner, "Harbour Ops");
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.Created, created?.StatusCode);

        // Anchor: registration seeded the default set (story 1.3), so "unchanged" is about three rows.
        Assert.Equal(3, before.Count);
        Assert.Equal(before.Order(), (await StatusIdsAsync(database, owner.SpaceId)).Order());

        var columns = await CatalogueAsync(
            database,
            "SELECT c.name FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id WHERE t.name = N'Project'");

        Assert.Contains("SpaceId", columns);
        Assert.DoesNotContain(columns, column => column.Contains("Status", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(await CatalogueAsync(
            database,
            "SELECT t.name FROM sys.tables t WHERE t.name LIKE N'%Delta%' OR t.name LIKE N'%EffectiveStatus%'"));
    }

    /// <summary>
    /// AC6 - a Project's id is generated application-side by the sequential generator: the column has no
    /// database default, and ids taken in creation order sort in creation order under SQL Server's own
    /// <c>uniqueidentifier</c> ordering, which is what the sequential generator exists for.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC6 - the Project table and the create route do not exist until story 2.1 Tasks 3 and 5")]
    [Trait("Requirement", "AR-34")]
    public async Task Project_ids_are_generated_by_the_application_in_sequential_order()
    {
        var database = Available();
        Actor? owner = null;
        var created = new List<Guid>();

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            foreach (var name in CreationOrderNames)
            {
                created.Add(IdOf(await CreateAsync(owner, name)));
            }
        });

        Assert.NotNull(owner);
        Assert.Equal(3, created.Count);

        Assert.Empty(await CatalogueAsync(
            database,
            """
            SELECT dc.name FROM sys.default_constraints dc
            JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Project') AND c.name = N'Id'
            """));

        var bySqlOrder = (await ProjectRowsAsync(database, owner.SpaceId, orderById: true)).Select(row => row.Id).ToList();

        Assert.Equal(created, bySqlOrder);
    }

    /// <summary>
    /// AC2, Q3 (a) - a name that is blank after trimming, or longer than 100 characters, is refused
    /// <c>400 invalid-project-name</c> and nothing is written.
    /// </summary>
    /// <param name="length">The name's length, in characters; 0 and a run of spaces are blank.</param>
    /// <param name="blank">Whether the name is spaces only.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC2 - the create route does not exist until story 2.1 Task 5; ProjectName lands in Task 3")]
    [InlineData(0, false)]
    [InlineData(3, true)]
    [InlineData(101, false)]
    [Trait("Priority", "P1")]
    public async Task An_invalid_name_is_refused_400_and_nothing_is_written(int length, bool blank)
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? refused = null;
        WireResponse? anchor = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            refused = await CreateAsync(owner, new string(blank ? ' ' : 'n', length));

            // Anchor: the limit itself is accepted, so the refusal is the rule's and not the route's.
            anchor = await CreateAsync(owner, new string('n', 100));
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.BadRequest, refused?.StatusCode);
        Assert.Equal(ProjectRouteScaffold.InvalidProjectName, Problem(refused!)?.Type);
        Assert.Equal(HttpStatusCode.Created, anchor?.StatusCode);
        Assert.Equal([new string('n', 100)], (await ProjectRowsAsync(database, owner.SpaceId)).Select(row => row.Name));
    }

    /// <summary>
    /// AC2, AC8, V-1, NFR-7 - a Viewer's Session and a Viewer's Token creating a Project are each refused
    /// <c>403 insufficient-role</c> naming <c>manage-projects</c>, each recorded once as
    /// <c>InsufficientRole</c> and never as <c>CrossSpace</c>, and nothing is written. Anchor: the Owner
    /// of the same Space then creates one.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2 - the create route and its ManageProjects declaration do not exist until story 2.1 Task 5")]
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
            await AddMemberAsync(database, owner.SpaceId, viewer.AccountId, "Viewer");

            var token = await IssueTokenAsync(viewer, owner.SpaceId);

            bySession = await CreateAsync(viewer, "Viewer's attempt", owner.SpaceId);
            byToken = await CreateWithTokenAsync(viewer, token, owner.SpaceId, "Viewer's script");
            anchor = await CreateAsync(owner, "Owner's Project");
        });

        Assert.NotNull(owner);
        Assert.NotNull(viewer);

        foreach (var refused in new[] { bySession!, byToken! })
        {
            Assert.True(refused.StatusCode == HttpStatusCode.Forbidden, $"{refused.StatusCode} {refused.Body}");
            Assert.Equal(ProblemTypes.InsufficientRole, Problem(refused)?.Type);
            Assert.Equal(CapabilityManageProjects, Problem(refused)?.Capability);
        }

        Assert.Equal([InsufficientRoleKind, InsufficientRoleKind], await RefusalKindsAsync(database, viewer.AccountId, owner.SpaceId));
        Assert.Equal(HttpStatusCode.Created, anchor?.StatusCode);
        Assert.Equal(["Owner's Project"], (await ProjectRowsAsync(database, owner.SpaceId)).Select(row => row.Name));
    }

    /// <summary>
    /// AC3 - renaming changes the name and nothing else: the id, the Space and the creation instant are
    /// the ones the Project was created with, and the answer is the renamed Project.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 - the rename route does not exist until story 2.1 Task 5")]
    public async Task Renaming_a_Project_changes_its_name_and_nothing_else()
    {
        var database = Available();
        Actor? owner = null;
        ProjectRow? before = null;
        WireResponse? renamed = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var id = IdOf(await CreateAsync(owner, "Working title"));
            before = Assert.Single(await ProjectRowsAsync(database, owner.SpaceId));
            renamed = await RenameAsync(owner, owner.SpaceId, id, "  Final title  ");
        });

        Assert.NotNull(owner);
        Assert.NotNull(before);
        Assert.True(renamed?.StatusCode == HttpStatusCode.OK, $"{renamed?.StatusCode} {renamed?.Body}");

        using var document = JsonDocument.Parse(renamed!.Body);

        Assert.Equal(before.Id, document.RootElement.GetProperty("id").GetGuid());
        Assert.Equal("Final title", document.RootElement.GetProperty("name").GetString());
        Assert.Equal(before with { Name = "Final title" }, Assert.Single(await ProjectRowsAsync(database, owner.SpaceId)));
    }

    /// <summary>
    /// AC3, AC8 - a Viewer's rename is refused <c>403 insufficient-role</c> on both surfaces and the name
    /// is unchanged.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 - the rename route does not exist until story 2.1 Task 5")]
    [Trait("Requirement", "AD-20")]
    public async Task A_Viewer_is_refused_403_on_rename_on_both_surfaces_and_the_name_is_unchanged()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? bySession = null;
        WireResponse? byToken = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var viewer = await RegisterAsync(client, database);
            await AddMemberAsync(database, owner.SpaceId, viewer.AccountId, "Viewer");

            var id = IdOf(await CreateAsync(owner, "Kept"));
            var token = await IssueTokenAsync(viewer, owner.SpaceId);

            bySession = await RenameAsync(viewer, owner.SpaceId, id, "Taken");
            byToken = await RenameWithTokenAsync(viewer, token, owner.SpaceId, id, "Taken by script");
        });

        Assert.NotNull(owner);

        foreach (var refused in new[] { bySession!, byToken! })
        {
            Assert.True(refused.StatusCode == HttpStatusCode.Forbidden, $"{refused.StatusCode} {refused.Body}");
            Assert.Equal(CapabilityManageProjects, Problem(refused)?.Capability);
        }

        Assert.Equal(["Kept"], (await ProjectRowsAsync(database, owner.SpaceId)).Select(row => row.Name));
    }

    /// <summary>
    /// AC8, AD-3, Task 5 - renaming a Project id that matches nothing in the active Space answers the
    /// boundary's <c>404 not-available</c>, byte-identical to a Space that does not exist, and - unlike the
    /// boundary refusal - records nothing (AD-20 records boundary <c>404</c>s and <c>403</c>s only).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC8 - the rename route does not exist until story 2.1 Task 5")]
    [Trait("Requirement", "AD-20")]
    public async Task Renaming_an_id_that_matches_nothing_in_the_Space_answers_the_boundary_404_and_records_nothing()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? inSpace = null;
        WireResponse? absentSpace = null;
        IReadOnlyList<string> refusalsAfterInSpace = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            _ = IdOf(await CreateAsync(owner, "Present"));

            inSpace = await RenameAsync(owner, owner.SpaceId, Guid.NewGuid(), "Anything");
            refusalsAfterInSpace = await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId);
            absentSpace = await RenameAsync(owner, Guid.NewGuid(), Guid.NewGuid(), "Anything");
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
        Assert.Equal(["Present"], (await ProjectRowsAsync(database, owner!.SpaceId)).Select(row => row.Name));
    }

    /// <summary>
    /// AC4, Q8 (a) - the listing holds every Project in the Space, ordered by name and then by id, with
    /// duplicate names permitted (Q3) and ordered by id as the database orders them.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC4 - the list route does not exist until story 2.1 Task 5")]
    public async Task The_listing_holds_every_Project_in_the_Space_ordered_by_name_then_id()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? listed = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            foreach (var name in ListingNames)
            {
                Assert.Equal(HttpStatusCode.Created, (await CreateAsync(owner, name)).StatusCode);
            }

            listed = await owner.Session.GetAsync(ProjectRouteScaffold.ListIn(owner.SpaceId));
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.OK, listed?.StatusCode);

        var deltas = (await ProjectRowsAsync(database, owner.SpaceId, orderById: true))
            .Where(row => row.Name == "Delta")
            .Select(row => row.Id)
            .ToList();
        var rows = Rows(listed!);

        Assert.Equal(["Alpha", "Bravo", "Delta", "Delta"], rows.Select(row => row.Name));
        Assert.Equal(deltas, rows.Skip(2).Select(row => row.Id));
    }

    /// <summary>
    /// AC4 - a Viewer is served exactly the listing a Member is, on both surfaces: Role affects what can be
    /// done to a Project, not whether it is visible.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC4 - the list route does not exist until story 2.1 Task 5")]
    public async Task A_Viewer_is_served_the_same_listing_as_a_Member_on_both_surfaces()
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

            Assert.Equal(HttpStatusCode.Created, (await CreateAsync(owner, "One")).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await CreateAsync(memberActor, "Two", owner.SpaceId)).StatusCode);

            var token = await IssueTokenAsync(viewer, owner.SpaceId);

            member = await memberActor.Session.GetAsync(ProjectRouteScaffold.ListIn(owner.SpaceId));
            viewerSession = await viewer.Session.GetAsync(ProjectRouteScaffold.ListIn(owner.SpaceId));
            viewerToken = await viewer.Session.GetWithTokenAsync(ProjectRouteScaffold.ListIn(owner.SpaceId), token);
        });

        Assert.Equal(HttpStatusCode.OK, member?.StatusCode);
        Assert.Equal(2, Rows(member!).Count);
        Assert.Equal(member!.Body, viewerSession?.Body);
        Assert.Equal(member.Body, viewerToken?.Body);
    }

    /// <summary>
    /// AC11, <c>deferred-work.md:388</c> - a Token creates a Project with no cookie and no anti-forgery
    /// header (<c>201</c>), while the same request carrying the Session cookie and no anti-forgery header is
    /// still refused <c>400 antiforgery-token-missing</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC11 - the create route does not exist until story 2.1 Task 5")]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "AR-14")]
    public async Task A_Token_creates_a_Project_without_antiforgery_and_a_Session_without_the_header_is_refused()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? byToken = null;
        WireResponse? bySessionWithoutHeader = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var token = await IssueTokenAsync(owner, owner.SpaceId);

            byToken = await CreateWithTokenAsync(owner, token, owner.SpaceId, "By script");
            bySessionWithoutHeader = await owner.Session.SendWithHeadersAsync(
                HttpMethod.Post,
                ProjectRouteScaffold.ListIn(owner.SpaceId),
                NameBody("By browser, unguarded"),
                NoHeaders,
                withAntiforgeryToken: false);
        });

        Assert.NotNull(owner);
        Assert.True(byToken?.StatusCode == HttpStatusCode.Created, $"{byToken?.StatusCode} {byToken?.Body}");
        Assert.Equal(HttpStatusCode.BadRequest, bySessionWithoutHeader?.StatusCode);
        Assert.Equal(ProblemTypes.AntiforgeryTokenMissing, Problem(bySessionWithoutHeader!)?.Type);
        Assert.Equal(["By script"], (await ProjectRowsAsync(database, owner.SpaceId)).Select(row => row.Name));
    }

    /// <summary>
    /// AC11, Q9 (a), <c>deferred-work.md:300</c> - a method a Project route does not map answers the
    /// unmatched fallback's <c>404 not-available</c> with no <c>Allow</c> header, so the route's method set
    /// is not disclosed. Anchored on the route answering its mapped method.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC11 - the Project routes do not exist until story 2.1 Task 5; Q9 decides the expectation")]
    [Trait("Priority", "P1")]
    public async Task An_unmapped_method_on_a_Project_route_answers_404_with_no_Allow_header()
    {
        var database = Available();
        WireResponse? mapped = null;
        var unmapped = new List<WireResponse>();

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var id = IdOf(await CreateAsync(owner, "Methods"));

            mapped = await owner.Session.GetAsync(ProjectRouteScaffold.ListIn(owner.SpaceId));

            unmapped.Add(await owner.Session.SendWithHeadersAsync(HttpMethod.Put, ProjectRouteScaffold.ListIn(owner.SpaceId), NameBody("x"), NoHeaders));
            unmapped.Add(await owner.Session.SendWithHeadersAsync(HttpMethod.Put, ProjectRouteScaffold.ProjectOf(owner.SpaceId, id), NameBody("x"), NoHeaders));
            unmapped.Add(await owner.Session.SendWithHeadersAsync(HttpMethod.Delete, ProjectRouteScaffold.ProjectOf(owner.SpaceId, id), content: null, NoHeaders));
        });

        Assert.Equal(HttpStatusCode.OK, mapped?.StatusCode);
        Assert.Equal(3, unmapped.Count);
        Assert.All(unmapped, answer =>
        {
            Assert.Equal(HttpStatusCode.NotFound, answer.StatusCode);
            Assert.Equal(ProblemTypes.NotAvailable, Problem(answer)?.Type);
            Assert.Null(answer.Header("Allow"));
        });
    }

    private static Guid IdOf(WireResponse created)
    {
        Assert.True(created.StatusCode == HttpStatusCode.Created, $"{created.StatusCode} {created.Body}");

        using var document = JsonDocument.Parse(created.Body);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static List<(Guid Id, string Name)> Rows(WireResponse listed)
    {
        using var document = JsonDocument.Parse(listed.Body);

        return [.. document.RootElement.EnumerateArray().Select(row => (row.GetProperty("id").GetGuid(), row.GetProperty("name").GetString() ?? string.Empty))];
    }

    private static ProblemResponse? Problem(WireResponse response) =>
        JsonSerializer.Deserialize<ProblemResponse>(response.Body, JsonOptions);

    private static JsonContent NameBody(string name) => JsonContent.Create(new { name }, options: JsonOptions);

    private static Dictionary<string, string> KeyHeader(string? key) =>
        key is null ? NoHeaders : new Dictionary<string, string>(StringComparer.Ordinal) { [ProjectRouteScaffold.IdempotencyKeyHeader] = key };

    private static Task<WireResponse> CreateAsync(Actor actor, string name, Guid? spaceId = null, string? key = null) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Post, ProjectRouteScaffold.ListIn(spaceId ?? actor.SpaceId), NameBody(name), KeyHeader(key));

    private static Task<WireResponse> CreateWithTokenAsync(Actor actor, string token, Guid spaceId, string name, string? key = null) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Post, ProjectRouteScaffold.ListIn(spaceId), token, NameBody(name), KeyHeader(key));

    private static Task<WireResponse> RenameAsync(Actor actor, Guid spaceId, Guid projectId, string name, string? key = null) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Patch, ProjectRouteScaffold.ProjectOf(spaceId, projectId), NameBody(name), KeyHeader(key));

    private static Task<WireResponse> RenameWithTokenAsync(Actor actor, string token, Guid spaceId, Guid projectId, string name) =>
        actor.Session.SendWithTokenAndHeadersAsync(HttpMethod.Patch, ProjectRouteScaffold.ProjectOf(spaceId, projectId), token, NameBody(name), NoHeaders);

    /// <summary>
    /// Registers and signs in through the product, re-fetches the anti-forgery token (one minted while
    /// anonymous is refused once a Session exists), and reads the Personal Space and the Account id.
    /// </summary>
    private static async Task<Actor> RegisterAsync(HttpClient client, RegistrationDatabase database)
    {
        var session = new WireSession(client);
        var address = $"project-{Guid.NewGuid():N}@yello.test";

        await session.FetchAntiforgeryTokenAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await session.RegisterAsync(new RegisterAccountRequest("Pia-" + Guid.NewGuid().ToString("N")[..8], address, Password))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await session.SignInAsync(address, Password)).StatusCode);
        await session.FetchAntiforgeryTokenAsync();

        var spaces = await session.GetAsync(SpaceRoutes.Directory);

        using var document = JsonDocument.Parse(spaces.Body);
        var spaceId = document.RootElement.EnumerateArray().Single().GetProperty("id").GetGuid();

        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Id FROM dbo.[Account] WHERE NormalizedEmailAddress = @address";
        command.Parameters.Add(new SqlParameter("@address", EmailAddressNormalisation.Normalise(address)));

        var accountId = Assert.IsType<Guid>(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        return new Actor(session, accountId, spaceId);
    }

    private static async Task<string> IssueTokenAsync(Actor actor, Guid spaceId)
    {
        var issued = await actor.Session.PostAsync(ApiTokenRoutes.IssueIn(spaceId), JsonContent.Create(new IssueApiTokenRequest("project script"), options: JsonOptions));

        Assert.True(issued.StatusCode == HttpStatusCode.Created, $"{issued.StatusCode} {issued.Body}");

        using var document = JsonDocument.Parse(issued.Body);

        return document.RootElement.GetProperty("token").GetString() ?? string.Empty;
    }

    /// <summary>
    /// A non-Owner Membership for an Account the product registered, written under the Space's own key
    /// (no endpoint adds a member until Epic 3). Roles are stored by name (<c>MembershipConfiguration</c>).
    /// </summary>
    private static async Task AddMemberAsync(RegistrationDatabase database, Guid spaceId, Guid accountId, string role)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText =
            "INSERT dbo.[Membership] (Id, SpaceId, AccountId, Role, CreatedAt) VALUES (@id, @space, @account, @role, SYSDATETIMEOFFSET())";
        command.Parameters.Add(new SqlParameter("@id", Guid.NewGuid()));
        command.Parameters.Add(new SqlParameter("@space", spaceId));
        command.Parameters.Add(new SqlParameter("@account", accountId));
        command.Parameters.Add(new SqlParameter("@role", SqlDbType.NVarChar, 32) { Value = role });

        Assert.Equal(1, await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// <paramref name="count"/> Projects written by SQL under the Space's own key - the bound cases' way
    /// to reach 49 without 49 requests. See the class remarks for the constraints this satisfies.
    /// </summary>
    private static async Task SeedProjectsAsync(RegistrationDatabase database, Guid spaceId, int count)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);

        for (var index = 0; index < count; index++)
        {
            await using var command = connection.CreateCommand();

            command.CommandText = "INSERT dbo.[Project] (Id, SpaceId, Name, CreatedAt) VALUES (@id, @space, @name, SYSDATETIMEOFFSET())";
            command.Parameters.Add(new SqlParameter("@id", Guid.NewGuid()));
            command.Parameters.Add(new SqlParameter("@space", spaceId));
            command.Parameters.Add(new SqlParameter("@name", SqlDbType.NVarChar, 100) { Value = $"Seeded {index:D2}" });

            Assert.Equal(1, await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        }
    }

    private static async Task<IReadOnlyList<ProjectRow>> ProjectRowsAsync(RegistrationDatabase database, Guid spaceId, bool orderById = false)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        const string byId = "SELECT Id, SpaceId, Name, CreatedAt FROM dbo.[Project] ORDER BY Id";
        const string byName = "SELECT Id, SpaceId, Name, CreatedAt FROM dbo.[Project] ORDER BY Name, Id";

#pragma warning disable CA2100 // Review SQL queries - one of two constant statements.
        command.CommandText = orderById ? byId : byName;
#pragma warning restore CA2100

        var rows = new List<ProjectRow>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add(new ProjectRow(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), await reader.GetFieldValueAsync<DateTimeOffset>(3, TestContext.Current.CancellationToken)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<Guid>> StatusIdsAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Id FROM dbo.[StatusDefinition]";

        var ids = new List<Guid>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    /// <summary>
    /// The kind of every refusal recorded for (Account, Space). <c>AccessRefusal</c> carries no policy.
    /// </summary>
    private static async Task<IReadOnlyList<string>> RefusalKindsAsync(RegistrationDatabase database, Guid accountId, Guid spaceId)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Kind FROM dbo.[AccessRefusal] WHERE AccountId = @account AND TargetSpaceId = @space ORDER BY OccurredAt, Id";
        command.Parameters.Add(new SqlParameter("@account", accountId));
        command.Parameters.Add(new SqlParameter("@space", spaceId));

        var kinds = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            kinds.Add(reader.GetString(0));
        }

        return kinds;
    }

    /// <summary>
    /// The first column of a catalogue query, as text, on a connection with no Space context.
    /// </summary>
    private static async Task<IReadOnlyList<string>> CatalogueAsync(RegistrationDatabase database, string sql)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Review SQL queries - constant catalogue queries written by this class.
        command.CommandText = sql;
#pragma warning restore CA2100

        var values = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            values.Add(Convert.ToString(reader.GetValue(0), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        }

        return values;
    }

    private RegistrationDatabase Available()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        return fixture.Database!;
    }

    /// <summary>
    /// A registered, signed-in Account and its Personal Space.
    /// </summary>
    /// <param name="Session">Its browser-shaped client.</param>
    /// <param name="AccountId">Its id.</param>
    /// <param name="SpaceId">Its Personal Space.</param>
    private sealed record Actor(WireSession Session, Guid AccountId, Guid SpaceId);

    /// <summary>
    /// One <c>dbo.Project</c> row as story 2.1 Task 3 shapes it.
    /// </summary>
    /// <param name="Id">The id.</param>
    /// <param name="SpaceId">The Space, fixed at creation.</param>
    /// <param name="Name">The name.</param>
    /// <param name="CreatedAt">When it was created, UTC.</param>
    private sealed record ProjectRow(Guid Id, Guid SpaceId, string Name, DateTimeOffset CreatedAt);
}
