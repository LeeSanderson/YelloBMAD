using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Projects;
using Yello.Contracts.Tasks;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Assigning a Task over the wire: <c>PUT /api/v1/spaces/{spaceId}/tasks/{taskId}/assignee</c> under
/// <c>Capability.AssignTask</c>, body <c>{ membershipId }</c> (<c>null</c> unassigns), answered <c>200</c> with the Task; the
/// address-free candidates read <c>GET /api/v1/spaces/{spaceId}/assignees</c> under <c>ReadProjectsAndTasks</c>; the
/// assignment email through the outbox in the same transaction, never for one's own action; the Space-scoped send record;
/// and the unassignment that removal and leaving perform inside their own transaction.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every refusal is anchored</b> on a <c>200</c> from the same route in the same case first: the route does not exist yet,
/// and an unmapped path answers the boundary <c>404</c>, so a refusal asserted alone would pass against nothing.
/// </para>
/// <para>
/// <b>Seeding.</b> An assigner or Assignee whose Role is the point joins through the product (an Invitation at that Role,
/// accepted). An Assignee whose <b>email</b> is the point is seeded directly (<see cref="WireDatabase.AddMembershipAsync"/>)
/// and registered through the product: its address then holds no Invitation email, so "one assignment email to this
/// address" is not confused with the Invitation. Where a joined Account is also emailed, the assignment email is told apart
/// by its subject (<see cref="AssignmentWire.IsAssignmentEmail"/>). Every Space here is seeded with one Status, because a
/// seeded Space carries none and a Task needs one.
/// </para>
/// <para>
/// <b>Seed constraints checked.</b> <c>FK_Membership_Account_AccountId</c> (Restrict): every Membership names an Account
/// registered through the product first. <c>AK_Membership_SpaceId_AccountId</c>: one Membership per Account per Space.
/// <c>UX_Membership_SpaceId_Owner</c>: one Owner per seeded Space. <c>FK_Task_Project_SpaceId_ProjectId</c> and the Status
/// key: every Task is created through the product in a Project the product created in that Space. The Assignee key
/// <c>(SpaceId, AssigneeMembershipId) → Membership(SpaceId, Id)</c> is the product's to write; no case here writes it by
/// hand except the race holder, which names a Membership of the same Space under that Space's key.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    private const string AssignRouteSkip = "red: AC1, AC2 - the assignment route PUT .../tasks/{taskId}/assignee is not mapped and dbo.[Task] has no AssigneeMembershipId yet (Lee's Q1 (a))";

    private static readonly string[] AssignedTaskProperties =
        ["assigneeMembershipId", "createdAt", "dueDate", "id", "labelIds", "positionKey", "statusId", "title"];

    /// <summary>
    /// An Owner, an Admin and a Member each assign a Task to a Member of the same Space: <c>200</c>, the body is the Task with
    /// <c>assigneeMembershipId</c> appended to its earlier members and nothing else (AD-19), and the row holds the Membership.
    /// </summary>
    /// <param name="assignerRole">The assigner's Role.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = AssignRouteSkip)]
    [InlineData("owner")]
    [InlineData("admin")]
    [InlineData("member")]
    [Trait("Requirement", "FR-21")]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "AD-19")]
    public async Task An_Owner_an_Admin_or_a_Member_assigns_a_Task_to_a_Member_of_the_Space(string assignerRole)
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, assignerAddress, assigneeAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var taskId = Guid.Empty;
        var assigneeMembership = Guid.Empty;
        WireResponse? assigned = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedAssignableSpaceAsync(database, spaceId, "Assigners " + Suffix(), await AccountIdOfAsync(database, ownerAddress));

            var assigner = assignerRole == "owner"
                ? owner
                : await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), assignerAddress, DisplayName(), assignerRole);

            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), assigneeAddress, DisplayName(), "member");
            assigneeMembership = (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, assigneeAddress))).Id;

            (_, taskId) = await TaskThroughTheProductAsync(owner, spaceId, "Assigned " + Suffix());

            await assigner.FetchAntiforgeryTokenAsync();
            assigned = await AssignOverWireAsync(assigner, spaceId, taskId, assigneeMembership);
        });

        Assert.True(assigned?.StatusCode == HttpStatusCode.OK, $"The {assignerRole}'s assignment: {Described(assigned)}");
        Assert.Equal(AssignedTaskProperties, MembershipWire.ObjectMemberNames(assigned.Body));
        Assert.Equal(assigneeMembership, AssignmentWire.AssigneeIn(assigned.Body));
        Assert.Equal(assigneeMembership, await wire.AssigneeOfAsync(spaceId, taskId));
    }

    /// <summary>
    /// A Token assigns with no anti-forgery header (AD-4), and <c>null</c> unassigns: <c>200</c> with
    /// <c>assigneeMembershipId</c> present and <c>null</c>, and the row cleared.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = AssignRouteSkip)]
    [Trait("Requirement", "FR-21")]
    [Trait("Requirement", "AD-4")]
    public async Task A_Token_assigns_without_anti_forgery_and_null_unassigns()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, assigneeAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var taskId = Guid.Empty;
        var assigneeMembership = Guid.Empty;
        Guid? afterAssign = null;
        WireResponse? assigned = null;
        WireResponse? unassigned = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedAssignableSpaceAsync(database, spaceId, "By Token " + Suffix(), await AccountIdOfAsync(database, ownerAddress));
            _ = await SignedInAsync(client, assigneeAddress);
            await wire.AddMembershipAsync(spaceId, await AccountIdOfAsync(database, assigneeAddress), "member");
            assigneeMembership = (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, assigneeAddress))).Id;

            (_, taskId) = await TaskThroughTheProductAsync(owner, spaceId, "Scripted " + Suffix());

            await owner.FetchAntiforgeryTokenAsync();

            var token = await BearerForAsync(owner, spaceId);

            assigned = await owner.SendWithTokenAndHeadersAsync(HttpMethod.Put, AssignmentWire.AssigneeOf(spaceId, taskId), token, AssignmentWire.Body(assigneeMembership), NoKey);
            afterAssign = await wire.AssigneeOfAsync(spaceId, taskId);
            unassigned = await owner.SendWithTokenAndHeadersAsync(HttpMethod.Put, AssignmentWire.AssigneeOf(spaceId, taskId), token, AssignmentWire.Body(null), NoKey);
        });

        Assert.True(assigned?.StatusCode == HttpStatusCode.OK, $"The Token's assignment: {Described(assigned)}");
        Assert.Equal(assigneeMembership, afterAssign);
        Assert.True(unassigned?.StatusCode == HttpStatusCode.OK, $"The Token's unassignment: {Described(unassigned)}");
        Assert.Equal(AssignedTaskProperties, MembershipWire.ObjectMemberNames(unassigned.Body));
        Assert.Null(AssignmentWire.AssigneeIn(unassigned.Body));
        Assert.Null(await wire.AssigneeOfAsync(spaceId, taskId));
    }

    /// <summary>
    /// A body that is not an object, or an object that names no <c>membershipId</c>, is <c>400 invalid-assignee</c> and changes
    /// nothing. Anchor: the same Task was assigned through the same route first, and is still assigned after.
    /// </summary>
    /// <param name="body">The refused body.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = AssignRouteSkip)]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("\"someone\"")]
    [InlineData("null")]
    [Trait("Requirement", "FR-21")]
    public async Task A_body_that_is_not_an_object_or_names_no_Membership_is_400_invalid_assignee(string body)
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, assigneeAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var taskId = Guid.Empty;
        var assigneeMembership = Guid.Empty;
        WireResponse? anchor = null;
        WireResponse? refused = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedAssignableSpaceAsync(database, spaceId, "Bodies " + Suffix(), await AccountIdOfAsync(database, ownerAddress));
            _ = await SignedInAsync(client, assigneeAddress);
            await wire.AddMembershipAsync(spaceId, await AccountIdOfAsync(database, assigneeAddress), "member");
            assigneeMembership = (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, assigneeAddress))).Id;

            (_, taskId) = await TaskThroughTheProductAsync(owner, spaceId, "Bodied " + Suffix());

            await owner.FetchAntiforgeryTokenAsync();
            anchor = await AssignOverWireAsync(owner, spaceId, taskId, assigneeMembership);
            refused = await owner.SendWithHeadersAsync(HttpMethod.Put, AssignmentWire.AssigneeOf(spaceId, taskId), AssignmentWire.RawBody(body), NoKey);
        });

        Assert.True(anchor?.StatusCode == HttpStatusCode.OK, $"The anchor: {Described(anchor)}");
        Assert.True(refused?.StatusCode == HttpStatusCode.BadRequest, $"The body {body}: {Described(refused)}");
        Assert.Equal(AssignmentWire.InvalidAssigneeProblemType, ProblemType(refused));
        Assert.Equal(assigneeMembership, await wire.AssigneeOfAsync(spaceId, taskId));
    }

    /// <summary>
    /// A random Membership id and another Space's real Membership id are each <c>400 invalid-assignee</c>, byte for byte the
    /// same answer, and the Task stays unassigned: the composite key and the guarded statement accept only this Space's
    /// Memberships, and the refusal says nothing about whether the id exists elsewhere (I-13, I-15). Never a <c>404</c>: the
    /// Task exists. Anchor: a real Member of this Space is assigned to another Task on the same route first.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = AssignRouteSkip)]
    [Trait("Requirement", "FR-21")]
    [Trait("Requirement", "AR-8")]
    [Trait("Requirement", "AD-23")]
    public async Task A_random_or_another_Spaces_Membership_id_is_the_same_400_invalid_assignee_and_nothing_is_stored()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, otherOwnerAddress, memberAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
        var (spaceId, otherSpaceId) = (Guid.NewGuid(), Guid.NewGuid());
        var taskId = Guid.Empty;
        WireResponse? anchor = null;
        WireResponse? random = null;
        WireResponse? foreign = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedAssignableSpaceAsync(database, spaceId, "Refusing " + Suffix(), await AccountIdOfAsync(database, ownerAddress));
            _ = await SignedInAsync(client, otherOwnerAddress);
            await SeedAssignableSpaceAsync(database, otherSpaceId, "Elsewhere " + Suffix(), await AccountIdOfAsync(database, otherOwnerAddress));
            _ = await SignedInAsync(client, memberAddress);
            await wire.AddMembershipAsync(spaceId, await AccountIdOfAsync(database, memberAddress), "member");

            var member = (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, memberAddress))).Id;
            var foreignMembership = (await wire.MembershipOfAsync(otherSpaceId, await AccountIdOfAsync(database, otherOwnerAddress))).Id;
            var (_, anchorTask) = await TaskThroughTheProductAsync(owner, spaceId, "Anchored " + Suffix());

            (_, taskId) = await TaskThroughTheProductAsync(owner, spaceId, "Refused " + Suffix());

            await owner.FetchAntiforgeryTokenAsync();
            anchor = await AssignOverWireAsync(owner, spaceId, anchorTask, member);
            random = await AssignOverWireAsync(owner, spaceId, taskId, Guid.NewGuid());
            foreign = await AssignOverWireAsync(owner, spaceId, taskId, foreignMembership);
        });

        Assert.True(anchor?.StatusCode == HttpStatusCode.OK, $"The anchor: {Described(anchor)}");
        Assert.True(random?.StatusCode == HttpStatusCode.BadRequest, $"A random id: {Described(random)}");
        Assert.Equal(AssignmentWire.InvalidAssigneeProblemType, ProblemType(random));
        Assert.True(foreign?.StatusCode == HttpStatusCode.BadRequest, $"Another Space's Membership: {Described(foreign)}");
        Assert.Equal(random.Body, foreign.Body);
        Assert.Null(await wire.AssigneeOfAsync(spaceId, taskId));
        Assert.All(await wire.AssigneesInAsync(otherSpaceId), row => Assert.Null(row.AssigneeMembershipId));
    }

    /// <summary>
    /// Every Role reads the candidates (the picker, the card avatars and the List filter all need them): <c>200</c>, exactly
    /// <c>{ assignees }</c>, each candidate exactly <c>{ displayName, membershipId }</c>, every Membership of the Space once in
    /// join order <c>(CreatedAt, Id)</c>, the Viewer among them (a Viewer may be assigned), and no <c>@</c> anywhere in the
    /// body: no address, no Role, no Account id (AD-23, UX-DR34).
    /// </summary>
    /// <param name="readerRole">The reader's Role.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC1, AC6 - the candidates read GET .../assignees is not mapped yet (Q2 (a))")]
    [InlineData("owner")]
    [InlineData("admin")]
    [InlineData("member")]
    [InlineData("viewer")]
    [Trait("Requirement", "FR-21")]
    [Trait("Requirement", "AD-23")]
    [Trait("Requirement", "UX-DR34")]
    public async Task Every_Role_reads_the_candidates_as_names_and_Membership_ids_only(string readerRole)
    {
        var database = Available();
        var wire = Wire();
        var ownerAddress = RandomAddress();
        var joiners = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["admin"] = RandomAddress(),
            ["member"] = RandomAddress(),
            ["viewer"] = RandomAddress(),
        };
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var spaceId = Guid.NewGuid();
        WireResponse? read = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var ownerName = DisplayName();
            var owner = await RegisteredAndSignedInAsync(client, ownerAddress, ownerName);
            await SeedAssignableSpaceAsync(database, spaceId, "Candidates " + Suffix(), await AccountIdOfAsync(database, ownerAddress));
            names[ownerAddress] = ownerName;

            var sessions = new Dictionary<string, WireSession>(StringComparer.Ordinal) { ["owner"] = owner };

            foreach (var (role, address) in joiners)
            {
                names[address] = DisplayName();
                sessions[role] = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), address, names[address], role);
            }

            read = await CandidatesOverWireAsync(sessions[readerRole], spaceId);
        });

        Assert.True(read?.StatusCode == HttpStatusCode.OK, $"The {readerRole}'s read: {Described(read)}");

        var (list, rows) = AssignmentWire.MemberNames(read.Body);
        var candidates = AssignmentWire.ParseCandidates(read.Body);
        var memberships = await wire.MembershipRowsInAsync(spaceId);

        Assert.Equal(AssignmentWire.ListMembers, list);
        Assert.All(rows, row => Assert.Equal(AssignmentWire.CandidateMembers, row));
        Assert.Equal(memberships.Select(row => row.Id), candidates.Select(candidate => candidate.MembershipId));
        Assert.Equal(names.Values.Order(StringComparer.Ordinal), candidates.Select(candidate => candidate.DisplayName).Order(StringComparer.Ordinal));
        Assert.DoesNotContain("@", read.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// A Viewer may be assigned, by an Owner, an Admin or a Member: <c>200</c>. The assignment grants the Viewer nothing: its
    /// edit of that Task is <c>403</c> naming <c>manage-tasks</c>, its own assignment of it is <c>403</c> naming
    /// <c>assign-task</c> (the matrix's Viewer cell), each recorded <c>InsufficientRole</c>, and the Task keeps its title and its
    /// Assignee.
    /// </summary>
    /// <param name="assignerRole">The assigner's Role.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC3 - the assignment route is not mapped yet, so no Viewer can be assigned")]
    [InlineData("owner")]
    [InlineData("admin")]
    [InlineData("member")]
    [Trait("Requirement", "FR-21")]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "AD-20")]
    public async Task A_Viewer_may_be_assigned_and_gains_no_write_over_the_Task(string assignerRole)
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, assignerAddress, viewerAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var taskId = Guid.Empty;
        var viewerId = Guid.Empty;
        var viewerMembership = Guid.Empty;
        var title = "Watched " + Suffix();
        var refusalsBefore = 0;
        WireResponse? assigned = null;
        WireResponse? edit = null;
        WireResponse? selfAssign = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedAssignableSpaceAsync(database, spaceId, "Viewer assigned " + Suffix(), await AccountIdOfAsync(database, ownerAddress));

            var assigner = assignerRole == "owner"
                ? owner
                : await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), assignerAddress, DisplayName(), assignerRole);
            var viewer = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), viewerAddress, DisplayName(), "viewer");

            viewerId = await AccountIdOfAsync(database, viewerAddress);
            viewerMembership = (await wire.MembershipOfAsync(spaceId, viewerId)).Id;
            (_, taskId) = await TaskThroughTheProductAsync(owner, spaceId, title);

            await assigner.FetchAntiforgeryTokenAsync();
            assigned = await AssignOverWireAsync(assigner, spaceId, taskId, viewerMembership);

            refusalsBefore = (await RefusalsOfAsync(database, viewerId)).Count;

            await viewer.FetchAntiforgeryTokenAsync();
            edit = await viewer.SendWithHeadersAsync(HttpMethod.Patch, TaskRoutes.TaskOf(spaceId, taskId), JsonContent.Create(new { title = "Taken over" }, options: JsonOptions), NoKey);
            selfAssign = await AssignOverWireAsync(viewer, spaceId, taskId, null);
        });

        Assert.True(assigned?.StatusCode == HttpStatusCode.OK, $"The {assignerRole} assigning a Viewer: {Described(assigned)}");
        Assert.True(edit?.StatusCode == HttpStatusCode.Forbidden, $"The assigned Viewer's edit: {Described(edit)}");
        Assert.Equal(ProblemTypes.InsufficientRole, ProblemType(edit));
        Assert.Equal("manage-tasks", CapabilityOf(edit));
        Assert.True(selfAssign?.StatusCode == HttpStatusCode.Forbidden, $"The assigned Viewer's own assignment: {Described(selfAssign)}");
        Assert.Equal(ProblemTypes.InsufficientRole, ProblemType(selfAssign));
        Assert.Equal(AssignmentWire.AssignCapabilityCode, CapabilityOf(selfAssign));

        var refusals = (await RefusalsOfAsync(database, viewerId)).Skip(refusalsBefore).ToList();

        Assert.Equal(2, refusals.Count);
        Assert.All(refusals, refusal => Assert.Equal("InsufficientRole", refusal.Kind));
        Assert.Contains(refusals, refusal => refusal.Capability == nameof(Capability.AssignTask));
        Assert.Equal(viewerMembership, await wire.AssigneeOfAsync(spaceId, taskId));
        Assert.Contains(await wire.TasksInAsync(spaceId), row => row.Id == taskId && row.Title == title);
    }

    /// <summary>
    /// Demoting an Assignee to Viewer through the Role change does not unassign it: responsibility and capability are
    /// separable. Every one of its Tasks keeps its <c>AssigneeMembershipId</c> after the <c>200</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 - the assignment route is not mapped yet, so there is no Assignee to demote")]
    [Trait("Requirement", "FR-21")]
    [Trait("Requirement", "FR-13")]
    public async Task Demoting_an_Assignee_to_Viewer_keeps_every_assignment()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var memberMembership = Guid.Empty;
        Guid[] tasks = [];
        WireResponse[] assigned = [];
        WireResponse? demoted = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedAssignableSpaceAsync(database, spaceId, "Demoted " + Suffix(), await AccountIdOfAsync(database, ownerAddress));
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), memberAddress, DisplayName(), "member");
            memberMembership = (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, memberAddress))).Id;

            tasks =
            [
                (await TaskThroughTheProductAsync(owner, spaceId, "First " + Suffix())).TaskId,
                (await TaskThroughTheProductAsync(owner, spaceId, "Second " + Suffix())).TaskId,
            ];

            await owner.FetchAntiforgeryTokenAsync();
            assigned = [await AssignOverWireAsync(owner, spaceId, tasks[0], memberMembership), await AssignOverWireAsync(owner, spaceId, tasks[1], memberMembership)];
            demoted = await ChangeRoleOverWireAsync(owner, spaceId, memberMembership, "viewer");
        });

        Assert.All(assigned, answer => Assert.True(answer.StatusCode == HttpStatusCode.OK, $"An assignment: {Described(answer)}"));
        Assert.True(demoted?.StatusCode == HttpStatusCode.OK, $"The demotion: {Described(demoted)}");
        Assert.Equal("Viewer", (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, memberAddress))).Role);
        Assert.Equal(memberMembership, await wire.AssigneeOfAsync(spaceId, tasks[0]));
        Assert.Equal(memberMembership, await wire.AssigneeOfAsync(spaceId, tasks[1]));
    }

    /// <summary>
    /// I-14: the Assignee belongs to two Spaces, each with distinctively named Projects and Tasks. Assigned in this Space, it
    /// receives exactly one assignment email, which names the assigner, this Space, this Project and this Task, and none of
    /// the other Space's names; it carries no address (neither the recipient's nor the assigner's), no password and no Token
    /// (NFR-6), and its link carries no id.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC4 - nothing enqueues an assignment email yet (OutboxKinds.TaskAssigned, AssignmentEmail)")]
    [Trait("Requirement", "FR-40")]
    [Trait("Requirement", "AR-33")]
    [Trait("Requirement", "AD-10")]
    [Trait("Requirement", "NFR-1")]
    [Trait("Requirement", "NFR-6")]
    [Trait("Assumption", "PRD-12-10")]
    public async Task The_assignment_email_names_this_Space_Project_and_Task_and_nothing_of_another_Space()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, otherOwnerAddress, recipientAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
        var (spaceId, otherSpaceId) = (Guid.NewGuid(), Guid.NewGuid());
        var (ownerName, spaceName, projectName, title) = (DisplayName(), "Harbour " + Suffix(), "Quayside " + Suffix(), "Paint the bollards " + Suffix());
        var (otherSpaceName, otherProjectName, otherTitle) = ("Secret works " + Suffix(), "Hidden project " + Suffix(), "Hidden task " + Suffix());
        var taskId = Guid.Empty;
        IReadOnlyList<DroppedEmail> received = [];
        WireResponse? assigned = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await RegisteredAndSignedInAsync(client, ownerAddress, ownerName);
            await SeedAssignableSpaceAsync(database, spaceId, spaceName, await AccountIdOfAsync(database, ownerAddress));
            var otherOwner = await SignedInAsync(client, otherOwnerAddress);
            await SeedAssignableSpaceAsync(database, otherSpaceId, otherSpaceName, await AccountIdOfAsync(database, otherOwnerAddress));

            _ = await SignedInAsync(client, recipientAddress);
            var recipientId = await AccountIdOfAsync(database, recipientAddress);

            await wire.AddMembershipAsync(spaceId, recipientId, "member");
            await wire.AddMembershipAsync(otherSpaceId, recipientId, "member");

            var otherProject = await NamedProjectAsync(otherOwner, otherSpaceId, otherProjectName);
            _ = await TaskInProjectAsync(otherOwner, otherSpaceId, otherProject, otherTitle);

            var project = await NamedProjectAsync(owner, spaceId, projectName);
            taskId = await TaskInProjectAsync(owner, spaceId, project, title);

            await owner.FetchAntiforgeryTokenAsync();
            assigned = await AssignOverWireAsync(owner, spaceId, taskId, (await wire.MembershipOfAsync(spaceId, recipientId)).Id);
            received = await mail.WaitForEmailsToAsync(recipientAddress, AssignmentWire.IsAssignmentEmail);
        });

        Assert.True(assigned?.StatusCode == HttpStatusCode.OK, $"The assignment: {Described(assigned)}");

        var email = Assert.Single(received);
        var content = string.Join('\n', email.Subject, email.PlainText, email.Html);

        Assert.Contains(ownerName, email.Subject, StringComparison.Ordinal);
        Assert.Contains(spaceName, email.Subject, StringComparison.Ordinal);
        Assert.All(new[] { spaceName, projectName, title, ownerName }, name => Assert.Contains(name, email.PlainText, StringComparison.Ordinal));
        Assert.All(new[] { otherSpaceName, otherProjectName, otherTitle }, name => Assert.DoesNotContain(name, content, StringComparison.Ordinal));
        Assert.All(new[] { recipientAddress, ownerAddress, otherOwnerAddress }, address => Assert.DoesNotContain(address, content, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(Password, content, StringComparison.Ordinal);
        Assert.DoesNotContain("yello_pat_", content, StringComparison.Ordinal);
        Assert.All(new[] { spaceId, otherSpaceId, taskId }, id => Assert.DoesNotContain(id.ToString("D"), content, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// An Account is never notified of its own action: a Member assigning itself is answered <c>200</c> and enqueues nothing.
    /// Anchor: the Owner then assigns it a second Task and that email arrives - exactly one assignment email to its address,
    /// naming the second Task and not the first.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC4 - nothing enqueues an assignment email yet, so the anchor never arrives")]
    [Trait("Requirement", "FR-40")]
    [Trait("Assumption", "PRD-12-10")]
    public async Task Assigning_oneself_sends_no_email_and_an_assignment_by_someone_else_does()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var (ownTitle, givenTitle) = ("Took it myself " + Suffix(), "Given to me " + Suffix());
        IReadOnlyList<DroppedEmail> received = [];
        WireResponse? self = null;
        WireResponse? given = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedAssignableSpaceAsync(database, spaceId, "Self " + Suffix(), await AccountIdOfAsync(database, ownerAddress));
            var member = await SignedInAsync(client, memberAddress);
            var memberId = await AccountIdOfAsync(database, memberAddress);

            await wire.AddMembershipAsync(spaceId, memberId, "member");

            var membership = (await wire.MembershipOfAsync(spaceId, memberId)).Id;
            var (_, own) = await TaskThroughTheProductAsync(owner, spaceId, ownTitle);
            var (_, theirs) = await TaskThroughTheProductAsync(owner, spaceId, givenTitle);

            await member.FetchAntiforgeryTokenAsync();
            self = await AssignOverWireAsync(member, spaceId, own, membership);

            await owner.FetchAntiforgeryTokenAsync();
            given = await AssignOverWireAsync(owner, spaceId, theirs, membership);

            _ = await mail.WaitForEmailsToAsync(memberAddress, email => AssignmentWire.IsAssignmentEmail(email) && email.PlainText.Contains(givenTitle, StringComparison.Ordinal));
            received = AssignmentWire.AssignmentEmailsTo(await mail.ReadAllAsync(), memberAddress);
        });

        Assert.True(self?.StatusCode == HttpStatusCode.OK, $"The self-assignment: {Described(self)}");
        Assert.True(given?.StatusCode == HttpStatusCode.OK, $"The Owner's assignment: {Described(given)}");

        var email = Assert.Single(received);

        Assert.Contains(givenTitle, email.PlainText, StringComparison.Ordinal);
        Assert.DoesNotContain(ownTitle, email.PlainText, StringComparison.Ordinal);
    }

    /// <summary>
    /// Only a change to another Account's Membership is announced: assigning A emails A; assigning A again (a
    /// no-op, still <c>200</c>) emails nobody; reassigning to B emails B only, never A; unassigning emails nobody. Anchor:
    /// assigning A a second Task then emails A. A ends with exactly two assignment emails (one per Task) and B with one.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC4 - nothing enqueues an assignment email yet")]
    [Trait("Requirement", "FR-40")]
    [Trait("Assumption", "PRD-12-10")]
    public async Task Only_a_change_to_another_Accounts_Membership_sends_an_email()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, firstAddress, secondAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var (reassignedTitle, anchorTitle) = ("Passed along " + Suffix(), "Second for A " + Suffix());
        var answers = new List<WireResponse>();
        IReadOnlyList<DroppedEmail> toFirst = [];
        IReadOnlyList<DroppedEmail> toSecond = [];

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedAssignableSpaceAsync(database, spaceId, "Changes " + Suffix(), await AccountIdOfAsync(database, ownerAddress));

            var memberships = new List<Guid>();

            foreach (var address in new[] { firstAddress, secondAddress })
            {
                _ = await SignedInAsync(client, address);
                var accountId = await AccountIdOfAsync(database, address);

                await wire.AddMembershipAsync(spaceId, accountId, "member");
                memberships.Add((await wire.MembershipOfAsync(spaceId, accountId)).Id);
            }

            var (_, reassigned) = await TaskThroughTheProductAsync(owner, spaceId, reassignedTitle);
            var (_, anchor) = await TaskThroughTheProductAsync(owner, spaceId, anchorTitle);

            await owner.FetchAntiforgeryTokenAsync();
            answers.Add(await AssignOverWireAsync(owner, spaceId, reassigned, memberships[0]));
            answers.Add(await AssignOverWireAsync(owner, spaceId, reassigned, memberships[0]));
            answers.Add(await AssignOverWireAsync(owner, spaceId, reassigned, memberships[1]));
            answers.Add(await AssignOverWireAsync(owner, spaceId, reassigned, null));
            answers.Add(await AssignOverWireAsync(owner, spaceId, anchor, memberships[0]));

            _ = await mail.WaitForEmailsToAsync(firstAddress, email => AssignmentWire.IsAssignmentEmail(email) && email.PlainText.Contains(anchorTitle, StringComparison.Ordinal));

            var dropped = await mail.ReadAllAsync();

            toFirst = AssignmentWire.AssignmentEmailsTo(dropped, firstAddress);
            toSecond = AssignmentWire.AssignmentEmailsTo(dropped, secondAddress);
        });

        Assert.All(answers, answer => Assert.True(answer.StatusCode == HttpStatusCode.OK, $"An assignment: {Described(answer)}"));
        Assert.Equal(2, toFirst.Count);
        Assert.Single(toFirst, email => email.PlainText.Contains(reassignedTitle, StringComparison.Ordinal));
        Assert.Single(toFirst, email => email.PlainText.Contains(anchorTitle, StringComparison.Ordinal));
        Assert.Contains(reassignedTitle, Assert.Single(toSecond).PlainText, StringComparison.Ordinal);
    }

    /// <summary>
    /// The enqueue and the send record are in the assignment's own transaction (AD-10): a failure after them (the idempotency
    /// record's insert, forced) leaves the Task unassigned, no outbox row for the recipient, no <c>task-assigned</c> send
    /// record and no email. Anchor: once the plant is gone, another assignment to the same recipient is sent and recorded.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC4, AC5 - the assignment route, its enqueue and the send record do not exist yet")]
    [Trait("Requirement", "AR-33")]
    [Trait("Requirement", "AD-10")]
    [Trait("Requirement", "FS-NFR-2")]
    [Trait("Assumption", "PRD-12-10")]
    public async Task A_failure_after_the_enqueue_rolls_back_the_assignment_its_email_and_its_send_record()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, recipientAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString("D");
        var (rolledBackTitle, anchorTitle) = ("Rolled back " + Suffix(), "Sent after " + Suffix());
        var rolledBackTask = Guid.Empty;
        Guid? assigneeAfterFailure = Guid.Empty;
        var outboxAfterFailure = -1;
        var recordsAfterFailure = -1;
        IReadOnlyList<DroppedEmail> received = [];
        WireResponse? failed = null;
        WireResponse? anchor = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedAssignableSpaceAsync(database, spaceId, "Rollback " + Suffix(), await AccountIdOfAsync(database, ownerAddress));
            _ = await SignedInAsync(client, recipientAddress);
            var recipientId = await AccountIdOfAsync(database, recipientAddress);

            await wire.AddMembershipAsync(spaceId, recipientId, "member");

            var membership = (await wire.MembershipOfAsync(spaceId, recipientId)).Id;
            var (_, anchorTask) = await TaskThroughTheProductAsync(owner, spaceId, anchorTitle);

            (_, rolledBackTask) = await TaskThroughTheProductAsync(owner, spaceId, rolledBackTitle);

            await owner.FetchAntiforgeryTokenAsync();

            var trigger = await PlantFailingIdempotencyTriggerAsync(database, key);

            try
            {
                failed = await AssignOverWireAsync(owner, spaceId, rolledBackTask, membership, key);
            }
            finally
            {
                await DropTriggerAsync(database, trigger);
            }

            assigneeAfterFailure = await wire.AssigneeOfAsync(spaceId, rolledBackTask);
            outboxAfterFailure = (await wire.OutboxRowsAsync()).Count(row => string.Equals(row.Recipient, recipientAddress, StringComparison.OrdinalIgnoreCase));
            recordsAfterFailure = (await wire.NotificationSendsInAsync(spaceId)).Count(record => record.Kind == AssignmentWire.TaskAssignedKind);

            anchor = await AssignOverWireAsync(owner, spaceId, anchorTask, membership);
            received = await mail.WaitForEmailsToAsync(recipientAddress, AssignmentWire.IsAssignmentEmail);
        });

        Assert.True(failed?.StatusCode >= HttpStatusCode.InternalServerError, $"The forced failure: {Described(failed)}");
        Assert.Null(assigneeAfterFailure);
        Assert.Equal(0, outboxAfterFailure);
        Assert.Equal(0, recordsAfterFailure);
        Assert.True(anchor?.StatusCode == HttpStatusCode.OK, $"The anchor: {Described(anchor)}");

        var email = Assert.Single(received);

        Assert.Contains(anchorTitle, email.PlainText, StringComparison.Ordinal);
        Assert.DoesNotContain(rolledBackTitle, email.PlainText, StringComparison.Ordinal);
        Assert.Equal(1, (await wire.NotificationSendsInAsync(spaceId)).Count(record => record.Kind == AssignmentWire.TaskAssignedKind));
    }

    /// <summary>
    /// FS-NFR-2, SM-C4: every notification leaves one send record in its Space - an Invitation issue one
    /// <c>invitation-issued</c>, an assignment one <c>task-assigned</c> - each stamped within the request's window by the
    /// database clock. The records are Space-scoped: under another Space's key none of them is visible.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC5 - dbo.[NotificationSend] does not exist and OutboxStore writes no record yet (Lee's Q4 (a))")]
    [Trait("Requirement", "FS-NFR-2")]
    [Trait("Requirement", "NFR-1")]
    [Trait("Assumption", "PRD-12-10")]
    public async Task An_Invitation_and_an_assignment_each_leave_one_send_record_in_their_Space()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, otherOwnerAddress, recipientAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
        var (spaceId, otherSpaceId) = (Guid.NewGuid(), Guid.NewGuid());
        var before = DateTimeOffset.MinValue;
        var after = DateTimeOffset.MinValue;
        IReadOnlyList<(string Kind, DateTimeOffset RecordedAt)> records = [];
        var visibleElsewhere = -1;
        WireResponse? issued = null;
        WireResponse? assigned = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedAssignableSpaceAsync(database, spaceId, "Recorded " + Suffix(), await AccountIdOfAsync(database, ownerAddress));
            _ = await SignedInAsync(client, otherOwnerAddress);
            await SeedAssignableSpaceAsync(database, otherSpaceId, "Unrecorded " + Suffix(), await AccountIdOfAsync(database, otherOwnerAddress));
            _ = await SignedInAsync(client, recipientAddress);
            var recipientId = await AccountIdOfAsync(database, recipientAddress);

            await wire.AddMembershipAsync(spaceId, recipientId, "member");

            var (_, taskId) = await TaskThroughTheProductAsync(owner, spaceId, "Recorded task " + Suffix());

            await owner.FetchAntiforgeryTokenAsync();
            before = await wire.DatabaseNowAsync();
            issued = await IssueAsync(owner, spaceId, InvitationWire.FreshAddress("recorded"), "member");
            assigned = await AssignOverWireAsync(owner, spaceId, taskId, (await wire.MembershipOfAsync(spaceId, recipientId)).Id);
            after = await wire.DatabaseNowAsync();

            records = await wire.NotificationSendsInAsync(spaceId);
            visibleElsewhere = await wire.NotificationSendsVisibleUnderAsync(otherSpaceId);
        });

        Assert.True(issued?.StatusCode == HttpStatusCode.NoContent, $"The issue: {Described(issued)}");
        Assert.True(assigned?.StatusCode == HttpStatusCode.OK, $"The assignment: {Described(assigned)}");
        Assert.Equal([AssignmentWire.InvitationIssuedKind, AssignmentWire.TaskAssignedKind], records.Select(record => record.Kind).Order(StringComparer.Ordinal));
        Assert.All(records, record => Assert.InRange(record.RecordedAt, before, after));
        Assert.All(records, record => Assert.Equal(TimeSpan.Zero, record.RecordedAt.Offset));
        Assert.Equal(0, visibleElsewhere);
    }

    /// <summary>
    /// AD-18: the same <c>Idempotency-Key</c> after a committed assignment answers the original <c>200</c> byte for byte, sends
    /// no second email and writes no second send record; the key on another body is <c>422 idempotency-key-reused</c>; a
    /// malformed key is <c>400 invalid-idempotency-key</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = AssignRouteSkip)]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "FR-40")]
    [Trait("Assumption", "PRD-12-10")]
    public async Task A_replayed_assignment_answers_its_original_200_and_sends_and_records_nothing_more()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, recipientAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString("D");
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);
        IReadOnlyList<DroppedEmail> received = [];

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedAssignableSpaceAsync(database, spaceId, "Replayed " + Suffix(), await AccountIdOfAsync(database, ownerAddress));
            _ = await SignedInAsync(client, recipientAddress);
            var recipientId = await AccountIdOfAsync(database, recipientAddress);

            await wire.AddMembershipAsync(spaceId, recipientId, "member");

            var membership = (await wire.MembershipOfAsync(spaceId, recipientId)).Id;
            var (_, taskId) = await TaskThroughTheProductAsync(owner, spaceId, "Replayed task " + Suffix());

            await owner.FetchAntiforgeryTokenAsync();
            answers["original"] = await AssignOverWireAsync(owner, spaceId, taskId, membership, key);
            _ = await mail.WaitForEmailsToAsync(recipientAddress, AssignmentWire.IsAssignmentEmail);
            answers["replay"] = await AssignOverWireAsync(owner, spaceId, taskId, membership, key);
            answers["reused"] = await AssignOverWireAsync(owner, spaceId, taskId, null, key);
            answers["malformed"] = await AssignOverWireAsync(owner, spaceId, taskId, membership, new string('k', Idempotency.MaxKeyLength + 1));

            // A second email would be sent after the replay's commit; one more assignment to someone else is the fence.
            var (_, fence) = await TaskThroughTheProductAsync(owner, spaceId, "Fence " + Suffix());

            _ = await AssignOverWireAsync(owner, spaceId, fence, membership);
            _ = await mail.WaitForEmailsToAsync(recipientAddress, AssignmentWire.IsAssignmentEmail, count: 2);
            received = AssignmentWire.AssignmentEmailsTo(await mail.ReadAllAsync(), recipientAddress);
        });

        Assert.True(answers["original"].StatusCode == HttpStatusCode.OK, $"The original: {Described(answers["original"])}");
        Assert.True(answers["replay"].StatusCode == HttpStatusCode.OK, $"The replay: {Described(answers["replay"])}");
        Assert.Equal(answers["original"].Body, answers["replay"].Body);
        Assert.True(answers["reused"].StatusCode == HttpStatusCode.UnprocessableContent, $"The reused key: {Described(answers["reused"])}");
        Assert.Equal(ProblemTypes.IdempotencyKeyReused, ProblemType(answers["reused"]));
        Assert.True(answers["malformed"].StatusCode == HttpStatusCode.BadRequest, $"The malformed key: {Described(answers["malformed"])}");
        Assert.Equal(ProblemTypes.InvalidIdempotencyKey, ProblemType(answers["malformed"]));
        Assert.Equal(2, received.Count);
        Assert.Equal(2, (await wire.NotificationSendsInAsync(spaceId)).Count(record => record.Kind == AssignmentWire.TaskAssignedKind));
    }

    /// <summary>
    /// FR-21, AD-5: removing an assigned Member unassigns its Tasks inside the removal's own transaction and deletes none of
    /// them. The removal answers <c>204</c>; the Space's Task count is unchanged; the removed Member's Tasks are unassigned;
    /// another Member's assignment is untouched; the candidates read no longer lists the removed Member; and the
    /// unassignment sends no email.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC8 - the assignment route is not mapped yet, and the removal does not unassign (the DELETE would fail 547)")]
    [Trait("Requirement", "FR-21")]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AD-5")]
    public async Task An_assigned_Member_is_removed_and_its_Tasks_are_unassigned_and_survive()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, removedAddress, keptAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var (removedMembership, keptMembership) = (Guid.Empty, Guid.Empty);
        Guid[] tasks = [];
        var countBefore = 0;
        var emailsBefore = 0;
        var emailsAfter = 0;
        IReadOnlyList<Candidate> candidatesAfter = [];
        WireResponse? removed = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedAssignableSpaceAsync(database, spaceId, "Removed assignee " + Suffix(), await AccountIdOfAsync(database, ownerAddress));
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), removedAddress, DisplayName(), "member");
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), keptAddress, DisplayName(), "member");
            removedMembership = (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, removedAddress))).Id;
            keptMembership = (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, keptAddress))).Id;

            tasks =
            [
                (await TaskThroughTheProductAsync(owner, spaceId, "Theirs one " + Suffix())).TaskId,
                (await TaskThroughTheProductAsync(owner, spaceId, "Theirs two " + Suffix())).TaskId,
                (await TaskThroughTheProductAsync(owner, spaceId, "Kept " + Suffix())).TaskId,
            ];

            await owner.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.OK, (await AssignOverWireAsync(owner, spaceId, tasks[0], removedMembership)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await AssignOverWireAsync(owner, spaceId, tasks[1], removedMembership)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await AssignOverWireAsync(owner, spaceId, tasks[2], keptMembership)).StatusCode);

            emailsBefore = (await mail.WaitForEmailsToAsync(removedAddress, AssignmentWire.IsAssignmentEmail, count: 2)).Count;
            countBefore = (await wire.TasksInAsync(spaceId)).Count;

            removed = await RemoveOverWireAsync(owner, spaceId, removedMembership);

            // The fence: a later email to the kept Member arrives after any the removal could have enqueued.
            var (_, fence) = await TaskThroughTheProductAsync(owner, spaceId, "Fence " + Suffix());

            _ = await AssignOverWireAsync(owner, spaceId, fence, keptMembership);
            _ = await mail.WaitForEmailsToAsync(keptAddress, AssignmentWire.IsAssignmentEmail, count: 2);
            emailsAfter = AssignmentWire.AssignmentEmailsTo(await mail.ReadAllAsync(), removedAddress).Count;
            candidatesAfter = AssignmentWire.ParseCandidates((await CandidatesOverWireAsync(owner, spaceId)).Body);
        });

        Assert.True(removed?.StatusCode == HttpStatusCode.NoContent, $"The removal: {Described(removed)}");
        Assert.Equal(countBefore + 1, (await wire.TasksInAsync(spaceId)).Count);
        Assert.Null(await wire.AssigneeOfAsync(spaceId, tasks[0]));
        Assert.Null(await wire.AssigneeOfAsync(spaceId, tasks[1]));
        Assert.Equal(keptMembership, await wire.AssigneeOfAsync(spaceId, tasks[2]));
        Assert.Equal(emailsBefore, emailsAfter);
        Assert.DoesNotContain(candidatesAfter, candidate => candidate.MembershipId == removedMembership);
        Assert.Contains(candidatesAfter, candidate => candidate.MembershipId == keptMembership);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// FR-21, AD-5: an assigned Member that leaves is unassigned the same way, inside the leave's own transaction: the leave
    /// answers <c>204</c>, its Task survives unassigned, and another Member's assignment is untouched.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC8 - the assignment route is not mapped yet, and the leave does not unassign (the DELETE would fail 547)")]
    [Trait("Requirement", "FR-21")]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AD-5")]
    public async Task An_assigned_Member_leaves_and_its_Tasks_are_unassigned_and_survive()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, leaverAddress, keptAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var keptMembership = Guid.Empty;
        Guid[] tasks = [];
        WireResponse? left = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedAssignableSpaceAsync(database, spaceId, "Left assignee " + Suffix(), await AccountIdOfAsync(database, ownerAddress));
            var leaver = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), leaverAddress, DisplayName(), "member");
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), keptAddress, DisplayName(), "member");

            var leaverMembership = (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, leaverAddress))).Id;

            keptMembership = (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, keptAddress))).Id;
            tasks =
            [
                (await TaskThroughTheProductAsync(owner, spaceId, "Leaver's " + Suffix())).TaskId,
                (await TaskThroughTheProductAsync(owner, spaceId, "Kept " + Suffix())).TaskId,
            ];

            await owner.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.OK, (await AssignOverWireAsync(owner, spaceId, tasks[0], leaverMembership)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await AssignOverWireAsync(owner, spaceId, tasks[1], keptMembership)).StatusCode);

            await leaver.FetchAntiforgeryTokenAsync();
            left = await LeaveOverWireAsync(leaver, spaceId);
        });

        Assert.True(left?.StatusCode == HttpStatusCode.NoContent, $"The leave: {Described(left)}");
        Assert.Contains(await wire.TasksInAsync(spaceId), row => row.Id == tasks[0]);
        Assert.Null(await wire.AssigneeOfAsync(spaceId, tasks[0]));
        Assert.Equal(keptMembership, await wire.AssigneeOfAsync(spaceId, tasks[1]));
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// The unassignment is guarded like the removal itself: a refused removal (an Admin removing another Admin, outside its
    /// authority) is rolled back whole, so the target Admin keeps its Membership and its Task stays assigned.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC8 - the assignment route is not mapped yet, so there is no assignment to keep")]
    [Trait("Requirement", "FR-21")]
    [Trait("Requirement", "FR-14")]
    [Trait("Assumption", "PRD-12-4")]
    public async Task A_refused_removal_leaves_the_targets_Tasks_assigned()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, actorAddress, targetAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var targetMembership = Guid.Empty;
        var taskId = Guid.Empty;
        WireResponse? refused = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedAssignableSpaceAsync(database, spaceId, "Refused removal " + Suffix(), await AccountIdOfAsync(database, ownerAddress));
            var actor = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), actorAddress, DisplayName(), "admin");
            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), targetAddress, DisplayName(), "admin");

            targetMembership = (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, targetAddress))).Id;
            (_, taskId) = await TaskThroughTheProductAsync(owner, spaceId, "Stays assigned " + Suffix());

            await owner.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.OK, (await AssignOverWireAsync(owner, spaceId, taskId, targetMembership)).StatusCode);

            await actor.FetchAntiforgeryTokenAsync();
            refused = await RemoveOverWireAsync(actor, spaceId, targetMembership);
        });

        Assert.True(refused?.StatusCode == HttpStatusCode.Forbidden, $"The Admin removing an Admin: {Described(refused)}");
        Assert.Equal("Admin", (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, targetAddress))).Role);
        Assert.Equal(targetMembership, await wire.AssigneeOfAsync(spaceId, taskId));
    }

    /// <summary>
    /// AR-8, AD-5: the Assignee key is NO ACTION, and Space deletion still works in one statement - SQL Server checks a NO
    /// ACTION constraint after every cascade of the statement, so deleting a Space that holds an assigned Task answers
    /// <c>204</c> and leaves no Task and no Membership of it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2 - the assignment route and its composite key do not exist yet")]
    [Trait("Requirement", "AR-8")]
    [Trait("Requirement", "AD-5")]
    public async Task Deleting_a_Space_that_holds_an_assigned_Task_answers_204_and_leaves_no_row()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        WireResponse? assigned = null;
        WireResponse? deleted = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedAssignableSpaceAsync(database, spaceId, "Deleted with assignee " + Suffix(), await AccountIdOfAsync(database, ownerAddress));
            _ = await SignedInAsync(client, memberAddress);
            await wire.AddMembershipAsync(spaceId, await AccountIdOfAsync(database, memberAddress), "member");

            var membership = (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, memberAddress))).Id;
            var (_, taskId) = await TaskThroughTheProductAsync(owner, spaceId, "Goes with the Space " + Suffix());

            await owner.FetchAntiforgeryTokenAsync();
            assigned = await AssignOverWireAsync(owner, spaceId, taskId, membership);
            deleted = await DeleteSpaceOverWireAsync(owner, spaceId, key: null);
        });

        Assert.True(assigned?.StatusCode == HttpStatusCode.OK, $"The assignment: {Described(assigned)}");
        Assert.True(deleted?.StatusCode == HttpStatusCode.NoContent, $"The deletion: {Described(deleted)}");
        Assert.Empty(await wire.TasksInAsync(spaceId));
        Assert.Empty(await wire.MembershipRowsInAsync(spaceId));
    }

    /// <summary>
    /// The assignment as the browser sends it: the Session's cookie and anti-forgery header, <c>{ membershipId }</c>, and an
    /// <c>Idempotency-Key</c> when one is given.
    /// </summary>
    private static Task<WireResponse> AssignOverWireAsync(WireSession session, Guid spaceId, Guid taskId, Guid? membershipId, string? key = null) =>
        session.SendWithHeadersAsync(HttpMethod.Put, AssignmentWire.AssigneeOf(spaceId, taskId), AssignmentWire.Body(membershipId), KeyHeaders(key));

    /// <summary>
    /// The candidates read as the browser sends it: the Session's cookie, a <c>GET</c>.
    /// </summary>
    private static Task<WireResponse> CandidatesOverWireAsync(WireSession session, Guid spaceId) =>
        session.SendWithHeadersAsync(HttpMethod.Get, AssignmentWire.AssigneesOf(spaceId), content: null, NoKey, withAntiforgeryToken: false);

    /// <summary>
    /// A named Space with its Owner and one Status, so a Task can be created in it: a seeded Space carries no Statuses.
    /// </summary>
    private static Task SeedAssignableSpaceAsync(RegistrationDatabase database, Guid spaceId, string name, Guid ownerId) =>
        SpaceSeeding.SeedAsync(database, spaceId, context =>
        {
            context.Spaces.Add(NamedSpace(spaceId, name));
            context.Memberships.Add(SpaceSeeding.NewMembership(spaceId, ownerId, Role.Owner));
            context.StatusDefinitions.Add(SpaceSeeding.NewStatus(spaceId, "To do"));
        }, TestContext.Current.CancellationToken);

    /// <summary>
    /// A Project with a chosen name, created through the product.
    /// </summary>
    private static async Task<Guid> NamedProjectAsync(WireSession session, Guid spaceId, string name)
    {
        await session.FetchAntiforgeryTokenAsync();

        var created = await session.SendWithHeadersAsync(HttpMethod.Post, ProjectRoutes.ListIn(spaceId), JsonContent.Create(new { name }, options: JsonOptions), NoKey);

        Assert.True(created.StatusCode == HttpStatusCode.Created, $"Creating a Project through the product: {Described(created)}");

        using var document = JsonDocument.Parse(created.Body);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>
    /// A Task with a chosen title in a given Project, created through the product.
    /// </summary>
    private static async Task<Guid> TaskInProjectAsync(WireSession session, Guid spaceId, Guid projectId, string title)
    {
        var created = await session.SendWithHeadersAsync(HttpMethod.Post, TaskRoutes.CreateIn(spaceId, projectId), JsonContent.Create(new { title }, options: JsonOptions), NoKey);

        Assert.True(created.StatusCode == HttpStatusCode.Created, $"Creating a Task through the product: {Described(created)}");

        using var document = JsonDocument.Parse(created.Body);

        return document.RootElement.GetProperty("id").GetGuid();
    }

    /// <summary>
    /// A fresh Project with one Task in it, both created through the product.
    /// </summary>
    private static async Task<(Guid ProjectId, Guid TaskId)> TaskThroughTheProductAsync(WireSession session, Guid spaceId, string title)
    {
        var projectId = await NamedProjectAsync(session, spaceId, "Assignable " + Suffix());

        return (projectId, await TaskInProjectAsync(session, spaceId, projectId, title));
    }
}
