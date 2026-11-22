using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Tasks;
using Yello.Domain;
using Yello.Domain.Bounds;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using static Yello.Tests.Slices.Tasks.ProjectMoveSliceWire;
using static Yello.Tests.Slices.Tasks.TaskEditWire;
using static Yello.Tests.Slices.Tasks.TaskWire;
using RemovalWire = Yello.Tests.Slices.Projects.StatusRemovalWire;
using StatusWire = Yello.Tests.Slices.Projects.ProjectStatusSliceWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Moving one Task into another Project of its Space over the wire: the Status kept where the destination holds it, a
/// destination Status required or applied where it does not, a destination that is not another Project of the Space
/// refused alike whatever the cause, the destination's Tasks-per-Project bound, what a move must leave alone, idempotency,
/// atomicity under a forced failure, the Role and the boundary. No Task holds a Status outside its Project's set after any
/// case.
/// </summary>
/// <remarks>
/// <para>
/// <b>Seeded through the product.</b> Every Project, Task, Label, Assignee and Status here is written through the product;
/// only the bound seed is SQL (<see cref="TaskWire.SeedTasksAsync"/>, whose constraints it states).
/// </para>
/// </remarks>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// A Task whose Status the destination's set holds moves with no <c>statusId</c>, on either surface: <c>200</c> with the
    /// Task as stored, its new Project stated; same Status; the last key of the destination's column; and no other Task in
    /// either Project changes its key.
    /// </summary>
    /// <param name="surface">The surface the move is sent on.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "FR-41")]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "AD-15")]
    public async Task A_Task_whose_Status_the_destination_holds_keeps_it_and_lands_last_in_its_column(IsolationSurface surface)
    {
        var database = Available();
        Actor? owner = null;
        TwoProjects? projects = null;
        IReadOnlyList<StatusRow> defaults = [];
        IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)> othersBefore = [];
        WireResponse? moved = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var token = await IssueTokenAsync(owner, owner.SpaceId);

            // The destination column holds more Tasks than precede the moved one in its source, so a key derived from the
            // source key would sort inside the destination column, or collide with one of its keys, never after its last.
            projects = await TwoProjectsAsync(owner, ["Alpha", "Bravo", "Charlie"], ["Xray", "Yankee", "Zulu"]);
            defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);
            othersBefore = await OtherPlacementsAsync(database, owner.SpaceId, projects, projects.InSource[1]);

            moved = await MoveToProjectOnAsync(surface, owner, token, projects.InSource[1], ProjectMoveWire.Body(projects.DestinationId));
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        AssertMovedInto(moved, projects.DestinationId);

        var (statusId, key) = MoveWire.PlacementOf(moved!);

        Assert.Equal(defaults[0].Id, statusId);
        Assert.Equal(
            [projects.InDestination[0], projects.InDestination[1], projects.InDestination[2], projects.InSource[1]],
            await MoveWire.ColumnAsync(database, owner.SpaceId, projects.DestinationId, defaults[0].Id));
        MoveWire.AssertStrictlyBetween(await MoveWire.KeyOfAsync(database, owner.SpaceId, projects.InDestination[2]), key, upper: null);
        Assert.Equal(othersBefore, await OtherPlacementsAsync(database, owner.SpaceId, projects, projects.InSource[1]));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A Task whose Status the destination's set lacks - a default the destination removed, or a Status the source added - and
    /// no destination Status, absent or <c>null</c>: <c>409 status-destination-required</c>, and the Task keeps its Project,
    /// Status and key.
    /// </summary>
    /// <param name="cause">Why the destination lacks the Task's Status.</param>
    /// <param name="explicitNull">Whether <c>statusId</c> is sent as <c>null</c> rather than left out.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("removed-default", false)]
    [InlineData("removed-default", true)]
    [InlineData("source-added", false)]
    [InlineData("source-added", true)]
    [Trait("Requirement", "FR-41")]
    [Trait("Requirement", "AR-24")]
    [Trait("Requirement", "AD-17")]
    public async Task A_Task_whose_Status_the_destination_lacks_needs_a_destination_Status_and_nothing_is_written(string cause, bool explicitNull)
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        TaskRow? before = null;
        WireResponse? refused = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projects = await TwoProjectsAsync(owner, ["Moving"], ["Resident"]);
            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            taskId = projects.InSource[0];
            await GiveAStatusTheDestinationLacksAsync(owner, projects, defaults, cause);
            before = await RowAsync(database, owner.SpaceId, taskId);

            refused = await MoveToProjectAsync(
                owner,
                owner.SpaceId,
                taskId,
                explicitNull ? ProjectMoveWire.Body(projects.DestinationId, statusId: null) : ProjectMoveWire.Body(projects.DestinationId));
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(refused, HttpStatusCode.Conflict, ProblemTypes.StatusDestinationRequired);
        Assert.Equal(before, await RowAsync(database, owner.SpaceId, taskId));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With a destination Status the destination's set holds - one of its defaults, or a Status it added itself - the move is
    /// admitted: <c>200</c>, in that Status, at the end of its column, in one write.
    /// </summary>
    /// <param name="destination">Which kind of destination Status is supplied.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("default")]
    [InlineData("destination-added")]
    [Trait("Requirement", "FR-41")]
    [Trait("Requirement", "AR-24")]
    [Trait("Requirement", "AD-17")]
    public async Task A_destination_Status_the_destination_holds_is_applied_at_the_end_of_its_column(string destination)
    {
        var database = Available();
        Actor? owner = null;
        TwoProjects? projects = null;
        var chosen = Guid.Empty;
        WireResponse? moved = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Moving"], ["Resident"]);

            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            await GiveAStatusTheDestinationLacksAsync(owner, projects, defaults, "removed-default");

            chosen = destination == "default"
                ? defaults[1].Id
                : await StatusWire.AddedAsync(owner, owner.SpaceId, projects.DestinationId, "Review " + Guid.NewGuid().ToString("N")[..6]);

            // A resident in the chosen column, so "the end" is after something.
            Assert.Equal(HttpStatusCode.OK, (await MoveWire.MoveTaskAsync(owner, owner.SpaceId, projects.InDestination[0], chosen, afterTaskId: null)).StatusCode);

            moved = await MoveToProjectAsync(owner, owner.SpaceId, projects.InSource[0], ProjectMoveWire.Body(projects.DestinationId, chosen));
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        AssertMovedInto(moved, projects.DestinationId);
        Assert.Equal(chosen, MoveWire.PlacementOf(moved!).StatusId);
        Assert.Equal(
            [projects.InDestination[0], projects.InSource[0]],
            await MoveWire.ColumnAsync(database, owner.SpaceId, projects.DestinationId, chosen));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A supplied destination Status the destination holds is applied even when the Task's own Status would have been kept:
    /// the request means "into this Project, in this Status" whatever the state.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-41")]
    public async Task A_supplied_Status_is_applied_even_when_the_current_one_would_be_kept()
    {
        var database = Available();
        Actor? owner = null;
        TwoProjects? projects = null;
        IReadOnlyList<StatusRow> defaults = [];
        WireResponse? moved = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Moving"], []);
            defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            moved = await MoveToProjectAsync(owner, owner.SpaceId, projects.InSource[0], ProjectMoveWire.Body(projects.DestinationId, defaults[2].Id));
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        AssertMovedInto(moved, projects.DestinationId);
        Assert.Equal(defaults[2].Id, MoveWire.PlacementOf(moved!).StatusId);
        Assert.Equal(defaults[2].Id, (await RowAsync(database, owner.SpaceId, projects.InSource[0]))?.StatusId);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A supplied destination Status the destination's set does not hold - the source's own added Status, another Space's
    /// Status, a default the destination removed - is refused <c>400 invalid-status-destination</c> byte for byte as a random
    /// id is, and nothing is written.
    /// </summary>
    /// <param name="kind">The Status supplied.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("source-added")]
    [InlineData("foreign")]
    [InlineData("removed-default")]
    [Trait("Requirement", "FR-20")]
    [Trait("Requirement", "FR-26")]
    [Trait("Requirement", "AD-2")]
    public async Task A_destination_Status_outside_the_destinations_set_is_refused_400_as_a_random_id_is(string kind)
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        TaskRow? before = null;
        WireResponse? refused = null;
        WireResponse? random = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var other = await RegisterAsync(client, database);
            var projects = await TwoProjectsAsync(owner, ["Moving"], []);
            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            taskId = projects.InSource[0];

            var supplied = kind switch
            {
                "source-added" => await StatusWire.AddedAsync(owner, owner.SpaceId, projects.SourceId, "Source only " + Guid.NewGuid().ToString("N")[..6]),
                "foreign" => (await StatusesAsync(database, other.SpaceId))[0].Id,
                _ => await RemovedDefaultAsync(owner, projects.DestinationId, defaults[2].Id),
            };

            before = await RowAsync(database, owner.SpaceId, taskId);
            refused = await MoveToProjectAsync(owner, owner.SpaceId, taskId, ProjectMoveWire.Body(projects.DestinationId, supplied));
            random = await MoveToProjectAsync(owner, owner.SpaceId, taskId, ProjectMoveWire.Body(projects.DestinationId, Guid.NewGuid()));
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(refused, HttpStatusCode.BadRequest, ProblemTypes.InvalidStatusDestination, random);
        Assert.Equal(before, await RowAsync(database, owner.SpaceId, taskId));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A destination that is not another Project of the request's Space - another Space's real Project, or the Task's own -
    /// is refused <c>400 invalid-task-project</c> byte for byte as a random id is; nothing is written and nothing recorded.
    /// </summary>
    /// <param name="kind">The destination named.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("foreign")]
    [InlineData("own")]
    [Trait("Requirement", "FR-41")]
    [Trait("Requirement", "FR-19")]
    [Trait("Requirement", "AD-2")]
    public async Task A_destination_that_is_not_another_Project_of_the_Space_is_refused_400_as_a_random_id_is(string kind)
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        TaskRow? before = null;
        WireResponse? refused = null;
        WireResponse? random = null;
        IReadOnlyList<string> recorded = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var other = await RegisterAsync(client, database);
            var foreign = await CreateProjectAsync(other, "Theirs");
            var projects = await TwoProjectsAsync(owner, ["Moving"], []);

            taskId = projects.InSource[0];
            before = await RowAsync(database, owner.SpaceId, taskId);
            refused = await MoveToProjectAsync(owner, owner.SpaceId, taskId, ProjectMoveWire.Body(kind == "foreign" ? foreign : projects.SourceId));
            random = await MoveToProjectAsync(owner, owner.SpaceId, taskId, ProjectMoveWire.Body(Guid.NewGuid()));
            recorded = await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId);
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(refused, HttpStatusCode.BadRequest, ProblemTypes.InvalidTaskProject, random);
        Assert.Equal(before, await RowAsync(database, owner.SpaceId, taskId));
        Assert.Empty(recorded);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// The body is read as a document and never bound: one that is unreadable, not an object, leaves out <c>projectId</c>, or
    /// carries a position or a Space is <c>400 invalid-task-edit</c>; a <c>projectId</c> that is not an id is
    /// <c>400 invalid-task-project</c>; a <c>statusId</c> that is neither an id nor <c>null</c> is
    /// <c>400 invalid-status-destination</c>. Nothing is written. <c>{p}</c> stands for a real destination and <c>{s}</c> for
    /// the caller's own Space.
    /// </summary>
    /// <param name="json">The body, with its placeholders.</param>
    /// <param name="problemType">The refusal it meets.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("not json", "urn:yello:problem:invalid-task-edit")]
    [InlineData("[]", "urn:yello:problem:invalid-task-edit")]
    [InlineData("{}", "urn:yello:problem:invalid-task-edit")]
    [InlineData("{\"projectId\":\"{p}\",\"positionKey\":\"V\"}", "urn:yello:problem:invalid-task-edit")]
    [InlineData("{\"projectId\":\"{p}\",\"afterTaskId\":null}", "urn:yello:problem:invalid-task-edit")]
    [InlineData("{\"projectId\":\"{p}\",\"spaceId\":\"{s}\"}", "urn:yello:problem:invalid-task-edit")]
    [InlineData("{\"projectId\":\"not-an-id\"}", "urn:yello:problem:invalid-task-project")]
    [InlineData("{\"projectId\":\"{p}\",\"statusId\":\"not-an-id\"}", "urn:yello:problem:invalid-status-destination")]
    [InlineData("{\"projectId\":\"{p}\",\"statusId\":7}", "urn:yello:problem:invalid-status-destination")]
    [Trait("Requirement", "AD-15")]
    [Trait("Requirement", "FR-19")]
    public async Task A_body_that_is_not_a_move_to_a_Project_is_refused_400_and_nothing_is_written(string json, string problemType)
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        TaskRow? before = null;
        WireResponse? refused = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projects = await TwoProjectsAsync(owner, ["Moving"], []);
            var body = json
                .Replace("{p}", projects.DestinationId.ToString("D"), StringComparison.Ordinal)
                .Replace("{s}", owner.SpaceId.ToString("D"), StringComparison.Ordinal);

            taskId = projects.InSource[0];
            before = await RowAsync(database, owner.SpaceId, taskId);
            refused = await MoveToProjectAsync(owner, owner.SpaceId, taskId, ProjectMoveWire.RawBody(body));
        });

        Assert.NotNull(owner);
        RemovalWire.AssertRefused(refused, HttpStatusCode.BadRequest, problemType);
        Assert.Equal(before, await RowAsync(database, owner.SpaceId, taskId));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// What a Task carries survives the move - its id, Space, title, due date, Assignee, Labels in their order and their sort
    /// key, and its creation time, each seeded non-null - and the move sends no email: the Assignee did not change. Anchored
    /// on a later re-assignment's email arriving: anything the move had enqueued would have been enqueued before it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-41")]
    [Trait("Requirement", "FR-21")]
    [Trait("Requirement", "AD-10")]
    public async Task What_a_Task_carries_survives_the_move_and_no_email_is_sent()
    {
        var database = Available();
        var wire = new WireDatabase(fixture.ConnectionString!);
        Actor? owner = null;
        var taskId = Guid.Empty;
        var destinationId = Guid.Empty;
        TaskSurvivors? before = null;
        TaskSurvivors? after = null;
        IReadOnlyList<Guid> labelsBefore = [];
        IReadOnlyList<Guid> labelsAfter = [];
        IReadOnlyList<Guid> byName = [];
        WireResponse? moved = null;
        IReadOnlyList<DroppedEmail> dropped = [];
        IReadOnlyList<OutboxRow> outbox = [];
        var title = "Carries everything " + Guid.NewGuid().ToString("N")[..8];

        using var mail = new MailDrop();

        _ = await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            owner = await RegisterAsync(client, database);

            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            var membershipId = (await wire.MembershipOfAsync(owner.SpaceId, member.AccountId)).Id;
            var projects = await TwoProjectsAsync(owner, [title], []);
            var bravo = await LabelWire.SeedAsync(owner, owner.SpaceId, "Bravo " + Guid.NewGuid().ToString("N")[..6]);
            var alpha = await LabelWire.SeedAsync(owner, owner.SpaceId, "Alpha " + Guid.NewGuid().ToString("N")[..6]);

            taskId = projects.InSource[0];
            destinationId = projects.DestinationId;
            byName = [alpha, bravo];
            Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, taskId, Body(new { labelIds = new[] { bravo, alpha }, dueDate = "2026-12-01T00:00:00+00:00" }))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await AssignAsync(owner, taskId, membershipId)).StatusCode);
            _ = await mail.WaitForAsync(1);

            before = await SurvivorsAsync(database, owner.SpaceId, taskId);
            labelsBefore = await LabelWire.AppliedToAsync(database, owner.SpaceId, taskId);

            moved = await MoveToProjectAsync(owner, owner.SpaceId, taskId, ProjectMoveWire.Body(projects.DestinationId));

            after = await SurvivorsAsync(database, owner.SpaceId, taskId);
            labelsAfter = await LabelWire.AppliedToAsync(database, owner.SpaceId, taskId);

            Assert.Equal(HttpStatusCode.OK, (await AssignAsync(owner, taskId, membershipId: null)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await AssignAsync(owner, taskId, membershipId)).StatusCode);
            dropped = await mail.WaitForAsync(2);
            outbox = await wire.OutboxRowsAsync();
        });

        Assert.NotNull(owner);
        Assert.NotNull(before);
        AssertMovedInto(moved, destinationId);
        Assert.NotNull(before.DueDate);
        Assert.NotNull(before.AssigneeMembershipId);
        Assert.NotNull(before.FirstLabelName);
        Assert.Equal(2, labelsBefore.Count);
        Assert.Equal(before, after);
        Assert.Equal(labelsBefore, labelsAfter);
        Assert.Equal(before.AssigneeMembershipId, AssignmentWire.AssigneeIn(moved!.Body));
        Assert.Equal(byName, LabelWire.LabelIdsOf(moved.Body));
        Assert.Equal(2, dropped.Count);
        Assert.All(dropped, email => Assert.True(AssignmentWire.IsAssignmentEmail(email), $"Not an assignment email: {email.Subject}"));
        Assert.DoesNotContain(outbox, row => row.Body.Contains(title, StringComparison.Ordinal) && !row.Subject.Contains("assigned you a Task", StringComparison.Ordinal));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Into a destination that already holds 5,000 Tasks the move is refused <c>409 bound-reached</c>, byte for byte as a
    /// creation in that Project is, and nothing is written. The source's bound is never consulted.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "AD-25")]
    [Trait("Requirement", "AR-30")]
    public async Task A_move_into_a_full_Project_is_refused_409_bound_reached_as_a_creation_is()
    {
        var database = Available();
        Actor? owner = null;
        TwoProjects? projects = null;
        TaskRow? before = null;
        WireResponse? refused = null;
        WireResponse? creation = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["One too many"], []);

            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            await SeedTasksAsync(database, owner.SpaceId, projects.DestinationId, defaults[0].Id, BoundRegistry.For(Bound.TasksPerProject).Limit);

            before = await RowAsync(database, owner.SpaceId, projects.InSource[0]);
            creation = await CreateTaskAsync(owner, owner.SpaceId, projects.DestinationId, "Created into a full Project");
            refused = await MoveToProjectAsync(owner, owner.SpaceId, projects.InSource[0], ProjectMoveWire.Body(projects.DestinationId));
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        RemovalWire.AssertRefused(refused, HttpStatusCode.Conflict, ProblemTypes.BoundReached, creation);
        Assert.Equal(before, await RowAsync(database, owner.SpaceId, projects.InSource[0]));
        Assert.Equal(BoundRegistry.For(Bound.TasksPerProject).Limit, await TaskCountAsync(database, owner.SpaceId, projects.DestinationId));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// A Viewer of the Space is refused <c>403 insufficient-role</c> naming <c>manage-tasks</c> on its Session and on its Token,
    /// each recorded once, and nothing moves. Anchor: a Member moves over a Token.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "AD-20")]
    public async Task A_Viewer_is_refused_403_on_both_surfaces_and_a_Member_moves_over_a_Token()
    {
        var database = Available();
        Actor? owner = null;
        Actor? viewer = null;
        TwoProjects? projects = null;
        WireResponse? bySession = null;
        WireResponse? byToken = null;
        WireResponse? anchor = null;
        TaskRow? afterRefusals = null;
        TaskRow? before = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            viewer = await RegisterAsync(client, database);

            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, viewer.AccountId, "Viewer");
            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            projects = await TwoProjectsAsync(owner, ["Shared work"], []);
            before = await RowAsync(database, owner.SpaceId, projects.InSource[0]);

            var viewerToken = await IssueTokenAsync(viewer, owner.SpaceId);
            var memberToken = await IssueTokenAsync(member, owner.SpaceId);

            bySession = await MoveToProjectAsync(viewer, owner.SpaceId, projects.InSource[0], ProjectMoveWire.Body(projects.DestinationId));
            byToken = await MoveToProjectWithTokenAsync(viewer, viewerToken, owner.SpaceId, projects.InSource[0], ProjectMoveWire.Body(projects.DestinationId));
            afterRefusals = await RowAsync(database, owner.SpaceId, projects.InSource[0]);
            anchor = await MoveToProjectWithTokenAsync(member, memberToken, owner.SpaceId, projects.InSource[0], ProjectMoveWire.Body(projects.DestinationId));
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
        AssertMovedInto(anchor, projects.DestinationId);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Another Space in the path is the boundary <c>404</c>, byte for byte as a Space that does not exist, and recorded as a
    /// cross-Space attempt; another Space's Task id under the caller's own Space is the same <c>404</c> as a random Task id,
    /// recorded nowhere; neither Task moves.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-20")]
    [Trait("Requirement", "NFR-1")]
    public async Task Another_Spaces_path_or_Task_answers_the_boundary_404_and_nothing_moves()
    {
        var database = Available();
        Actor? owner = null;
        Actor? other = null;
        TwoProjects? theirs = null;
        TaskRow? theirsBefore = null;
        WireResponse? foreignSpace = null;
        WireResponse? absentSpace = null;
        WireResponse? foreignTask = null;
        WireResponse? randomTask = null;
        IReadOnlyList<string> recordedInOwn = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            other = await RegisterAsync(client, database);

            var mine = await TwoProjectsAsync(owner, ["Mine"], []);

            theirs = await TwoProjectsAsync(other, ["Theirs"], []);
            theirsBefore = await RowAsync(database, other.SpaceId, theirs.InSource[0]);

            foreignSpace = await MoveToProjectAsync(owner, other.SpaceId, theirs.InSource[0], ProjectMoveWire.Body(theirs.DestinationId));
            absentSpace = await MoveToProjectAsync(owner, Guid.NewGuid(), theirs.InSource[0], ProjectMoveWire.Body(theirs.DestinationId));
            foreignTask = await MoveToProjectAsync(owner, owner.SpaceId, theirs.InSource[0], ProjectMoveWire.Body(mine.DestinationId));
            randomTask = await MoveToProjectAsync(owner, owner.SpaceId, Guid.NewGuid(), ProjectMoveWire.Body(mine.DestinationId));
            recordedInOwn = await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId);
        });

        Assert.NotNull(owner);
        Assert.NotNull(other);
        Assert.NotNull(theirs);
        RemovalWire.AssertRefused(foreignSpace, HttpStatusCode.NotFound, ProblemTypes.NotAvailable, absentSpace);
        Assert.Equal(absentSpace!.HeaderNames, foreignSpace!.HeaderNames);
        RemovalWire.AssertRefused(foreignTask, HttpStatusCode.NotFound, ProblemTypes.NotAvailable, randomTask);
        Assert.Equal([CrossSpaceKind], await RefusalKindsAsync(database, owner.AccountId, other.SpaceId));
        Assert.Empty(recordedInOwn);
        Assert.Equal(theirsBefore, await RowAsync(database, other.SpaceId, theirs.InSource[0]));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, other.SpaceId);
    }

    /// <summary>
    /// A replay of the same <c>Idempotency-Key</c> answers the original <c>200</c> byte for byte and moves nothing again: the
    /// Task, moved back to its source by another request in between, stays there.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-18")]
    public async Task A_replayed_key_answers_the_original_move_and_moves_nothing_again()
    {
        var database = Available();
        Actor? owner = null;
        TwoProjects? projects = null;
        WireResponse? first = null;
        WireResponse? back = null;
        WireResponse? replay = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Moved once"], []);

            var key = Guid.NewGuid().ToString("N");

            first = await MoveToProjectAsync(owner, owner.SpaceId, projects.InSource[0], ProjectMoveWire.Body(projects.DestinationId), key);
            back = await MoveToProjectAsync(owner, owner.SpaceId, projects.InSource[0], ProjectMoveWire.Body(projects.SourceId));
            replay = await MoveToProjectAsync(owner, owner.SpaceId, projects.InSource[0], ProjectMoveWire.Body(projects.DestinationId), key);
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        AssertMovedInto(first, projects.DestinationId);
        AssertMovedInto(back, projects.SourceId);
        Assert.Equal(first!.StatusCode, replay?.StatusCode);
        Assert.Equal(first.Body, replay?.Body);
        Assert.Equal(projects.SourceId, (await RowAsync(database, owner.SpaceId, projects.InSource[0]))?.ProjectId);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With a test-only trigger failing the moved Task's row write, the move fails <c>5xx</c> and the Task keeps its Project,
    /// Status and key, and no Task holds a Status outside its Project's set. Anchored on the same request succeeding once the
    /// trigger is dropped.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "AR-24")]
    public async Task A_failure_forced_at_the_row_write_leaves_the_Task_where_it_was()
    {
        var database = Available();
        Actor? owner = null;
        TwoProjects? projects = null;
        TaskRow? before = null;
        TaskRow? afterFailure = null;
        WireResponse? failed = null;
        WireResponse? anchor = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Forced to fail"], []);

            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);
            var body = ProjectMoveWire.Body(projects.DestinationId, defaults[1].Id);

            before = await RowAsync(database, owner.SpaceId, projects.InSource[0]);

            var trigger = await PlantFailingTaskUpdateTriggerAsync(database, projects.InSource[0]);

            try
            {
                failed = await MoveToProjectAsync(owner, owner.SpaceId, projects.InSource[0], body);
                afterFailure = await RowAsync(database, owner.SpaceId, projects.InSource[0]);
            }
            finally
            {
                await RemovalWire.DropTriggerAsync(database, trigger);
            }

            anchor = await MoveToProjectAsync(owner, owner.SpaceId, projects.InSource[0], ProjectMoveWire.Body(projects.DestinationId, defaults[1].Id));
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        Assert.NotNull(failed);
        Assert.True(failed.StatusCode >= HttpStatusCode.InternalServerError, $"{failed.StatusCode} {failed.Body}");
        Assert.Equal(before, afterFailure);
        AssertMovedInto(anchor, projects.DestinationId);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// After a move the Task's own URL answers the same id and Space with its new Project, the source's Board no longer lists
    /// it and the destination's does.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-19")]
    [Trait("Requirement", "FR-41")]
    [Trait("Requirement", "AD-19")]
    public async Task After_a_move_the_Task_reads_the_same_id_and_Space_with_its_new_Project_on_its_new_Board()
    {
        var database = Available();
        Actor? owner = null;
        TwoProjects? projects = null;
        TaskRow? before = null;
        WireResponse? read = null;
        WireResponse? sourceBoard = null;
        WireResponse? destinationBoard = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, ["Re-filed"], []);
            before = await RowAsync(database, owner.SpaceId, projects.InSource[0]);

            AssertMovedInto(await MoveToProjectAsync(owner, owner.SpaceId, projects.InSource[0], ProjectMoveWire.Body(projects.DestinationId)), projects.DestinationId);

            read = await ViewTaskAsync(owner, owner.SpaceId, projects.InSource[0]);
            sourceBoard = await BoardAsync(owner, owner.SpaceId, projects.SourceId);
            destinationBoard = await BoardAsync(owner, owner.SpaceId, projects.DestinationId);
        });

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        Assert.NotNull(before);

        var after = await RowAsync(database, owner.SpaceId, projects.InSource[0]);

        Assert.Equal(before.Id, after?.Id);
        Assert.Equal(before.SpaceId, after?.SpaceId);
        Assert.Equal(projects.DestinationId, after?.ProjectId);
        Assert.True(read?.StatusCode == HttpStatusCode.OK, $"{read?.StatusCode} {read?.Body}");
        Assert.Equal(projects.InSource[0], IdIn(read!.Body));
        Assert.Equal(projects.DestinationId, ProjectMoveWire.ProjectIdIn(read.Body));
        Assert.DoesNotContain(DeleteWire.Columns(sourceBoard!).SelectMany(column => column.TaskIds), id => id == projects.InSource[0]);
        Assert.Contains(DeleteWire.Columns(destinationBoard!).SelectMany(column => column.TaskIds), id => id == projects.InSource[0]);
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// Every Task answer states the Task's Project: a creation, a read, an edit, a move within the Project, an assignment, and
    /// each Task row of the Board.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "AD-19")]
    public async Task Every_Task_answer_states_the_Tasks_Project()
    {
        var database = Available();
        var projectId = Guid.Empty;
        var spaceId = Guid.Empty;
        var answers = new List<(string Name, WireResponse Answer)>();
        WireResponse? board = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var wire = new WireDatabase(fixture.ConnectionString!);
            var self = (await wire.MembershipOfAsync(owner.SpaceId, owner.AccountId)).Id;

            spaceId = owner.SpaceId;
            projectId = await CreateProjectAsync(owner, "States itself");

            var created = await CreateTaskAsync(owner, owner.SpaceId, projectId, "Anywhere");
            var taskId = IdOf(created);
            var defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            answers.Add(("creation", created));
            answers.Add(("read", await ViewTaskAsync(owner, owner.SpaceId, taskId)));
            answers.Add(("edit", await EditTaskAsync(owner, owner.SpaceId, taskId, Body(new { title = "Still here" }))));
            answers.Add(("move", await MoveWire.MoveTaskAsync(owner, owner.SpaceId, taskId, defaults[1].Id, afterTaskId: null)));
            answers.Add(("assignment", await AssignAsync(owner, taskId, self)));
            board = await BoardAsync(owner, owner.SpaceId, projectId);
        });

        Assert.All(answers, entry =>
        {
            Assert.True(entry.Answer.StatusCode < HttpStatusCode.MultipleChoices, $"{entry.Name}: {entry.Answer.StatusCode} {entry.Answer.Body}");
            Assert.True(ProjectMoveWire.ProjectIdIn(entry.Answer.Body) == projectId, $"The {entry.Name} answer does not state the Project: {entry.Answer.Body}");
        });

        using var document = JsonDocument.Parse(board!.Body);

        var rows = document.RootElement.GetProperty("columns").EnumerateArray().SelectMany(column => column.GetProperty("tasks").EnumerateArray()).ToList();

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Equal(projectId, row.GetProperty("projectId").GetGuid()));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, spaceId);
    }

    private static Task<WireResponse> AssignAsync(Actor actor, Guid taskId, Guid? membershipId) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Put, TaskRoutes.AssigneeOf(actor.SpaceId, taskId), AssignmentWire.Body(membershipId), NoHeaders);

    private static Guid IdIn(string taskBody)
    {
        using var document = JsonDocument.Parse(taskBody);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>
    /// Makes the destination's set lack the source Task's Status: the destination removes the first default (its own Tasks
    /// there go to the second), or the source adds a Status and moves its Task into it.
    /// </summary>
    private static async Task GiveAStatusTheDestinationLacksAsync(Actor owner, TwoProjects projects, IReadOnlyList<StatusRow> defaults, string cause)
    {
        if (cause == "removed-default")
        {
            _ = await RemovalWire.RemovedAsync(owner, owner.SpaceId, projects.DestinationId, defaults[0].Id, projects.InDestination.Count > 0 ? defaults[1].Id : null);

            return;
        }

        var added = await StatusWire.AddedAsync(owner, owner.SpaceId, projects.SourceId, "Source only " + Guid.NewGuid().ToString("N")[..6]);

        Assert.Equal(HttpStatusCode.OK, (await MoveWire.MoveTaskAsync(owner, owner.SpaceId, projects.InSource[0], added, afterTaskId: null)).StatusCode);
    }

    private static async Task<Guid> RemovedDefaultAsync(Actor owner, Guid projectId, Guid statusId)
    {
        _ = await RemovalWire.RemovedAsync(owner, owner.SpaceId, projectId, statusId);

        return statusId;
    }

    /// <summary>
    /// Every Task's placement in both Projects except <paramref name="moved"/>'s, ordered by id.
    /// </summary>
    private static async Task<IReadOnlyList<(Guid Id, Guid StatusId, string PositionKey)>> OtherPlacementsAsync(RegistrationDatabase database, Guid spaceId, TwoProjects projects, Guid moved) =>
    [
        .. (await MoveWire.PlacementsAsync(database, spaceId, projects.SourceId))
            .Concat(await MoveWire.PlacementsAsync(database, spaceId, projects.DestinationId))
            .Where(row => row.Id != moved)
            .OrderBy(row => row.Id),
    ];
}
