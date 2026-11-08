using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Lists;
using Yello.Contracts.Memberships;
using Yello.Contracts.Tasks;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// Assigning a Task: the rows for <c>PUT /api/v1/spaces/{spaceId}/tasks/{taskId}/assignee</c> and
/// <c>GET /api/v1/spaces/{spaceId}/assignees</c> (I-1, I-2, I-4, I-12, I-13, I-15, I-17; V-1), each stating both surfaces.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every row is live.</b> Both routes fail the route-coverage gate unless these rows are active.
/// </para>
/// <para>
/// <b>I-15 is held twice:</b> by the composite key and the guarded statement (another Space's real Membership id in the
/// body is the same <c>400</c> as a random id, <c>ISO-ASSIGN-FOREIGN-MEMBERSHIP</c>), and by the candidates read and the List
/// filter offering and matching only this Space's Memberships (<c>ISO-ASSIGNEES-ONLY-THIS-SPACE</c>,
/// <c>ISO-LIST-FOREIGN-ASSIGNEE-FILTER</c>).
/// </para>
/// <para>
/// <b>I-14 is proved in the Slices suite</b> (<c>The_assignment_email_names_this_Space_Project_and_Task_and_nothing_of_another_Space</c>),
/// which reads mail through <c>HostProcess</c> and a <see cref="MailDrop"/> of its own; this suite's world shares one Host and
/// one mail drop across rows, so a row cannot own "the one email to this address".
/// </para>
/// <para>
/// <b>Timing.</b> No row is added to <c>IsolationTimingTests</c>: both <c>400</c>s are one byte-identical body with no
/// existence branch (the <c>EXISTS</c> clause does the same work for a random id and a foreign one, which the FILTER predicate
/// hides), and the in-Space not-found is the floored boundary body (<c>NotAvailableFloor</c>), which
/// <c>ISO-TIMING-IN-SPACE</c> already proves. The dev phase revisits this only if measurement says otherwise.
/// </para>
/// </remarks>
internal static partial class IsolationCases
{
    private static IEnumerable<IsolationCase> AssignmentCases() =>
    [
        new IsolationCase(
            "ISO-ASSIGNEES-FOREIGN-SPACE", "I-1", IsolationVector.DirectIdentifier, HttpMethod.Get, AssigneeRoutes.List,
            world => ForeignAsync(world, AssigneeRoutes.ListOf),
            Both(new BoundaryNotFound())),

        // B's Task, assigned to B's own Owner Membership in the body: a write that leaked would land in B's Space.
        new IsolationCase(
            "ISO-ASSIGN-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Put, TaskRoutes.Assignee,
            async world =>
            {
                var (_, foreign) = await AnyProjectWithTaskAsync(world, world.B);
                _ = await AnyProjectWithTaskAsync(world, world.A);
                var theirOwner = await world.Database.MembershipOfAsync(world.B.SpaceId, world.B.AccountId);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Put,
                    [new(TaskRoutes.AssigneeOf(world.B.SpaceId, foreign), TaskRoutes.AssigneeOf(Guid.NewGuid(), foreign))],
                    world.B.SpaceId,
                    Body: () => AssignmentWire.Body(theirOwner.Id));
            },
            Both(new BoundaryNotFound { Then = NoTaskIsAssignedInEitherSpaceAsync })),

        // I-15, the ISO-APPLY-FOREIGN-LABEL template: B's real Membership id in the body of A's own Task's assignment answers
        // byte for byte what a random id does, and nothing is assigned anywhere.
        new IsolationCase(
            "ISO-ASSIGN-FOREIGN-MEMBERSHIP", "I-2, I-13, I-15", IsolationVector.Write, HttpMethod.Put, TaskRoutes.Assignee,
            async world =>
            {
                var (_, own) = await AnyProjectWithTaskAsync(world, world.A);
                _ = await AnyProjectWithTaskAsync(world, world.B);
                var foreign = await world.Database.MembershipOfAsync(world.B.SpaceId, world.B.AccountId);
                var path = TaskRoutes.AssigneeOf(world.A.SpaceId, own);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Put,
                    [new(path, path)],
                    TargetSpaceId: null,
                    Body: () => AssignmentWire.Body(foreign.Id),
                    AnchorBody: () => AssignmentWire.Body(Guid.NewGuid()));
            },
            Both(new SameAsAnchor(HttpStatusCode.BadRequest, ExpectedType: ProblemTypes.InvalidAssignee) { Then = NoTaskIsAssignedInEitherSpaceAsync })),

        // B's Task id under A's own Space answers as a Task that never existed there: the unrecorded in-Space not-found,
        // byte-identical to the boundary. The body names A's own Owner Membership, so a leak would assign B's Task.
        new IsolationCase(
            "ISO-ASSIGN-FOREIGN-TASK-IN-OWN-SPACE", "I-1, I-4, I-13", IsolationVector.DeepLink, HttpMethod.Put, TaskRoutes.Assignee,
            async world =>
            {
                var (_, foreign) = await AnyProjectWithTaskAsync(world, world.B);
                _ = await AnyProjectWithTaskAsync(world, world.A);
                var own = await world.Database.MembershipOfAsync(world.A.SpaceId, world.A.AccountId);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Put,
                    [new(TaskRoutes.AssigneeOf(world.A.SpaceId, foreign), TaskRoutes.AssigneeOf(world.A.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null,
                    Body: () => AssignmentWire.Body(own.Id));
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = NoTaskIsAssignedInEitherSpaceAsync })),

        // I-15, aggregate: the host's Space holds a Member who also belongs to a second Space, whose Owner and other Member the
        // host shares nothing with. The host's candidates are exactly its own Space's Memberships, by name only.
        new IsolationCase(
            "ISO-ASSIGNEES-ONLY-THIS-SPACE", "I-3, I-11, I-15", IsolationVector.Listing, HttpMethod.Get, AssigneeRoutes.List,
            async world =>
            {
                var host = await world.NewActorAsync("Ah");
                var elsewhere = await world.NewActorAsync("Ae");
                var shared = await JoinedThroughTheProductAsync(world, host, "As", "member");

                // Seeded directly: a second accept by the same address would read the first Invitation's email back.
                await world.Database.AddMembershipAsync(elsewhere.SpaceId, shared.AccountId, "viewer");
                var stranger = await JoinedThroughTheProductAsync(world, elsewhere, "At", "member");

                HoldsIdentity(host);
                HoldsIdentity(elsewhere);

                var path = AssigneeRoutes.ListOf(host.SpaceId);

                return new CaseRequest(host, HttpMethod.Get, [new(path, path)], TargetSpaceId: null, Other: stranger);
            },
            Both(new Served(HttpStatusCode.OK, OffersExactlyTheHostsMembershipsAsync, "200, exactly the Space's Memberships by name; nothing of the shared Member's other Space, no address"))),

        // I-15 on the List, the ISO-LIST-FOREIGN-FILTER-IN-OWN-SPACE template: B's real Membership id in A's own List's
        // assignee filter matches nothing, with bytes identical to a random id's - never a refusal.
        new IsolationCase(
            "ISO-LIST-FOREIGN-ASSIGNEE-FILTER", "I-13, I-15", IsolationVector.DeepLink, HttpMethod.Get, ListRoute,
            async world =>
            {
                var (own, _) = await AnyProjectWithTaskAsync(world, world.A);
                var foreign = await world.Database.MembershipOfAsync(world.B.SpaceId, world.B.AccountId);
                var path = ListOf(world.A.SpaceId, own);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Get,
                    [new($"{path}&{ListRoutes.AssigneeParameter}={foreign.Id:D}", $"{path}&{ListRoutes.AssigneeParameter}={Guid.NewGuid():D}")],
                    TargetSpaceId: null);
            },
            Both(new SameAsAnchor(HttpStatusCode.OK))),

        new IsolationCase(
            "ISO-RECORD-KIND-ASSIGN-VIEWER", "V-1, C-6, NFR-7", IsolationVector.RecordKind, HttpMethod.Put, TaskRoutes.Assignee,
            ViewerAssignsATaskAsync,
            Both(new RoleRefused(AssignmentWire.AssignCapabilityCode) { Then = NoTaskIsAssignedInTheActorsSpaceAsync })),

        // The 3.4 gate: one row per Space-scoped route a Token reaches. By Session the Account unassigns an unassigned Task of
        // its second Space (a no-op 200) and reads that Space's candidates; by the first Space's Token each is a non-member's.
        TokenOwnOtherSpace("ASSIGN", HttpMethod.Put, TaskRoutes.Assignee, HttpStatusCode.OK, sa2 => TaskRoutes.AssigneeOf(sa2.SpaceId, sa2.TaskIds[0]), () => AssignmentWire.Body(null)),

        TokenOwnOtherSpace("ASSIGNEES", HttpMethod.Get, AssigneeRoutes.List, HttpStatusCode.OK, sa2 => AssigneeRoutes.ListOf(sa2.SpaceId)),
    ];

    /// <summary>
    /// A fresh Account with a Project and a Task in its own Personal Space, then demoted to Viewer there, so its Session and
    /// its Token both hold exactly the Viewer Role; the request assigns that Task to the Account's own Membership.
    /// </summary>
    private static async Task<CaseRequest> ViewerAssignsATaskAsync(IsolationWorld world)
    {
        var viewer = await world.NewActorAsync("Va");
        var (_, taskId) = await SeedProjectWithTaskAsync(viewer);
        var own = await world.Database.MembershipOfAsync(viewer.SpaceId, viewer.AccountId);

        await world.Database.ChangeRoleAsync(viewer.SpaceId, viewer.AccountId, "viewer");

        var path = TaskRoutes.AssigneeOf(viewer.SpaceId, taskId);

        return new CaseRequest(viewer, HttpMethod.Put, [new(path, path)], viewer.SpaceId, Body: () => AssignmentWire.Body(own.Id));
    }

    /// <summary>
    /// No Task in A's Space or in B's carries an Assignee. Anchor: both Spaces hold Tasks, so the check reads rows.
    /// </summary>
    private static async Task NoTaskIsAssignedInEitherSpaceAsync(IsolationWorld world, CaseRequest request)
    {
        var own = await world.Database.AssigneesInAsync(world.A.SpaceId);
        var theirs = await world.Database.AssigneesInAsync(world.B.SpaceId);

        Assert.NotEmpty(own);
        Assert.NotEmpty(theirs);
        Assert.All(own.Concat(theirs), row => Assert.Null(row.AssigneeMembershipId));
    }

    /// <summary>
    /// No Task in the refused actor's Space carries an Assignee. Anchor: the Space holds the Task the request named.
    /// </summary>
    private static async Task NoTaskIsAssignedInTheActorsSpaceAsync(IsolationWorld world, CaseRequest request)
    {
        var rows = await world.Database.AssigneesInAsync(request.Actor.SpaceId);

        Assert.NotEmpty(rows);
        Assert.All(rows, row => Assert.Null(row.AssigneeMembershipId));
    }

    /// <summary>
    /// I-15 for the candidates: the host's read offers exactly its Space's Memberships (the host and the shared Member), each
    /// as a Membership id and a display name only, and nothing of the shared Member's other Space - not that Space's Owner's
    /// or other Member's name, address or Membership id. Anchored both ways: both Spaces hold Members.
    /// </summary>
    private static async Task OffersExactlyTheHostsMembershipsAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var candidates = AssignmentWire.ParseCandidates(answer.Body);
        var (_, shapes) = AssignmentWire.MemberNames(answer.Body);
        var own = await world.Database.MembershipRowsInAsync(request.Actor.SpaceId);
        var stranger = request.Other ?? throw new InvalidOperationException("The case names no stranger.");
        var theirs = await world.Database.MembershipRowsInAsync(stranger.SpaceId);

        Assert.Equal(2, own.Count);
        Assert.Equal(3, theirs.Count);
        Assert.Equal(own.Select(row => row.Id), candidates.Select(candidate => candidate.MembershipId));
        Assert.All(shapes, shape => Assert.Equal(AssignmentWire.CandidateMembers, shape));
        Assert.Empty(candidates.Select(candidate => candidate.MembershipId).Intersect(theirs.Where(row => own.All(mine => mine.AccountId != row.AccountId)).Select(row => row.Id)));
        Assert.DoesNotContain("@", answer.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(stranger.DisplayName, answer.Body, StringComparison.Ordinal);
    }
}
