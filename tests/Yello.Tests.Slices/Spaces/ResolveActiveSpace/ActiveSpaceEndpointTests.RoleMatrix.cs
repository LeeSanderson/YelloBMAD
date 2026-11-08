using System.Net;
using System.Net.Http.Json;
using Xunit;
using Yello.Contracts.ApiTokens;
using Yello.Contracts.Invitations;
using Yello.Contracts.Labels;
using Yello.Contracts.Memberships;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;
using Yello.Contracts.Tasks;
using Yello.Domain;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// The Role capability matrix exercised through the product's own paths: an Owner who registered and created the Space, an
/// Admin, a Member and a Viewer who each joined through an accepted Invitation at that Role, and a second Viewer produced by
/// the Owner demoting a Member. One representative route per capability row that has an endpoint, on both surfaces, with
/// the expected answer read from <see cref="RoleCapabilityMatrix.Decide"/> rather than from a table copied here.
/// </summary>
/// <remarks>
/// <para>
/// <b>The route table is read by a gate.</b> <c>RoleMatrixCoverageGateTests</c> in the Architecture suite reads
/// <see cref="RoleMatrixRoutes"/> from this file's source and requires it to name exactly the capabilities some endpoint
/// declares through <c>RequiresCapability</c>, so the first endpoint for a row that has none today fails that gate until it
/// is added here. Keep each row on a line of its own, starting with its constructor.
/// </para>
/// <para>
/// <b>Each row builds its own world on a Host of its own</b> (the joins read Invitation tokens from a mail drop, so the
/// class's pooled Hosts are stopped first and never used again in the case). A route whose success consumes its target -
/// a removal, a deletion - is given a fresh target per success; a Role change toggles one Member between Member and Viewer.
/// </para>
/// <para>
/// <b>Seed constraints checked.</b> Every Account registers through the product, so <c>FK_Membership_Account_AccountId</c>
/// holds; every Membership but the Owner's is written by the accept, so <c>AK_Membership_SpaceId_AccountId</c> and
/// <c>UX_Membership_SpaceId_Owner</c> are the product's to keep; the Space is created through <c>POST /api/v1/spaces</c>, so
/// it carries its default Statuses and the Task created here satisfies the <c>(SpaceId, StatusId)</c> key.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// One representative route per capability row that has an endpoint.
    /// </summary>
    internal static readonly MatrixRoute[] RoleMatrixRoutes =
    [
        new MatrixRoute(Capability.ReadProjectsAndTasks, "GET", ProjectRoutes.List, HttpStatusCode.OK),
        new MatrixRoute(Capability.ManageProjects, "POST", ProjectRoutes.Create, HttpStatusCode.Created),
        new MatrixRoute(Capability.ManageTasks, "POST", TaskRoutes.Create, HttpStatusCode.Created),
        new MatrixRoute(Capability.AssignTask, "PUT", TaskRoutes.Assignee, HttpStatusCode.OK),
        new MatrixRoute(Capability.ManageLabels, "POST", LabelRoutes.Create, HttpStatusCode.Created),
        new MatrixRoute(Capability.IssueAndRevokeInvitations, "POST", InvitationRoutes.Issue, HttpStatusCode.NoContent),
        new MatrixRoute(Capability.ChangeMembershipRole, "PATCH", MembershipRoutes.Membership, HttpStatusCode.OK),
        new MatrixRoute(Capability.RemoveMembership, "DELETE", MembershipRoutes.Membership, HttpStatusCode.NoContent),
        new MatrixRoute(Capability.RenameSpace, "PATCH", SpaceRoutes.Active, HttpStatusCode.OK),
        new MatrixRoute(Capability.DeleteSpace, "DELETE", SpaceRoutes.Active, HttpStatusCode.NoContent),
        new MatrixRoute(Capability.IssueApiTokenForOneself, "POST", ApiTokenRoutes.Issue, HttpStatusCode.Created, SessionOnly: true),
    ];

    /// <summary>
    /// The theory's rows: every capability in <see cref="RoleMatrixRoutes"/>.
    /// </summary>
    public static TheoryData<Capability> MatrixRows => [.. RoleMatrixRoutes.Select(route => route.Capability)];

    /// <summary>
    /// For one capability row, every product-made Membership on both surfaces gets what the matrix decides: <c>Allowed</c>
    /// is the route's success; <c>Denied</c> is <c>403 insufficient-role</c> naming the capability's stable code with one
    /// more <c>InsufficientRole</c> record for that Account; <c>Qualified</c> (an Admin changing or removing a Membership) is
    /// the success inside the target rule (on a Member) and the same <c>403</c> outside it (on another Admin). A Token on a
    /// Session-only route is the no-credential <c>401</c>, whatever the Role. The demoted Viewer's Token was issued while it
    /// was a Member, so every <c>Denied</c> Viewer cell also proves the Token narrowed on the very next request.
    /// </summary>
    /// <param name="capability">The capability row.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(MatrixRows))]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "AD-1")]
    [Trait("Requirement", "FR-13")]
    [Trait("Assumption", "PRD-12-4")]
    public async Task Each_matrix_row_governs_Memberships_that_arrived_through_the_product(Capability capability)
    {
        var database = Available();
        var route = RoleMatrixRoutes.Single(candidate => candidate.Capability == capability);
        var failures = new List<string>();

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var world = await MatrixWorldAsync(client, database, wire, mail);
            var probe = await MatrixProbeAsync(client, database, wire, mail, world, capability);

            // The Owner deletes last: its Session deletes a second Space of its own, its Token the matrix's Space.
            var ordered = world.Actors.OrderBy(actor => actor.Role == Role.Owner ? 1 : 0).ToList();

            foreach (var actor in ordered)
            {
                await MatrixCellAsync(failures, database, route, probe, actor, viaToken: false);
                await MatrixCellAsync(failures, database, route, probe, actor, viaToken: true);
            }

            if (capability != Capability.DeleteSpace)
            {
                await AssertOneOwnerEachAsync(wire, world.SpaceId);
            }
        });

        Assert.True(failures.Count == 0, "The matrix did not govern:\n" + string.Join('\n', failures));
    }

    /// <summary>
    /// One cell of the row: the actor on one surface gets what the matrix decides for its Role.
    /// </summary>
    private static async Task MatrixCellAsync(List<string> failures, RegistrationDatabase database, MatrixRoute route, MatrixProbe probe, MatrixActor actor, bool viaToken)
    {
        var capability = route.Capability;
        var decision = RoleCapabilityMatrix.Decide(actor.Role, capability);
        var cell = $"{capability} / {actor.Name} ({actor.Role}) / {(viaToken ? "Token" : "Session")}";

        if (route.SessionOnly && viaToken)
        {
            Expect(failures, cell, await probe.Inside(actor, viaToken), HttpStatusCode.Unauthorized);
            return;
        }

        if (decision == CapabilityDecision.Denied)
        {
            await ExpectRefusedAsync(failures, database, cell, actor, capability, () => probe.Inside(actor, viaToken));
            return;
        }

        Expect(failures, cell, await probe.Inside(actor, viaToken), route.Success);

        if (decision == CapabilityDecision.Qualified)
        {
            var outside = probe.Outside ?? throw new InvalidOperationException($"{capability} has a Qualified cell and no outside-the-rule target.");

            await ExpectRefusedAsync(failures, database, cell + " outside the target rule", actor, capability, () => outside(actor, viaToken));
        }
    }

    /// <summary>
    /// The matrix's Space and its five product-made Memberships, each with a Session and a Token bound to the Space. S-2 is
    /// checked once the demotion has landed.
    /// </summary>
    private static async Task<MatrixWorld> MatrixWorldAsync(HttpClient client, RegistrationDatabase database, WireDatabase wire, MailDrop mail)
    {
        var owner = await SignedInAsync(client, RandomAddress());

        await owner.FetchAntiforgeryTokenAsync();

        var spaceName = "Matrix " + Suffix();
        var spaceId = CreatedSpace(await CreateSpaceAsync(owner, spaceName)).Id;

        async Task<MatrixActor> JoinAsync(string name, string role)
        {
            var address = RandomAddress();
            var session = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), address, DisplayName(), role);
            var accountId = await AccountIdOfAsync(database, address);

            return new MatrixActor(name, Enum.Parse<Role>(role, ignoreCase: true), session, accountId, (await wire.MembershipOfAsync(spaceId, accountId)).Id, IssuedToken(await IssueAsync(session, spaceId, "matrix " + name)).Plaintext);
        }

        var admin = await JoinAsync("Admin", "admin");
        var member = await JoinAsync("Member", "member");
        var viewer = await JoinAsync("Viewer", "viewer");

        // Joined as a Member and holding a Token from then, then demoted by the Owner through the product.
        var demoted = await JoinAsync("Demoted Viewer", "member");
        var demotion = await ChangeRoleOverWireAsync(owner, spaceId, demoted.MembershipId, "viewer");

        Assert.True(demotion.StatusCode == HttpStatusCode.OK, $"The Owner's demotion through the product: {Described(demotion)}");

        var ownerAccount = (await wire.MembershipRowsInAsync(spaceId)).Single(row => row.Role == "Owner");
        var ownerActor = new MatrixActor("Owner", Role.Owner, owner, ownerAccount.AccountId, ownerAccount.Id, IssuedToken(await IssueAsync(owner, spaceId, "matrix Owner")).Plaintext);

        foreach (var session in new[] { owner, admin.Session, member.Session, viewer.Session, demoted.Session })
        {
            await session.FetchAntiforgeryTokenAsync();
        }

        await AssertOneOwnerEachAsync(wire, spaceId);

        return new MatrixWorld(spaceId, spaceName, [ownerActor, admin, member, viewer, demoted with { Role = Role.Viewer }]);
    }

    /// <summary>
    /// The representative request for <paramref name="capability"/>: <c>Inside</c> is the request an Allowed or Qualified
    /// Role succeeds with (and a Denied Role is refused); <c>Outside</c>, for a capability with a Qualified cell, is the same
    /// route aimed at a target outside the Admin's rule - another Admin.
    /// </summary>
    private static async Task<MatrixProbe> MatrixProbeAsync(HttpClient client, RegistrationDatabase database, WireDatabase wire, MailDrop mail, MatrixWorld world, Capability capability)
    {
        var owner = world.Actors.Single(actor => actor.Role == Role.Owner);
        var spaceId = world.SpaceId;

        async Task<MatrixActor> ExtraAsync(string name, string role)
        {
            var address = RandomAddress();
            var session = await JoinedThroughTheProductAsync(client, wire, mail, (owner.Session, spaceId), address, DisplayName(), role);
            var accountId = await AccountIdOfAsync(database, address);

            return new MatrixActor(name, Enum.Parse<Role>(role, ignoreCase: true), session, accountId, (await wire.MembershipOfAsync(spaceId, accountId)).Id, string.Empty);
        }

        switch (capability)
        {
            case Capability.ReadProjectsAndTasks:
                return new MatrixProbe((actor, viaToken) => SendAsAsync(actor, viaToken, HttpMethod.Get, ProjectRoutes.ListIn(spaceId), body: null));

            case Capability.ManageProjects:
                return new MatrixProbe((actor, viaToken) => SendAsAsync(actor, viaToken, HttpMethod.Post, ProjectRoutes.ListIn(spaceId), JsonContent.Create(new { name = "Matrix " + Suffix() }, options: JsonOptions)));

            case Capability.ManageTasks:
            {
                var projectId = await NamedProjectAsync(owner.Session, spaceId, "Matrix tasks " + Suffix());

                return new MatrixProbe((actor, viaToken) => SendAsAsync(actor, viaToken, HttpMethod.Post, TaskRoutes.CreateIn(spaceId, projectId), JsonContent.Create(new { title = "Matrix " + Suffix() }, options: JsonOptions)));
            }

            case Capability.AssignTask:
            {
                var (_, taskId) = await TaskThroughTheProductAsync(owner.Session, spaceId, "Matrix assignment " + Suffix());

                return new MatrixProbe((actor, viaToken) => SendAsAsync(actor, viaToken, HttpMethod.Put, TaskRoutes.AssigneeOf(spaceId, taskId), AssignmentWire.Body(owner.MembershipId)));
            }

            case Capability.ManageLabels:
                return new MatrixProbe((actor, viaToken) => SendAsAsync(actor, viaToken, HttpMethod.Post, LabelRoutes.LabelsOf(spaceId), JsonContent.Create(new { name = "Matrix " + Suffix(), colour = LabelColours.Keys[0] }, options: JsonOptions)));

            case Capability.IssueAndRevokeInvitations:
                return new MatrixProbe((actor, viaToken) => SendAsAsync(actor, viaToken, HttpMethod.Post, InvitationRoutes.InvitationsOf(spaceId), InvitationWire.Body(InvitationWire.FreshAddress("matrix"), "member")));

            case Capability.ChangeMembershipRole:
                return RoleChangeProbe(spaceId, await ExtraAsync("Toggled Member", "member"), await ExtraAsync("Second Admin", "admin"));

            case Capability.RemoveMembership:
            {
                // One throwaway Member per success: the Owner's and the Admin's, on each surface.
                var throwaways = new Queue<MatrixActor>();

                for (var index = 0; index < 4; index++)
                {
                    throwaways.Enqueue(await ExtraAsync("Throwaway " + index, "member"));
                }

                return RemovalProbe(spaceId, throwaways, await ExtraAsync("Second Admin", "admin"));
            }

            case Capability.RenameSpace:
                return new MatrixProbe((actor, viaToken) => SendAsAsync(actor, viaToken, HttpMethod.Patch, SpaceRoutes.ActiveOf(spaceId), JsonContent.Create(new { name = world.SpaceName }, options: JsonOptions)));

            case Capability.DeleteSpace:
            {
                // The Owner's Session deletes a second Space of its own, so its Token still has the matrix's Space to delete.
                var second = CreatedSpace(await CreateSpaceAsync(owner.Session, "Matrix second " + Suffix())).Id;

                return new MatrixProbe((actor, viaToken) =>
                    SendAsAsync(actor, viaToken, HttpMethod.Delete, SpaceRoutes.ActiveOf(actor.Role == Role.Owner && !viaToken ? second : spaceId), body: null));
            }

            case Capability.IssueApiTokenForOneself:
                return new MatrixProbe((actor, viaToken) => SendAsAsync(actor, viaToken, HttpMethod.Post, ApiTokenRoutes.IssueIn(spaceId), JsonContent.Create(new IssueApiTokenRequest("matrix " + Suffix()), options: JsonOptions)));

            default:
                throw new InvalidOperationException($"{capability} has no representative route; add one to {nameof(RoleMatrixRoutes)}.");
        }
    }

    /// <summary>
    /// The Role change: inside the rule, <paramref name="toggled"/> is moved between Member and Viewer (each success flips
    /// it, so every Allowed or Qualified cell asks for a real change); outside it, <paramref name="otherAdmin"/> is asked to
    /// become a Member.
    /// </summary>
    private static MatrixProbe RoleChangeProbe(Guid spaceId, MatrixActor toggled, MatrixActor otherAdmin)
    {
        var current = "member";

        return new MatrixProbe(
            async (actor, viaToken) =>
            {
                var next = current == "member" ? "viewer" : "member";
                var answer = await SendAsAsync(actor, viaToken, HttpMethod.Patch, MembershipRoutes.MembershipOf(spaceId, toggled.MembershipId), MembershipWire.ChangeBody(next));

                if (answer.StatusCode == HttpStatusCode.OK)
                {
                    current = next;
                }

                return answer;
            },
            (actor, viaToken) => SendAsAsync(actor, viaToken, HttpMethod.Patch, MembershipRoutes.MembershipOf(spaceId, otherAdmin.MembershipId), MembershipWire.ChangeBody("member")));
    }

    /// <summary>
    /// The removal: a Role the matrix does not deny removes the next throwaway Member; a denied Role, and the Admin outside
    /// its rule, ask to remove <paramref name="otherAdmin"/>, which must survive.
    /// </summary>
    private static MatrixProbe RemovalProbe(Guid spaceId, Queue<MatrixActor> throwaways, MatrixActor otherAdmin) =>
        new(
            (actor, viaToken) =>
            {
                var allowed = RoleCapabilityMatrix.Decide(actor.Role, Capability.RemoveMembership) != CapabilityDecision.Denied;
                var target = allowed && throwaways.Count > 0 ? throwaways.Dequeue() : otherAdmin;

                return SendAsAsync(actor, viaToken, HttpMethod.Delete, MembershipRoutes.MembershipOf(spaceId, target.MembershipId), body: null);
            },
            (actor, viaToken) => SendAsAsync(actor, viaToken, HttpMethod.Delete, MembershipRoutes.MembershipOf(spaceId, otherAdmin.MembershipId), body: null));

    /// <summary>
    /// The request as the Session sends it (cookie and anti-forgery header) or as a script sends it (the actor's Token as a
    /// bearer and nothing else).
    /// </summary>
    private static Task<WireResponse> SendAsAsync(MatrixActor actor, bool viaToken, HttpMethod method, string path, HttpContent? body) =>
        viaToken
            ? actor.Session.SendWithTokenAndHeadersAsync(method, path, actor.Token, body, NoKey)
            : actor.Session.SendWithHeadersAsync(method, path, body, NoKey, withAntiforgeryToken: method != HttpMethod.Get);

    private static void Expect(List<string> failures, string cell, WireResponse answer, HttpStatusCode expected)
    {
        if (answer.StatusCode != expected)
        {
            failures.Add($"{cell}: expected {expected:D}, got {Described(answer)}");
        }
    }

    /// <summary>
    /// The request is <c>403 insufficient-role</c> naming the capability's stable code, and adds exactly one
    /// <c>InsufficientRole</c> record for the Account.
    /// </summary>
    private static async Task ExpectRefusedAsync(List<string> failures, RegistrationDatabase database, string cell, MatrixActor actor, Capability capability, Func<Task<WireResponse>> send)
    {
        var before = (await RefusalsOfAsync(database, actor.AccountId)).Count(row => row.Kind == "InsufficientRole");
        var answer = await send();
        var after = (await RefusalsOfAsync(database, actor.AccountId)).Count(row => row.Kind == "InsufficientRole");

        if (answer.StatusCode != HttpStatusCode.Forbidden || CapabilityOf(answer) != StableCode.Of(capability))
        {
            failures.Add($"{cell}: expected 403 {StableCode.Of(capability)}, got {Described(answer)}");
        }

        if (after != before + 1)
        {
            failures.Add($"{cell}: expected one more InsufficientRole record, {before} became {after}");
        }
    }

    /// <summary>
    /// A row of the theory's route table: the capability, the representative route's method and pattern, its success
    /// status, and whether only a Session reaches it.
    /// </summary>
    internal sealed record MatrixRoute(Capability Capability, string Method, string Pattern, HttpStatusCode Success, bool SessionOnly = false);

    /// <summary>
    /// A product-made Membership: its Session, its Account and Membership ids, and a Token bound to the matrix's Space.
    /// </summary>
    private sealed record MatrixActor(string Name, Role Role, WireSession Session, Guid AccountId, Guid MembershipId, string Token);

    private sealed record MatrixWorld(Guid SpaceId, string SpaceName, IReadOnlyList<MatrixActor> Actors);

    private sealed record MatrixProbe(Func<MatrixActor, bool, Task<WireResponse>> Inside, Func<MatrixActor, bool, Task<WireResponse>>? Outside = null);
}
