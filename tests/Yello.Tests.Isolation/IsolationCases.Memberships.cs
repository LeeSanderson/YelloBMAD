using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Invitations;
using Yello.Contracts.Memberships;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// Story 4.4 - list Memberships and change a Role: the rows for <c>GET /api/v1/spaces/{spaceId}/memberships</c> and
/// <c>PATCH .../memberships/{membershipId}</c> (AC9; I-1, I-2, I-3, I-4, I-11, I-12, I-13, I-17), each stating both surfaces.
/// Written by the story 4.4 ATDD run (2026-11-02) as <c>PendingUntil: "4.4"</c>, and made live by its dev phase (GATE-003).
/// </summary>
/// <remarks>
/// <para>
/// <b>The routes are the contract's <see cref="MembershipRoutes"/></b>, swapped in for the ATDD run's stand-ins. Both routes
/// fail <c>Every_api_route_the_Host_maps_but_the_named_credential_routes_is_named_by_a_case</c> unless these rows are active.
/// </para>
/// <para>
/// <b>Seeding through the product.</b> Lesser Roles and the Admin of the Qualified row join a fresh host's Space through an
/// Invitation at that Role, accepted (4.3; <c>JoinedThroughTheProductAsync</c>), and hold a Token issued there, so
/// both surfaces carry the Role. <c>world.A</c> and <c>world.B</c> are never given new Members: other rows rely on their
/// Spaces as they are.
/// </para>
/// <para>
/// <b>I-17.</b> Every joined Account's address and display name is held as its own secret, and every invited address as its
/// issuer's, so the row log check fails on any address or name in the Host log (AD-23).
/// </para>
/// <para>
/// <b>Timing.</b> No row is added to <c>IsolationTimingTests</c>: under Q9 (a) the in-Space not-found is the boundary
/// <c>404</c> held by the same <c>NotAvailableFloor</c>, and <c>ISO-TIMING-IN-SPACE</c> already proves the floor. The dev
/// phase revisits this only if its in-Space not-found does work the boundary does not (AC9's last clause).
/// </para>
/// <para>
/// <b>Removal, leaving and the single-row read</b> (<see cref="RemovalCases"/>) follow the same row kinds, and need no
/// timing row for the same reason: the in-Space not-found is the floored boundary body.
/// </para>
/// </remarks>
internal static partial class IsolationCases
{

    private static IEnumerable<IsolationCase> MembershipCases() =>
    [
        new IsolationCase(
            "ISO-LIST-MEMBERSHIPS-FOREIGN-SPACE", "I-1 (4.4 AC9)", IsolationVector.DirectIdentifier, HttpMethod.Get, MembershipRoutes.List,
            world => ForeignAsync(world, MembershipRoutes.MembershipsOf),
            Both(new BoundaryNotFound())),

        new IsolationCase(
            "ISO-CHANGE-ROLE-FOREIGN-SPACE", "I-2 (4.4 AC9)", IsolationVector.Write, HttpMethod.Patch, MembershipRoutes.Membership,
            async world =>
            {
                var foreign = await world.Database.MembershipOfAsync(world.B.SpaceId, world.B.AccountId);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Patch,
                    [new(MembershipRoutes.MembershipOf(world.B.SpaceId, foreign.Id), MembershipRoutes.MembershipOf(Guid.NewGuid(), foreign.Id))],
                    world.B.SpaceId,
                    Body: () => MembershipWire.ChangeBody("viewer"));
            },
            Both(new BoundaryNotFound { Then = BStillOwnsItsSpaceAsync })),

        new IsolationCase(
            "ISO-LIST-MEMBERSHIPS-OWN-SPACE", "I-3, I-11 (4.4 AC9)", IsolationVector.Listing, HttpMethod.Get, MembershipRoutes.List,
            async world =>
            {
                // The lister's Space holds a Member who also belongs to a second Space, whose other Member and Owner the
                // lister shares nothing with: their names and addresses must not reach the list.
                var host = await world.NewActorAsync("Lo");
                var elsewhere = await world.NewActorAsync("Le");
                var shared = await JoinedThroughTheProductAsync(world, host, "Ls", "member");

                // Seeded directly: a second accept by the same address would read the first Invitation's email back from the
                // world's mail drop (dev-phase correction of the ATDD scaffold, recorded in the story's Debug Log).
                await world.Database.AddMembershipAsync(elsewhere.SpaceId, shared.AccountId, "viewer");
                var stranger = await JoinedThroughTheProductAsync(world, elsewhere, "Lt", "member");

                HoldsIdentity(host);
                HoldsIdentity(elsewhere);

                var path = MembershipRoutes.MembershipsOf(host.SpaceId);

                return new CaseRequest(host, HttpMethod.Get, [new(path, path)], TargetSpaceId: null, Other: stranger);
            },
            Both(new Served(HttpStatusCode.OK, ListsExactlyTheHostsMembershipsAsync, "200, exactly the Space's Memberships; nothing of the shared Member's other Space"))),

        new IsolationCase(
            "ISO-RECORD-KIND-LIST-MEMBERSHIPS-MEMBER", "V-1, C-6, NFR-7 (4.4 AC1, AC9; Lee's Q4 (a))", IsolationVector.RecordKind, HttpMethod.Get, MembershipRoutes.List,
            world => LesserRoleTouchesTheMembershipsAsync(world, "member", change: false),
            Both(new RoleRefused(MembershipWire.CapabilityCode) { Then = TheOtherKeepsItsRole("Owner") })),

        new IsolationCase(
            "ISO-RECORD-KIND-LIST-MEMBERSHIPS-VIEWER", "V-1, C-6, NFR-7 (4.4 AC1, AC9; Lee's Q4 (a))", IsolationVector.RecordKind, HttpMethod.Get, MembershipRoutes.List,
            world => LesserRoleTouchesTheMembershipsAsync(world, "viewer", change: false),
            Both(new RoleRefused(MembershipWire.CapabilityCode) { Then = TheOtherKeepsItsRole("Owner") })),

        new IsolationCase(
            "ISO-RECORD-KIND-CHANGE-ROLE-MEMBER", "V-1, C-6, NFR-7 (4.4 AC4, AC9)", IsolationVector.RecordKind, HttpMethod.Patch, MembershipRoutes.Membership,
            world => LesserRoleTouchesTheMembershipsAsync(world, "member", change: true),
            Both(new RoleRefused(MembershipWire.CapabilityCode) { Then = TheOtherKeepsItsRole("Owner") })),

        new IsolationCase(
            "ISO-RECORD-KIND-CHANGE-ROLE-VIEWER", "V-1, C-6, NFR-7 (4.4 AC4, AC9)", IsolationVector.RecordKind, HttpMethod.Patch, MembershipRoutes.Membership,
            world => LesserRoleTouchesTheMembershipsAsync(world, "viewer", change: true),
            Both(new RoleRefused(MembershipWire.CapabilityCode) { Then = TheOtherKeepsItsRole("Owner") })),

        // The Qualified seam's row (Lee's Q1 (a)): the pipeline admits the Admin (Qualified), the store's guarded statement
        // finds Member -> Admin outside the Admin's To set, and the pipeline - not the slice - answers and records the 403.
        new IsolationCase(
            "ISO-CHANGE-ROLE-ADMIN-BEYOND-AUTHORITY", "V-1, C-6, NFR-7 (4.4 AC4, AC9; Lee's Q1 (a); ledger :329)", IsolationVector.RecordKind, HttpMethod.Patch, MembershipRoutes.Membership,
            async world =>
            {
                var host = await world.NewActorAsync("Qh");
                var admin = await JoinedThroughTheProductAsync(world, host, "Qa", "admin");
                var member = await JoinedThroughTheProductAsync(world, host, "Qm", "member");
                var target = await world.Database.MembershipOfAsync(host.SpaceId, member.AccountId);
                var path = MembershipRoutes.MembershipOf(host.SpaceId, target.Id);

                return new CaseRequest(admin, HttpMethod.Patch, [new(path, path)], host.SpaceId, Body: () => MembershipWire.ChangeBody("admin"), Other: member);
            },
            Both(new RoleRefused(MembershipWire.CapabilityCode) { Then = TheOtherKeepsItsRole("Member") })) with
        {
            Assumption = "PRD-12-4",
        },

        new IsolationCase(
            "ISO-CHANGE-ROLE-FOREIGN-ID-IN-OWN-SPACE", "I-4, I-13 (4.4 AC4, AC9; Q9 (a))", IsolationVector.DeepLink, HttpMethod.Patch, MembershipRoutes.Membership,
            async world =>
            {
                var foreign = await world.Database.MembershipOfAsync(world.B.SpaceId, world.B.AccountId);

                // The ISO-INVITATION-FOREIGN-ID-IN-OWN-SPACE model: B's Membership id under A's own Space answers as an id
                // that never existed there - the unrecorded in-Space not-found, byte-identical to the boundary.
                return new CaseRequest(
                    world.A,
                    HttpMethod.Patch,
                    [new(MembershipRoutes.MembershipOf(world.A.SpaceId, foreign.Id), MembershipRoutes.MembershipOf(world.A.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null,
                    Body: () => MembershipWire.ChangeBody("viewer"));
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = BStillOwnsItsSpaceAsync })),

        TokenOwnOtherSpace(
            "LIST-MEMBERSHIPS",
            HttpMethod.Get,
            MembershipRoutes.List,
            HttpStatusCode.OK,
            second => MembershipRoutes.MembershipsOf(second.SpaceId))
            with
        {
            TestDesignId = "I-12 (4.4 AC9; the 3.4 AC3 gate; Lee's Q4 (a) Token parity)",
        },

        new IsolationCase(
            "ISO-TOKEN-OWN-OTHER-SPACE-CHANGE-ROLE", "I-12 (4.4 AC9; the 3.4 AC3 gate)", IsolationVector.TokenOwnOtherSpace, HttpMethod.Patch, MembershipRoutes.Membership,
            async world =>
            {
                var (first, second) = await world.OwnerOfTwoAsync("Tm");
                var member = await JoinedThroughTheProductAsync(world, second.Actor, "Tj", "member");
                var target = await world.Database.MembershipOfAsync(second.SpaceId, member.AccountId);
                var path = MembershipRoutes.MembershipOf(second.SpaceId, target.Id);

                return new CaseRequest(first.Actor, HttpMethod.Patch, [new(path, path)], second.SpaceId, Body: () => MembershipWire.ChangeBody("viewer"), Other: member);
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.OK, (_, _, _) => Task.CompletedTask, "200 - a Session carries no Space (FR-2); the Account owns SA2") { Then = TheOtherKeepsItsRole("Viewer") },
                [IsolationSurface.ApiToken] = new SameAsNonMember { Then = TheOtherKeepsItsRole("Member") },
            }),

        .. RemovalCases(),
        .. AssignmentCases(),
        .. ViewerSpaceCases(),
    ];

    private static IEnumerable<IsolationCase> RemovalCases() =>
    [
        new IsolationCase(
            "ISO-REMOVE-MEMBERSHIP-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Delete, MembershipRoutes.Membership,
            async world =>
            {
                var foreign = await world.Database.MembershipOfAsync(world.B.SpaceId, world.B.AccountId);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Delete,
                    [new(MembershipRoutes.MembershipOf(world.B.SpaceId, foreign.Id), MembershipRoutes.MembershipOf(Guid.NewGuid(), foreign.Id))],
                    world.B.SpaceId);
            },
            Both(new BoundaryNotFound { Then = BStillOwnsItsSpaceAsync })),

        new IsolationCase(
            "ISO-LEAVE-FOREIGN-SPACE", "I-2", IsolationVector.Write, HttpMethod.Delete, MembershipRoutes.Own,
            world => ForeignAsync(world, MembershipRoutes.OwnOf, HttpMethod.Delete),
            Both(new BoundaryNotFound { Then = BStillOwnsItsSpaceAsync })),

        new IsolationCase(
            "ISO-READ-MEMBERSHIP-FOREIGN-SPACE", "I-1", IsolationVector.DirectIdentifier, HttpMethod.Get, MembershipRoutes.Membership,
            async world =>
            {
                var foreign = await world.Database.MembershipOfAsync(world.B.SpaceId, world.B.AccountId);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Get,
                    [new(MembershipRoutes.MembershipOf(world.B.SpaceId, foreign.Id), MembershipRoutes.MembershipOf(Guid.NewGuid(), foreign.Id))],
                    world.B.SpaceId);
            },
            Both(new BoundaryNotFound())),

        new IsolationCase(
            "ISO-RECORD-KIND-REMOVE-MEMBERSHIP-MEMBER", "V-1, C-6, NFR-7", IsolationVector.RecordKind, HttpMethod.Delete, MembershipRoutes.Membership,
            world => LesserRoleRemovesTheOwnerAsync(world, "member"),
            Both(new RoleRefused(MembershipWire.RemoveCapabilityCode) { Then = TheOtherKeepsItsRole("Owner") })),

        new IsolationCase(
            "ISO-RECORD-KIND-REMOVE-MEMBERSHIP-VIEWER", "V-1, C-6, NFR-7", IsolationVector.RecordKind, HttpMethod.Delete, MembershipRoutes.Membership,
            world => LesserRoleRemovesTheOwnerAsync(world, "viewer"),
            Both(new RoleRefused(MembershipWire.RemoveCapabilityCode) { Then = TheOtherKeepsItsRole("Owner") })),

        // The Qualified seam's row: the pipeline admits the Admin (Qualified), the guarded DELETE finds another Admin outside
        // the Admin's removable set, and the pipeline - not the slice - answers and records the 403.
        new IsolationCase(
            "ISO-REMOVE-MEMBERSHIP-ADMIN-BEYOND-AUTHORITY", "V-1, C-6, NFR-7", IsolationVector.RecordKind, HttpMethod.Delete, MembershipRoutes.Membership,
            async world =>
            {
                var host = await world.NewActorAsync("Rh");
                var admin = await JoinedThroughTheProductAsync(world, host, "Ra", "admin");
                var other = await JoinedThroughTheProductAsync(world, host, "Rb", "admin");
                var target = await world.Database.MembershipOfAsync(host.SpaceId, other.AccountId);
                var path = MembershipRoutes.MembershipOf(host.SpaceId, target.Id);

                return new CaseRequest(admin, HttpMethod.Delete, [new(path, path)], host.SpaceId, Other: other);
            },
            Both(new RoleRefused(MembershipWire.RemoveCapabilityCode) { Then = TheOtherKeepsItsRole("Admin") })) with
        {
            Assumption = "PRD-12-4",
        },

        new IsolationCase(
            "ISO-REMOVE-MEMBERSHIP-FOREIGN-ID-IN-OWN-SPACE", "I-4, I-13", IsolationVector.DeepLink, HttpMethod.Delete, MembershipRoutes.Membership,
            async world =>
            {
                var foreign = await world.Database.MembershipOfAsync(world.B.SpaceId, world.B.AccountId);

                // B's Membership id under A's own Space answers as an id that never existed there: the unrecorded in-Space
                // not-found, byte-identical to the boundary.
                return new CaseRequest(
                    world.A,
                    HttpMethod.Delete,
                    [new(MembershipRoutes.MembershipOf(world.A.SpaceId, foreign.Id), MembershipRoutes.MembershipOf(world.A.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable) { Then = BStillOwnsItsSpaceAsync })),

        new IsolationCase(
            "ISO-READ-MEMBERSHIP-FOREIGN-ID-IN-OWN-SPACE", "I-4, I-13", IsolationVector.DeepLink, HttpMethod.Get, MembershipRoutes.Membership,
            async world =>
            {
                var foreign = await world.Database.MembershipOfAsync(world.B.SpaceId, world.B.AccountId);

                return new CaseRequest(
                    world.A,
                    HttpMethod.Get,
                    [new(MembershipRoutes.MembershipOf(world.A.SpaceId, foreign.Id), MembershipRoutes.MembershipOf(world.A.SpaceId, Guid.NewGuid()))],
                    TargetSpaceId: null);
            },
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable))),

        // The Account owns both Spaces, and its Token is bound to the first. By Session the Owner removes a Member of the
        // second, and by the first Space's Token the same request is a non-member's.
        new IsolationCase(
            "ISO-TOKEN-OWN-OTHER-SPACE-REMOVE-MEMBERSHIP", "I-12", IsolationVector.TokenOwnOtherSpace, HttpMethod.Delete, MembershipRoutes.Membership,
            async world =>
            {
                var (first, second) = await world.OwnerOfTwoAsync("Tr");
                var member = await JoinedThroughTheProductAsync(world, second.Actor, "Tk", "member");
                var target = await world.Database.MembershipOfAsync(second.SpaceId, member.AccountId);
                var path = MembershipRoutes.MembershipOf(second.SpaceId, target.Id);

                return new CaseRequest(first.Actor, HttpMethod.Delete, [new(path, path)], second.SpaceId, Other: member);
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.NoContent, (_, _, _) => Task.CompletedTask, "204 - a Session carries no Space (FR-2); the Account owns SA2") { Then = TheOtherHoldsNoMembershipAsync },
                [IsolationSurface.ApiToken] = new SameAsNonMember { Then = TheOtherKeepsItsRole("Member") },
            }),

        // The leaver is a Member of the host's Space, and its own Token is bound to its Personal Space. By Session it leaves,
        // and by that Token the same request is a non-member's, so it stays.
        new IsolationCase(
            "ISO-TOKEN-OWN-OTHER-SPACE-LEAVE", "I-12", IsolationVector.TokenOwnOtherSpace, HttpMethod.Delete, MembershipRoutes.Own,
            async world =>
            {
                var host = await world.NewActorAsync("Tv");
                var leaver = await world.NewActorAsync("Tw");
                var joined = await JoinedThroughTheProductAsync(world, host, leaver, "member");
                var path = MembershipRoutes.OwnOf(host.SpaceId);

                return new CaseRequest(leaver, HttpMethod.Delete, [new(path, path)], host.SpaceId, Other: joined);
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.NoContent, (_, _, _) => Task.CompletedTask, "204 - a Session carries no Space (FR-2); the Account is a Member there") { Then = TheOtherHoldsNoMembershipAsync },
                [IsolationSurface.ApiToken] = new SameAsNonMember { Then = TheOtherKeepsItsRole("Member") },
            }),

        new IsolationCase(
            "ISO-TOKEN-OWN-OTHER-SPACE-READ-MEMBERSHIP", "I-12", IsolationVector.TokenOwnOtherSpace, HttpMethod.Get, MembershipRoutes.Membership,
            async world =>
            {
                var (first, second) = await world.OwnerOfTwoAsync("Tq");
                var own = await world.Database.MembershipOfAsync(second.SpaceId, second.Actor.AccountId);
                var path = MembershipRoutes.MembershipOf(second.SpaceId, own.Id);

                return new CaseRequest(first.Actor, HttpMethod.Get, [new(path, path)], second.SpaceId, Other: second.Actor);
            },
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.OK, (_, _, _) => Task.CompletedTask, "200 - a Session carries no Space (FR-2); the Account owns SA2"),
                [IsolationSurface.ApiToken] = new SameAsNonMember(),
            }),
    ];

    /// <summary>
    /// A fresh host's Space in which a fresh Account holds <paramref name="roleStableCode"/>, joined through the product,
    /// and tries to remove the host's own Owner row. The pipeline refuses on the capability before any slice runs.
    /// <c>Other</c> is the host.
    /// </summary>
    private static async Task<CaseRequest> LesserRoleRemovesTheOwnerAsync(IsolationWorld world, string roleStableCode)
    {
        var member = string.Equals(roleStableCode, "member", StringComparison.Ordinal);
        var host = await world.NewActorAsync(member ? "Xh" : "Yh");
        var actor = await JoinedThroughTheProductAsync(world, host, member ? "Xm" : "Ym", roleStableCode);
        var owner = await world.Database.MembershipOfAsync(host.SpaceId, host.AccountId);
        var path = MembershipRoutes.MembershipOf(host.SpaceId, owner.Id);

        return new CaseRequest(actor, HttpMethod.Delete, [new(path, path)], host.SpaceId, Other: host);
    }

    /// <summary>
    /// The request's <c>Other</c> holds no Membership in the Space it is seen from: the removal or the leave landed.
    /// </summary>
    private static async Task TheOtherHoldsNoMembershipAsync(IsolationWorld world, CaseRequest request)
    {
        var other = request.Other ?? throw new InvalidOperationException("The case names no Other whose Membership it checks.");

        Assert.DoesNotContain(await world.Database.MembershipRowsInAsync(other.SpaceId), row => row.AccountId == other.AccountId);
    }

    /// <summary>
    /// A fresh Account (its address and display name held as its own secrets) joins <paramref name="host"/>'s Space at
    /// <paramref name="role"/> through the product: <paramref name="host"/> issues an Invitation through 4.1's route (the
    /// address held as the issuer's secret, the token read from the world's mail drop), the Account accepts it through 4.3's
    /// route, and issues itself a Token bound there. Story 4.4 (ATDD run).
    /// </summary>
    /// <returns>The joined Account seen from the host's Space: its Session, and a Token bound there.</returns>
    private static async Task<ProductActor> JoinedThroughTheProductAsync(IsolationWorld world, ProductActor host, string label, string role) =>
        await JoinedThroughTheProductAsync(world, host, await world.NewActorAsync(label), role);

    /// <summary>
    /// <paramref name="joiner"/>, an existing Account, joins <paramref name="host"/>'s Space as above.
    /// </summary>
    private static async Task<ProductActor> JoinedThroughTheProductAsync(IsolationWorld world, ProductActor host, ProductActor joiner, string role)
    {
        HoldsIdentity(joiner);

        var (_, _, token) = await InviteWithTokenAsync(world, host, joiner.EmailAddress, role);
        var accepted = await joiner.SendAsync(IsolationSurface.Session, HttpMethod.Post, InvitationRoutes.Acceptance, InvitationWire.AcceptBody(token), headers: null);

        Assert.True(accepted.StatusCode == HttpStatusCode.OK, $"Accepting through the product: {accepted.StatusCode} {accepted.Body}");

        return joiner.InSpace(host.SpaceId, host.SpaceName, await joiner.IssueTokenAsync(host.SpaceId, "joined-" + role));
    }

    /// <summary>
    /// Holds an Account's own address and display name as its secrets, so the row log check looks for both (AD-23, I-17).
    /// </summary>
    private static void HoldsIdentity(ProductActor actor)
    {
        actor.Origin.HoldsSecret(actor.EmailAddress);
        actor.Origin.HoldsSecret(actor.DisplayName);
    }

    /// <summary>
    /// A fresh host's Space in which a fresh Account holds <paramref name="roleStableCode"/>, joined through the product;
    /// the request lists the Space's Memberships or changes the host's own row to Viewer. <c>Other</c> is the host, whose
    /// Role the refusal must leave as it was.
    /// </summary>
    private static async Task<CaseRequest> LesserRoleTouchesTheMembershipsAsync(IsolationWorld world, string roleStableCode, bool change)
    {
        var host = await world.NewActorAsync(string.Equals(roleStableCode, "member", StringComparison.Ordinal) ? "Mh" : "Vh");
        var actor = await JoinedThroughTheProductAsync(world, host, string.Equals(roleStableCode, "member", StringComparison.Ordinal) ? "Mm" : "Vm", roleStableCode);

        if (!change)
        {
            var list = MembershipRoutes.MembershipsOf(host.SpaceId);

            return new CaseRequest(actor, HttpMethod.Get, [new(list, list)], host.SpaceId, Other: host);
        }

        var owner = await world.Database.MembershipOfAsync(host.SpaceId, host.AccountId);
        var path = MembershipRoutes.MembershipOf(host.SpaceId, owner.Id);

        return new CaseRequest(actor, HttpMethod.Patch, [new(path, path)], host.SpaceId, Body: () => MembershipWire.ChangeBody("viewer"), Other: host);
    }

    /// <summary>
    /// The request's <c>Other</c> still holds <paramref name="stored"/> in the Space it is seen from.
    /// </summary>
    private static Func<IsolationWorld, CaseRequest, Task> TheOtherKeepsItsRole(string stored) =>
        async (world, request) =>
        {
            var other = request.Other ?? throw new InvalidOperationException("The case names no Other whose Role it checks.");

            Assert.Equal(stored, (await world.Database.MembershipOfAsync(other.SpaceId, other.AccountId)).Role);
        };

    /// <summary>
    /// B still holds its own Space's Owner Membership: nothing the case sent changed it.
    /// </summary>
    private static async Task BStillOwnsItsSpaceAsync(IsolationWorld world, CaseRequest request) =>
        Assert.Equal("Owner", (await world.Database.MembershipOfAsync(world.B.SpaceId, world.B.AccountId)).Role);

    /// <summary>
    /// I-3, I-11 for Memberships: the host's list holds exactly its Space's Memberships (the host and the shared Member), and
    /// nothing of the shared Member's other Space - not that Space's Owner's or other Member's name, address or Membership id.
    /// Anchored both ways: both Spaces hold Members.
    /// </summary>
    private static async Task ListsExactlyTheHostsMembershipsAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var page = MembershipWire.ParsePage(answer.Body);
        var own = await world.Database.MembershipRowsInAsync(request.Actor.SpaceId);
        var stranger = request.Other ?? throw new InvalidOperationException("The case names no stranger.");
        var theirs = await world.Database.MembershipRowsInAsync(stranger.SpaceId);

        Assert.Equal(2, own.Count);
        Assert.Equal(3, theirs.Count);
        Assert.Equal(own.Count, page.Total);
        Assert.Equal(own.Select(row => row.Id).Order(), page.Memberships.Select(row => row.Id).Order());
        Assert.Empty(page.Memberships.Select(row => row.Id).Intersect(theirs.Where(row => own.All(mine => mine.AccountId != row.AccountId)).Select(row => row.Id)));
        Assert.DoesNotContain(stranger.EmailAddress, answer.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(stranger.DisplayName, answer.Body, StringComparison.Ordinal);
    }
}
