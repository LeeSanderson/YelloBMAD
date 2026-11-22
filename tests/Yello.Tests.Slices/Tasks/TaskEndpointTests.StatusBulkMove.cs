using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Domain;
using Yello.Domain.Bounds;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using static Yello.Tests.Slices.Tasks.ProjectMoveSliceWire;
using static Yello.Tests.Slices.Tasks.StatusBulkMoveSliceWire;
using static Yello.Tests.Slices.Tasks.TaskEditWire;
using static Yello.Tests.Slices.Tasks.TaskWire;
using RemovalWire = Yello.Tests.Slices.Projects.StatusRemovalWire;
using StatusWire = Yello.Tests.Slices.Projects.ProjectStatusSliceWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Moving every Task of one Status of a Project into another Project of its Space over the wire: the Status kept where the
/// destination holds it, a destination Status required or applied where it does not, the single move's refusals byte for
/// byte, a source that is not here answered as the boundary answers it, the destination's bound for all the Tasks at once,
/// what a move must leave alone, idempotency, atomicity under a forced failure, the Role and the boundary, and a Project
/// emptied Status by Status. No Task holds a Status outside its Project's set after any case.
/// </summary>
/// <remarks>
/// <para>
/// <b>Seeded through the product.</b> Every Project, Task, Label, Assignee and Status here is written through the product;
/// only the bound seed is SQL (<see cref="TaskWire.SeedTasksAsync"/>, whose constraints it states).
/// </para>
/// <para>
/// <b>The route is a stand-in</b> (<see cref="StatusBulkMoveWire.Route"/>) until the contract declares it. The body is the
/// single move's: a destination Project and an optional destination Status.
/// </para>
/// </remarks>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// With no <c>statusId</c> and a destination whose set holds the source Status, on either surface: <c>200</c> stating the
    /// destination, the kept Status and three moved; the three are at the end of a destination column that already held three
    /// Tasks, in their source order; and no other Task in either Project changes its Status or key.
    /// </summary>
    /// <param name="surface">The surface the move is sent on.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC1, AC4, AC8 - the bulk move route is not mapped yet")]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "FR-41")]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "AD-15")]
    public async Task A_Status_the_destination_holds_is_kept_and_its_Tasks_land_last_in_source_order(IsolationSurface surface)
    {
        var database = Available();
        Actor? owner = null;
        TwoProjects? projects = null;
        IReadOnlyList<StatusRow> defaults = [];
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> othersBefore = [];
        var lastBefore = string.Empty;
        WireResponse? moved = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var token = await IssueTokenAsync(owner, owner.SpaceId);

            // The destination column holds as many Tasks as the source, so keys derived from the source keys would collide
            // with the destination's or sort inside it, never after its last.
            projects = await TwoProjectsAsync(owner, ["Alpha", "Bravo", "Charlie"], ["Xray", "Yankee", "Zulu"]);
            defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);
            _ = await TasksInStatusAsync(owner, projects.SourceId, defaults[1].Id, defaults[0].Id, "Stays behind");
            othersBefore = await PlacementsOutsideAsync(database, owner.SpaceId, projects, projects.InSource);
            lastBefore = await MoveWire.KeyOfAsync(database, owner.SpaceId, projects.InDestination[2]);

            moved = await BulkMoveOnAsync(surface, owner, token, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId));
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        AssertBulkMoved(moved, projects.DestinationId, defaults[0].Id, 3);
        Assert.Equal(
            [.. projects.InDestination, .. projects.InSource],
            await MoveWire.ColumnAsync(database, owner.SpaceId, projects.DestinationId, defaults[0].Id));
        MoveWire.AssertStrictlyBetween(lastBefore, await MoveWire.KeyOfAsync(database, owner.SpaceId, projects.InSource[0]), upper: null);
        Assert.Empty(await MoveWire.ColumnAsync(database, owner.SpaceId, projects.SourceId, defaults[0].Id));
        Assert.Equal(othersBefore, await PlacementsOutsideAsync(database, owner.SpaceId, projects, projects.InSource));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A source Status the destination's set lacks - a default the destination removed, or a Status the source added - and no
    /// destination Status, absent or <c>null</c>: <c>409 status-destination-required</c>, and no Task in the Space changes.
    /// </summary>
    /// <param name="cause">Why the destination lacks the source Status.</param>
    /// <param name="explicitNull">Whether <c>statusId</c> is sent as <c>null</c> rather than left out.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC4 - the bulk move route is not mapped yet")]
    [InlineData("removed-default", false)]
    [InlineData("removed-default", true)]
    [InlineData("source-added", false)]
    [InlineData("source-added", true)]
    [Trait("Requirement", "FR-41")]
    [Trait("Requirement", "AR-24")]
    [Trait("Requirement", "AD-17")]
    public async Task A_Status_the_destination_lacks_needs_a_destination_Status_and_nothing_is_written(string cause, bool explicitNull)
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<TaskRow> before = [];
        WireResponse? refused = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projects = await TwoProjectsAsync(owner, ["Moving", "Also moving"], []);
            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);
            var source = await SourceStatusTheDestinationLacksAsync(owner, projects, defaults, cause);

            before = await TaskRowsAsync(database, owner.SpaceId);
            refused = await BulkMoveAsync(
                owner,
                owner.SpaceId,
                projects.SourceId,
                source,
                explicitNull ? ProjectMoveWire.Body(projects.DestinationId, statusId: null) : ProjectMoveWire.Body(projects.DestinationId));
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(refused, HttpStatusCode.Conflict, ProblemTypes.StatusDestinationRequired);
        Assert.Equal(before, await TaskRowsAsync(database, owner.SpaceId));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With a destination Status the destination's set holds - one of its defaults, or a Status it added itself - the move is
    /// admitted: <c>200</c> stating that Status, every Task in it at the end of its column in source order.
    /// </summary>
    /// <param name="destination">Which kind of destination Status is supplied.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC4, AC8 - the bulk move route is not mapped yet")]
    [InlineData("default")]
    [InlineData("destination-added")]
    [Trait("Requirement", "FR-41")]
    [Trait("Requirement", "AR-24")]
    [Trait("Requirement", "AD-15")]
    public async Task A_destination_Status_the_destination_holds_is_applied_to_every_Task_at_the_end_of_its_column(string destination)
    {
        var database = Available();
        Actor? owner = null;
        TwoProjects? projects = null;
        var chosen = Guid.Empty;
        WireResponse? moved = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Moving", "Also moving"], ["Resident"]);

            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            // The destination removes the source Status; its resident goes to the second default.
            _ = await RemovalWire.RemovedAsync(owner, owner.SpaceId, projects.DestinationId, defaults[0].Id, defaults[1].Id);

            chosen = destination == "default"
                ? defaults[1].Id
                : await StatusWire.AddedAsync(owner, owner.SpaceId, projects.DestinationId, "Review " + Guid.NewGuid().ToString("N")[..6]);

            if (destination != "default")
            {
                Assert.Equal(HttpStatusCode.OK, (await MoveWire.MoveTaskAsync(owner, owner.SpaceId, projects.InDestination[0], chosen, afterTaskId: null)).StatusCode);
            }

            moved = await BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId, chosen));
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        AssertBulkMoved(moved, projects.DestinationId, chosen, 2);
        Assert.Equal(
            [projects.InDestination[0], .. projects.InSource],
            await MoveWire.ColumnAsync(database, owner.SpaceId, projects.DestinationId, chosen));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A supplied destination Status the destination holds is applied even when the source Status would have been kept, as
    /// the single move applies it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC4 - the bulk move route is not mapped yet (Q5)")]
    [Trait("Requirement", "FR-41")]
    public async Task A_supplied_Status_is_applied_even_when_the_source_Status_would_be_kept()
    {
        var database = Available();
        Actor? owner = null;
        TwoProjects? projects = null;
        IReadOnlyList<StatusRow> defaults = [];
        WireResponse? moved = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Moving", "Also moving"], []);
            defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            moved = await BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId, defaults[2].Id));
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        AssertBulkMoved(moved, projects.DestinationId, defaults[2].Id, 2);
        Assert.Equal(projects.InSource, await MoveWire.ColumnAsync(database, owner.SpaceId, projects.DestinationId, defaults[2].Id));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A supplied destination Status the destination's set does not hold - the source's own added Status, another Space's
    /// Status, a default the destination removed - is refused <c>400 invalid-status-destination</c> byte for byte as a random
    /// id is, and as the single move refuses the same Status; nothing is written.
    /// </summary>
    /// <param name="kind">The Status supplied.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC4 - the bulk move route is not mapped yet")]
    [InlineData("source-added")]
    [InlineData("foreign")]
    [InlineData("removed-default")]
    [Trait("Requirement", "FR-20")]
    [Trait("Requirement", "FR-26")]
    [Trait("Requirement", "AD-2")]
    public async Task A_destination_Status_outside_the_destinations_set_is_refused_400_as_a_random_id_and_the_single_move_are(string kind)
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<TaskRow> before = [];
        WireResponse? refused = null;
        WireResponse? random = null;
        WireResponse? single = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var other = await RegisterAsync(client, database);
            var projects = await TwoProjectsAsync(owner, ["Moving", "Also moving"], []);
            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            var supplied = kind switch
            {
                "source-added" => await StatusWire.AddedAsync(owner, owner.SpaceId, projects.SourceId, "Source only " + Guid.NewGuid().ToString("N")[..6]),
                "foreign" => (await StatusesAsync(database, other.SpaceId))[0].Id,
                _ => await RemovedDefaultAsync(owner, projects.DestinationId, defaults[2].Id),
            };

            before = await TaskRowsAsync(database, owner.SpaceId);
            refused = await BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId, supplied));
            random = await BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId, Guid.NewGuid()));
            single = await MoveToProjectAsync(owner, owner.SpaceId, projects.InSource[0], ProjectMoveWire.Body(projects.DestinationId, supplied));
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(refused, HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusDestination, random);
        RemovalWire.AssertRefused(refused, HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusDestination, single);
        Assert.Equal(before, await TaskRowsAsync(database, owner.SpaceId));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A destination that is not another Project of the request's Space - another Space's real Project, or the source itself -
    /// is refused <c>400 invalid-task-project</c> byte for byte as a random id is, and as the single move refuses it; nothing
    /// is written and nothing recorded.
    /// </summary>
    /// <param name="kind">The destination named.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC5 - the bulk move route is not mapped yet (Q4)")]
    [InlineData("foreign")]
    [InlineData("source")]
    [Trait("Requirement", "FR-41")]
    [Trait("Requirement", "FR-19")]
    [Trait("Requirement", "AD-2")]
    public async Task A_destination_that_is_not_another_Project_of_the_Space_is_refused_400_as_a_random_id_and_the_single_move_are(string kind)
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<TaskRow> before = [];
        WireResponse? refused = null;
        WireResponse? random = null;
        WireResponse? single = null;
        IReadOnlyList<string> recorded = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var other = await RegisterAsync(client, database);
            var foreign = await CreateProjectAsync(other, "Theirs");
            var projects = await TwoProjectsAsync(owner, ["Moving"], []);
            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);
            var named = kind == "foreign" ? foreign : projects.SourceId;

            before = await TaskRowsAsync(database, owner.SpaceId);
            refused = await BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(named));
            random = await BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(Guid.NewGuid()));
            single = await MoveToProjectAsync(owner, owner.SpaceId, projects.InSource[0], ProjectMoveWire.Body(named));
            recorded = await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId);
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(refused, HttpStatusCode.BadRequest, ProblemTypes.InvalidTaskProject, random);
        RemovalWire.AssertRefused(refused, HttpStatusCode.BadRequest, ProblemTypes.InvalidTaskProject, single);
        Assert.Equal(before, await TaskRowsAsync(database, owner.SpaceId));
        Assert.Empty(recorded);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A source that is not here - a random or another Space's Project in the path, or a Status that is not in the source's
    /// effective set (random, another Space's, another Project's added Status, a default the source removed) - is the boundary
    /// <c>404</c>, byte for byte as a random Project is, recorded nowhere; nothing is written.
    /// </summary>
    /// <param name="kind">What the path names.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC3 - the bulk move route is not mapped yet (Q4)")]
    [InlineData("foreign-project")]
    [InlineData("random-status")]
    [InlineData("foreign-status")]
    [InlineData("sibling-added-status")]
    [InlineData("removed-from-source")]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-20")]
    [Trait("Requirement", "NFR-1")]
    public async Task A_source_that_is_not_here_answers_the_boundary_404_as_a_random_Project_does(string kind)
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<TaskRow> before = [];
        WireResponse? refused = null;
        WireResponse? random = null;
        IReadOnlyList<string> recorded = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var other = await RegisterAsync(client, database);
            var theirs = await TwoProjectsAsync(other, ["Theirs"], []);
            var projects = await TwoProjectsAsync(owner, ["Mine"], []);
            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);
            var body = ProjectMoveWire.Body(projects.DestinationId);

            var (projectId, statusId) = kind switch
            {
                "foreign-project" => (theirs.SourceId, defaults[0].Id),
                "random-status" => (projects.SourceId, Guid.NewGuid()),
                "foreign-status" => (projects.SourceId, (await StatusesAsync(database, other.SpaceId))[0].Id),
                "sibling-added-status" => (projects.SourceId, await StatusWire.AddedAsync(owner, owner.SpaceId, projects.DestinationId, "Elsewhere " + Guid.NewGuid().ToString("N")[..6])),
                _ => (projects.SourceId, await RemovedDefaultAsync(owner, projects.SourceId, defaults[2].Id)),
            };

            before = await TaskRowsAsync(database, owner.SpaceId);
            refused = await BulkMoveAsync(owner, owner.SpaceId, projectId, statusId, body);
            random = await BulkMoveAsync(owner, owner.SpaceId, Guid.NewGuid(), defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId));
            recorded = await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId);
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(refused, HttpStatusCode.NotFound, ProblemTypes.NotAvailable, random);
        Assert.Equal(before, await TaskRowsAsync(database, owner.SpaceId));
        Assert.Empty(recorded);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// The body is read as a document before anything else and never bound: one that is unreadable, not an object, leaves out
    /// <c>projectId</c>, or carries a position, a Space or a list of Tasks is <c>400 invalid-task-edit</c>; a <c>projectId</c>
    /// that is not an id is <c>400 invalid-task-project</c>; a <c>statusId</c> that is neither an id nor <c>null</c> is
    /// <c>400 invalid-status-destination</c>. Nothing is written. <c>{p}</c> stands for a real destination and <c>{s}</c> for
    /// the caller's own Space.
    /// </summary>
    /// <param name="json">The body, with its placeholders.</param>
    /// <param name="problemType">The refusal it meets.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC1 - the bulk move route is not mapped yet")]
    [InlineData("not json", "urn:yello:problem:invalid-task-edit")]
    [InlineData("[]", "urn:yello:problem:invalid-task-edit")]
    [InlineData("{}", "urn:yello:problem:invalid-task-edit")]
    [InlineData("{\"projectId\":\"{p}\",\"positionKey\":\"V\"}", "urn:yello:problem:invalid-task-edit")]
    [InlineData("{\"projectId\":\"{p}\",\"afterTaskId\":null}", "urn:yello:problem:invalid-task-edit")]
    [InlineData("{\"projectId\":\"{p}\",\"spaceId\":\"{s}\"}", "urn:yello:problem:invalid-task-edit")]
    [InlineData("{\"projectId\":\"{p}\",\"taskIds\":[]}", "urn:yello:problem:invalid-task-edit")]
    [InlineData("{\"projectId\":\"not-an-id\"}", "urn:yello:problem:invalid-task-project")]
    [InlineData("{\"projectId\":\"{p}\",\"statusId\":\"not-an-id\"}", "urn:yello:problem:invalid-status-destination")]
    [InlineData("{\"projectId\":\"{p}\",\"statusId\":7}", "urn:yello:problem:invalid-status-destination")]
    [Trait("Requirement", "AD-15")]
    [Trait("Requirement", "FR-19")]
    public async Task A_bulk_move_body_that_is_not_a_move_to_a_Project_is_refused_400_and_nothing_is_written(string json, string problemType)
    {
        var database = Available();
        Actor? owner = null;
        IReadOnlyList<TaskRow> before = [];
        WireResponse? refused = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projects = await TwoProjectsAsync(owner, ["Moving"], []);
            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);
            var body = json
                .Replace("{p}", projects.DestinationId.ToString("D"), StringComparison.Ordinal)
                .Replace("{s}", owner.SpaceId.ToString("D"), StringComparison.Ordinal);

            before = await TaskRowsAsync(database, owner.SpaceId);
            refused = await BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.RawBody(body));
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(refused, HttpStatusCode.BadRequest, problemType);
        Assert.Equal(before, await TaskRowsAsync(database, owner.SpaceId));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A Status holding no Tasks answers <c>200</c> with nothing moved and writes nothing - even one the destination lacks,
    /// because a destination Status is required only for Tasks that would land outside its set.
    /// </summary>
    /// <param name="kind">The empty Status: a default, or one the source added and the destination lacks.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC2, AC6 - the bulk move route is not mapped yet (Q5)")]
    [InlineData("default")]
    [InlineData("source-added")]
    [Trait("Requirement", "FR-41")]
    [Trait("Requirement", "AD-17")]
    public async Task An_empty_Status_answers_200_with_nothing_moved(string kind)
    {
        var database = Available();
        Actor? owner = null;
        TwoProjects? projects = null;
        IReadOnlyList<TaskRow> before = [];
        WireResponse? moved = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Not in the moved Status"], ["Resident"]);

            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);
            var empty = kind == "default"
                ? defaults[1].Id
                : await StatusWire.AddedAsync(owner, owner.SpaceId, projects.SourceId, "Empty " + Guid.NewGuid().ToString("N")[..6]);

            before = await TaskRowsAsync(database, owner.SpaceId);
            moved = await BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, empty, ProjectMoveWire.Body(projects.DestinationId));
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        Assert.NotNull(moved);
        Assert.True(moved.StatusCode == HttpStatusCode.OK, $"{moved.StatusCode} {moved.Body}");
        Assert.Equal(projects.DestinationId, ProjectMoveWire.ProjectIdIn(moved.Body));
        Assert.Equal(0, StatusBulkMoveWire.MovedCountIn(moved.Body));
        Assert.Equal(before, await TaskRowsAsync(database, owner.SpaceId));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// An empty Status still validates a supplied destination Status first: one the destination does not hold is
    /// <c>400 invalid-status-destination</c>, not <c>200</c> with nothing moved.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2, AC4 - the bulk move route is not mapped yet (Q5)")]
    [Trait("Requirement", "FR-41")]
    [Trait("Requirement", "FR-20")]
    public async Task An_empty_Status_still_refuses_a_destination_Status_outside_the_destinations_set()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? refused = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projects = await TwoProjectsAsync(owner, ["Not in the moved Status"], []);
            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            refused = await BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[1].Id, ProjectMoveWire.Body(projects.DestinationId, Guid.NewGuid()));
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(refused, HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusDestination);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// What each moved Task carries survives - its id, Space, title, due date, Assignee, Labels in their order and their sort
    /// key, and its creation time, each seeded non-null on two Tasks - the move sends no email, and each Task's own URL then
    /// answers the same id with the destination as its Project. Anchored on a later re-assignment's email arriving: anything
    /// the move had enqueued would have been enqueued before it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC12 - the bulk move route is not mapped yet")]
    [Trait("Requirement", "FR-41")]
    [Trait("Requirement", "FR-21")]
    [Trait("Requirement", "AD-10")]
    public async Task What_each_Task_carries_survives_the_bulk_move_and_no_email_is_sent()
    {
        var database = Available();
        var wire = new WireDatabase(fixture.ConnectionString!);
        Actor? owner = null;
        TwoProjects? projects = null;
        var before = new List<TaskSurvivors>();
        var after = new List<TaskSurvivors>();
        var labelsBefore = new List<IReadOnlyList<Guid>>();
        var labelsAfter = new List<IReadOnlyList<Guid>>();
        var reads = new List<WireResponse>();
        WireResponse? moved = null;
        IReadOnlyList<DroppedEmail> dropped = [];
        IReadOnlyList<OutboxRow> outbox = [];
        string[] titles = ["Carries everything " + Guid.NewGuid().ToString("N")[..8], "Carries everything too " + Guid.NewGuid().ToString("N")[..8]];

        using var mail = new MailDrop();

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            owner = await RegisterAsync(client, database);

            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            var membershipId = (await wire.MembershipOfAsync(owner.SpaceId, member.AccountId)).Id;
            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);
            var bravo = await LabelWire.SeedAsync(owner, owner.SpaceId, "Bravo " + Guid.NewGuid().ToString("N")[..6]);
            var alpha = await LabelWire.SeedAsync(owner, owner.SpaceId, "Alpha " + Guid.NewGuid().ToString("N")[..6]);

            projects = await TwoProjectsAsync(owner, titles, []);

            foreach (var taskId in projects.InSource)
            {
                Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, taskId, Body(new { labelIds = new[] { bravo, alpha }, dueDate = "2026-12-01T00:00:00+00:00" }))).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await AssignAsync(owner, taskId, membershipId)).StatusCode);
            }

            _ = await mail.WaitForAsync(2);

            foreach (var taskId in projects.InSource)
            {
                before.Add(await SurvivorsAsync(database, owner.SpaceId, taskId));
                labelsBefore.Add(await LabelWire.AppliedToAsync(database, owner.SpaceId, taskId));
            }

            moved = await BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId));

            foreach (var taskId in projects.InSource)
            {
                after.Add(await SurvivorsAsync(database, owner.SpaceId, taskId));
                labelsAfter.Add(await LabelWire.AppliedToAsync(database, owner.SpaceId, taskId));
                reads.Add(await ViewTaskAsync(owner, owner.SpaceId, taskId));
            }

            Assert.Equal(HttpStatusCode.OK, (await AssignAsync(owner, projects.InSource[0], membershipId: null)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await AssignAsync(owner, projects.InSource[0], membershipId)).StatusCode);
            dropped = await mail.WaitForAsync(3);
            outbox = await wire.OutboxRowsAsync();
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        AssertBulkMoved(moved, projects.DestinationId, (await StatusWire.DefaultsAsync(database, owner.SpaceId))[0].Id, 2);
        Assert.All(before, survivor =>
        {
            Assert.NotNull(survivor.DueDate);
            Assert.NotNull(survivor.AssigneeMembershipId);
            Assert.NotNull(survivor.FirstLabelName);
        });
        Assert.All(labelsBefore, labels => Assert.Equal(2, labels.Count));
        Assert.Equal(before, after);
        Assert.Equal(labelsBefore, labelsAfter);

        for (var index = 0; index < reads.Count; index++)
        {
            Assert.True(reads[index].StatusCode == HttpStatusCode.OK, $"{reads[index].StatusCode} {reads[index].Body}");
            Assert.Equal(projects.InSource[index], IdIn(reads[index].Body));
            Assert.Equal(projects.DestinationId, ProjectMoveWire.ProjectIdIn(reads[index].Body));
        }

        Assert.Equal(3, dropped.Count);
        Assert.All(dropped, email => Assert.True(AssignmentWire.IsAssignmentEmail(email), $"Not an assignment email: {email.Subject}"));
        Assert.DoesNotContain(outbox, row => Array.Exists(titles, title => row.Body.Contains(title, StringComparison.Ordinal)) && !row.Subject.Contains("assigned you a Task", StringComparison.Ordinal));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// The destination's Tasks-per-Project bound counts every Task the move adds: at 4,999 a move of two is refused
    /// <c>409 bound-reached</c>, byte for byte as a creation into a full Project is, and nothing is written; at 4,998 the same
    /// move lands the destination on exactly 5,000. The source's own count is never consulted.
    /// </summary>
    /// <param name="destinationHolds">The destination's Tasks before the move.</param>
    /// <param name="admitted">Whether the move of two is admitted.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC7 - the bulk move route is not mapped yet (Q6)")]
    [InlineData(4_999, false)]
    [InlineData(4_998, true)]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "AD-25")]
    [Trait("Requirement", "AR-30")]
    public async Task The_destinations_bound_counts_every_Task_the_move_adds(int destinationHolds, bool admitted)
    {
        var database = Available();
        var limit = BoundRegistry.For(Bound.TasksPerProject).Limit;
        Actor? owner = null;
        TwoProjects? projects = null;
        IReadOnlyList<StatusRow> defaults = [];
        IReadOnlyList<TaskRow> sourceBefore = [];
        var heldAfterTheMove = 0;
        WireResponse? answer = null;
        WireResponse? creationRefused = null;

        Assert.Equal(5_000, limit);

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["First of two", "Second of two"], []);
            defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            await SeedTasksAsync(database, owner.SpaceId, projects.DestinationId, defaults[0].Id, destinationHolds);

            sourceBefore = await SourceRowsAsync(database, owner.SpaceId, projects.SourceId);
            answer = await BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId));
            heldAfterTheMove = await TaskCountAsync(database, owner.SpaceId, projects.DestinationId);

            if (!admitted)
            {
                // The anchor: fill the destination's last place, then a creation meets the bound.
                Assert.Equal(HttpStatusCode.Created, (await CreateTaskAsync(owner, owner.SpaceId, projects.DestinationId, "The last place")).StatusCode);
                creationRefused = await CreateTaskAsync(owner, owner.SpaceId, projects.DestinationId, "One too many");
            }
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);

        if (admitted)
        {
            AssertBulkMoved(answer, projects.DestinationId, defaults[0].Id, 2);
            Assert.Equal(limit, heldAfterTheMove);
        }
        else
        {
            RemovalWire.AssertRefused(answer, HttpStatusCode.Conflict, ProblemTypes.BoundReached, creationRefused);
            Assert.Equal(destinationHolds, heldAfterTheMove);
            Assert.Equal(sourceBefore, await SourceRowsAsync(database, owner.SpaceId, projects.SourceId));
        }

        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A Viewer of the Space is refused <c>403 insufficient-role</c> naming <c>manage-tasks</c> on its Session and on its
    /// Token, each recorded once, and nothing moves. Anchors: a Member moves the Status over a Token, and an Admin moves it
    /// back over a Session.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC15 - the bulk move route is not mapped yet")]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "AD-20")]
    public async Task A_Viewer_is_refused_403_on_both_surfaces_a_Member_moves_over_a_Token_and_an_Admin_over_a_Session()
    {
        var database = Available();
        Actor? owner = null;
        Actor? viewer = null;
        TwoProjects? projects = null;
        IReadOnlyList<StatusRow> defaults = [];
        WireResponse? bySession = null;
        WireResponse? byToken = null;
        WireResponse? byMember = null;
        WireResponse? byAdmin = null;
        IReadOnlyList<TaskRow> before = [];
        IReadOnlyList<TaskRow> afterRefusals = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            viewer = await RegisterAsync(client, database);

            var member = await RegisterAsync(client, database);
            var admin = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, viewer.AccountId, "Viewer");
            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");
            await AddMemberAsync(database, owner.SpaceId, admin.AccountId, "Admin");

            projects = await TwoProjectsAsync(owner, ["Shared work", "More shared work"], []);
            defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);
            before = await TaskRowsAsync(database, owner.SpaceId);

            var viewerToken = await IssueTokenAsync(viewer, owner.SpaceId);
            var memberToken = await IssueTokenAsync(member, owner.SpaceId);

            bySession = await BulkMoveAsync(viewer, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId));
            byToken = await BulkMoveWithTokenAsync(viewer, viewerToken, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId));
            afterRefusals = await TaskRowsAsync(database, owner.SpaceId);
            byMember = await BulkMoveWithTokenAsync(member, memberToken, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId));
            byAdmin = await BulkMoveAsync(admin, owner.SpaceId, projects.DestinationId, defaults[0].Id, ProjectMoveWire.Body(projects.SourceId));
        });

        Assert.NotNull(owner);
        Assert.NotNull(viewer);
        Assert.NotNull(projects);

        foreach (var refused in new[] { bySession!, byToken! })
        {
            Assert.True(refused.StatusCode == HttpStatusCode.Forbidden, $"{refused.StatusCode} {refused.Body}");
            Assert.Equal(ProblemTypes.InsufficientRole, Problem(refused)?.Type);
            Assert.Equal(StableCode.Of(Capability.ManageTasks), Problem(refused)?.Capability);
        }

        Assert.Equal([InsufficientRoleKind, InsufficientRoleKind], await RefusalKindsAsync(database, viewer.AccountId, owner.SpaceId));
        Assert.Equal(before, afterRefusals);
        AssertBulkMoved(byMember, projects.DestinationId, defaults[0].Id, 2);
        AssertBulkMoved(byAdmin, projects.SourceId, defaults[0].Id, 2);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Another Space in the path is the boundary <c>404</c>, byte for byte and header for header as a Space that does not
    /// exist, and recorded as a cross-Space attempt; the other Space's Tasks stay where they were.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3, AC16 - the bulk move route is not mapped yet")]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-20")]
    [Trait("Requirement", "NFR-1")]
    public async Task Another_Spaces_path_answers_the_boundary_404_recorded_and_nothing_moves()
    {
        var database = Available();
        Actor? owner = null;
        Actor? other = null;
        IReadOnlyList<TaskRow> theirsBefore = [];
        WireResponse? foreignSpace = null;
        WireResponse? absentSpace = null;
        IReadOnlyList<string> recordedInOwn = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            other = await RegisterAsync(client, database);

            var theirs = await TwoProjectsAsync(other, ["Theirs"], []);
            var theirDefaults = await StatusWire.DefaultsAsync(database, other.SpaceId);

            theirsBefore = await TaskRowsAsync(database, other.SpaceId);
            foreignSpace = await BulkMoveAsync(owner, other.SpaceId, theirs.SourceId, theirDefaults[0].Id, ProjectMoveWire.Body(theirs.DestinationId));
            absentSpace = await BulkMoveAsync(owner, Guid.NewGuid(), theirs.SourceId, theirDefaults[0].Id, ProjectMoveWire.Body(theirs.DestinationId));
            recordedInOwn = await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId);
        });

        Assert.NotNull(owner);
        Assert.NotNull(other);
        RemovalWire.AssertRefused(foreignSpace, HttpStatusCode.NotFound, ProblemTypes.NotAvailable, absentSpace);
        Assert.Equal(absentSpace!.HeaderNames, foreignSpace!.HeaderNames);
        Assert.Equal([CrossSpaceKind], await RefusalKindsAsync(database, owner.AccountId, other.SpaceId));
        Assert.Empty(recordedInOwn);
        Assert.Equal(theirsBefore, await TaskRowsAsync(database, other.SpaceId));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, other.SpaceId);
    }

    /// <summary>
    /// A replay of the same <c>Idempotency-Key</c> answers the original <c>200</c> byte for byte and moves nothing again: the
    /// Tasks, moved back to their source by another request in between, stay there.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 - the bulk move route is not mapped yet")]
    [Trait("Requirement", "AD-18")]
    public async Task A_replayed_key_answers_the_original_bulk_move_and_moves_nothing_again()
    {
        var database = Available();
        Actor? owner = null;
        TwoProjects? projects = null;
        IReadOnlyList<StatusRow> defaults = [];
        WireResponse? first = null;
        WireResponse? back = null;
        WireResponse? replay = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Moved once", "Also moved once"], []);
            defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            var key = Guid.NewGuid().ToString("N");

            first = await BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId), key);
            back = await BulkMoveAsync(owner, owner.SpaceId, projects.DestinationId, defaults[0].Id, ProjectMoveWire.Body(projects.SourceId));
            replay = await BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId), key);
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        AssertBulkMoved(first, projects.DestinationId, defaults[0].Id, 2);
        AssertBulkMoved(back, projects.SourceId, defaults[0].Id, 2);
        Assert.Equal(first!.StatusCode, replay?.StatusCode);
        Assert.Equal(first.Body, replay?.Body);
        Assert.Equal(projects.InSource, await MoveWire.ColumnAsync(database, owner.SpaceId, projects.SourceId, defaults[0].Id));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With a test-only trigger failing the write of the last Task in source order, the move fails <c>5xx</c>: every selected
    /// Task keeps its Project, Status and key, the destination column is unchanged, and no Task holds a Status outside its
    /// Project's set. Anchored on the same request succeeding once the trigger is dropped.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC6, AC17 - the bulk move route is not mapped yet")]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "AR-24")]
    public async Task A_failure_forced_on_the_last_Task_written_leaves_every_Task_where_it_was()
    {
        var database = Available();
        Actor? owner = null;
        TwoProjects? projects = null;
        IReadOnlyList<StatusRow> defaults = [];
        IReadOnlyList<TaskRow> before = [];
        IReadOnlyList<TaskRow> afterFailure = [];
        WireResponse? failed = null;
        WireResponse? anchor = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["First", "Second", "Third"], ["Resident"]);
            defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            before = await TaskRowsAsync(database, owner.SpaceId);

            var trigger = await PlantFailingTaskUpdateTriggerAsync(database, projects.InSource[^1]);

            try
            {
                failed = await BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId));
                afterFailure = await TaskRowsAsync(database, owner.SpaceId);
            }
            finally
            {
                await RemovalWire.DropTriggerAsync(database, trigger);
            }

            anchor = await BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId));
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        Assert.NotNull(failed);
        Assert.True(failed.StatusCode >= HttpStatusCode.InternalServerError, $"{failed.StatusCode} {failed.Body}");
        Assert.Equal(before, afterFailure);
        AssertBulkMoved(anchor, projects.DestinationId, defaults[0].Id, 3);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A Project is emptied Status by Status - one Status the destination holds, moved and kept, and one the source added,
    /// mapped to a destination Status - after which its Board holds nothing and its deletion deletes no Task.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC19 - the bulk move route is not mapped yet")]
    [Trait("Requirement", "FR-41")]
    [Trait("Requirement", "AR-24")]
    public async Task A_Project_emptied_Status_by_Status_is_deleted_with_no_Task_in_it()
    {
        var database = Available();
        Actor? owner = null;
        TwoProjects? projects = null;
        IReadOnlyList<StatusRow> defaults = [];
        WireResponse? kept = null;
        WireResponse? mapped = null;
        WireResponse? sourceBoard = null;
        WireResponse? deleted = null;
        var tasksBeforeDeletion = 0;
        var tasksAfterDeletion = 0;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, [], ["Already there"]);
            defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            var parked = await StatusWire.AddedAsync(owner, owner.SpaceId, projects.SourceId, "Parked " + Guid.NewGuid().ToString("N")[..6]);

            _ = await TasksInStatusAsync(owner, projects.SourceId, defaults[2].Id, defaults[0].Id, "Finished one", "Finished two");
            _ = await TasksInStatusAsync(owner, projects.SourceId, parked, defaults[0].Id, "Set aside");

            kept = await BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[2].Id, ProjectMoveWire.Body(projects.DestinationId));
            mapped = await BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, parked, ProjectMoveWire.Body(projects.DestinationId, defaults[1].Id));
            sourceBoard = await BoardAsync(owner, owner.SpaceId, projects.SourceId);

            tasksBeforeDeletion = (await TaskRowsAsync(database, owner.SpaceId)).Count;
            deleted = await DeleteWire.DeleteProjectAsync(owner, owner.SpaceId, projects.SourceId);
            tasksAfterDeletion = (await TaskRowsAsync(database, owner.SpaceId)).Count;
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        AssertBulkMoved(kept, projects.DestinationId, defaults[2].Id, 2);
        AssertBulkMoved(mapped, projects.DestinationId, defaults[1].Id, 1);
        Assert.All(DeleteWire.Columns(sourceBoard!), column => Assert.Equal(0, column.Total));
        Assert.True(deleted?.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent, $"{deleted?.StatusCode} {deleted?.Body}");
        Assert.Equal(4, tasksBeforeDeletion);
        Assert.Equal(tasksBeforeDeletion, tasksAfterDeletion);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A Status removal still moves its Tasks within their own Project only: every Task of the removed Status keeps its
    /// Project, and another Project's Tasks in the same Status are untouched. The set-based migration a removal shares with
    /// the bulk move must keep the source Project in its predicate.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-26")]
    [Trait("Requirement", "AD-17")]
    public async Task A_Status_removal_moves_its_Tasks_within_their_own_Project_only()
    {
        var database = Available();
        Actor? owner = null;
        TwoProjects? projects = null;
        IReadOnlyList<StatusRow> defaults = [];
        IReadOnlyList<TaskRow> otherProjectBefore = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Removed from under", "Also removed from under"], ["Elsewhere", "Also elsewhere"]);
            defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);
            otherProjectBefore = await SourceRowsAsync(database, owner.SpaceId, projects.DestinationId);

            _ = await RemovalWire.RemovedAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, defaults[1].Id);
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);

        var removedFrom = await SourceRowsAsync(database, owner.SpaceId, projects.SourceId);

        Assert.Equal(projects.InSource.Order(), removedFrom.Select(row => row.Id).Order());
        Assert.All(removedFrom, row => Assert.Equal(defaults[1].Id, row.StatusId));
        Assert.Equal(otherProjectBefore, await SourceRowsAsync(database, owner.SpaceId, projects.DestinationId));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Gives the source's Tasks a Status the destination's set lacks: the destination removes the first default (it holds no
    /// Task there), or the source adds a Status and moves its Tasks into it. Answers the source Status.
    /// </summary>
    private static async Task<Guid> SourceStatusTheDestinationLacksAsync(Actor owner, TwoProjects projects, IReadOnlyList<StatusRow> defaults, string cause)
    {
        if (cause == "removed-default")
        {
            _ = await RemovalWire.RemovedAsync(owner, owner.SpaceId, projects.DestinationId, defaults[0].Id);

            return defaults[0].Id;
        }

        var added = await StatusWire.AddedAsync(owner, owner.SpaceId, projects.SourceId, "Source only " + Guid.NewGuid().ToString("N")[..6]);
        Guid? after = null;

        foreach (var taskId in projects.InSource)
        {
            Assert.Equal(HttpStatusCode.OK, (await MoveWire.MoveTaskAsync(owner, owner.SpaceId, taskId, added, after)).StatusCode);
            after = taskId;
        }

        return added;
    }

    /// <summary>
    /// Every Task row of one Project, in the database's order.
    /// </summary>
    private static async Task<IReadOnlyList<TaskRow>> SourceRowsAsync(RegistrationDatabase database, Guid spaceId, Guid projectId) =>
        [.. (await TaskRowsAsync(database, spaceId)).Where(row => row.ProjectId == projectId)];

    /// <summary>
    /// Every Task's placement in both Projects except the <paramref name="moved"/> ones, ordered by id.
    /// </summary>
    private static async Task<IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)>> PlacementsOutsideAsync(RegistrationDatabase database, Guid spaceId, TwoProjects projects, IReadOnlyList<Guid> moved) =>
    [
        .. (await MoveWire.PlacementsAsync(database, spaceId, projects.SourceId))
            .Concat(await MoveWire.PlacementsAsync(database, spaceId, projects.DestinationId))
            .Where(row => !moved.Contains(row.Id))
            .OrderBy(row => row.Id),
    ];
}
