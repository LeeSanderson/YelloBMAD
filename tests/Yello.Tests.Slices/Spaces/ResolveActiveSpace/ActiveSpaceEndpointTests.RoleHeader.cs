using System.Net;
using System.Net.Http.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Boards;
using Yello.Contracts.Labels;
using Yello.Contracts.Lists;
using Yello.Contracts.Memberships;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;
using Yello.Contracts.Tasks;
using Yello.Domain;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// The Role an answer was judged under, echoed by the pipeline in a response header (<see cref="RoleHeader.HeaderName"/>)
/// on every <c>2xx</c> and on the <c>403 insufficient-role</c> of a Space-scoped request that resolved a Membership, and on
/// nothing else: never on a <c>404</c> of either kind, a <c>401</c>, a <c>400</c>, a <c>409</c> or a <c>422</c>. A replay
/// carries the Role of the replaying request, never one stored with the record.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every absence is anchored</b> on the same actor's own read carrying the header in the same case, so a Host that sends
/// it nowhere fails the anchor rather than passing the absence.
/// </para>
/// <para>
/// <b>Seeding.</b> The Memberships are made through the product (<see cref="MatrixWorldAsync"/>: an Owner who created the
/// Space, an Admin, a Member and a Viewer who accepted Invitations, and a Member demoted to Viewer by the Owner). Its seed
/// constraints are listed on <c>ActiveSpaceEndpointTests.RoleMatrix.cs</c>.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    private static readonly bool[] BothSurfaces = [false, true];

    private static readonly string[] LowerRoles = ["member", "viewer"];

    /// <summary>
    /// Every read a Viewer still makes answers <c>200</c> on both surfaces, and every read and write answer judged under a
    /// Membership carries the Role it was judged under: the Owner's say <c>owner</c>, the demoted Viewer's (whose Token was
    /// issued while it was a Member) say <c>viewer</c>. A write the matrix denies answers <c>403 insufficient-role</c> and
    /// still carries the Role, so the client learns why on the refusal itself.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-1")]
    [Trait("Requirement", "AD-9")]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "FR-15")]
    public async Task Every_answer_judged_under_a_Membership_echoes_the_Role_it_was_judged_under()
    {
        var database = Available();
        var failures = new List<string>();

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var world = await MatrixWorldAsync(client, database, wire, mail);
            var owner = world.Actors.Single(actor => actor.Role == Role.Owner);
            var (projectId, taskId) = await TaskThroughTheProductAsync(owner.Session, world.SpaceId, "Echoed " + Suffix());

            string[] reads =
            [
                SpaceRoutes.ActiveOf(world.SpaceId),
                ProjectRoutes.ListIn(world.SpaceId),
                BoardRoutes.BoardOf(world.SpaceId, projectId),
                ListRoutes.ListOf(world.SpaceId, projectId, new ListQuery(ListSorts.Status, [], [], DueFrom: null, DueTo: null, DueNone: false)),
                TaskRoutes.TaskOf(world.SpaceId, taskId),
                LabelRoutes.LabelsOf(world.SpaceId),
                AssigneeRoutes.ListOf(world.SpaceId),
                SpaceRoutes.StatusesOf(world.SpaceId),
            ];

            foreach (var actor in world.Actors)
            {
                foreach (var viaToken in BothSurfaces)
                {
                    await ExpectEchoesAsync(failures, world.SpaceId, reads, actor, viaToken);
                }
            }
        });

        Assert.True(failures.Count == 0, "The Role header did not echo the judged Role:\n" + string.Join('\n', failures));
    }

    /// <summary>
    /// No answer that resolved no Membership, or failed after resolving one, carries the Role header: the boundary
    /// <c>404</c> (an absent Space), the in-Space <c>404</c> (an id that matches nothing in the caller's own Space), the
    /// no-credential <c>401</c>, a malformed body's <c>400</c>, a reused key's <c>422</c> and the Owner's leave's
    /// <c>409 ownership-held</c>. The in-Space <c>404</c> keeps exactly the boundary <c>404</c>'s header names, so the header
    /// can never tell the two apart. Anchor: the same Owner's read of its own Space carries it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "FR-15")]
    [Trait("Requirement", "AD-1")]
    public async Task No_answer_but_a_2xx_or_a_403_insufficient_role_carries_the_Role_header()
    {
        var address = RandomAddress();
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, address);

            await owner.FetchAntiforgeryTokenAsync();

            var spaceId = CreatedSpace(await CreateSpaceAsync(owner, "Unechoed " + Suffix())).Id;
            var key = Guid.NewGuid().ToString("D");

            answers["anchor: own Space"] = await owner.GetAsync(SpaceRoutes.ActiveOf(spaceId));
            answers["boundary 404"] = await owner.GetAsync(TaskRoutes.TaskOf(Guid.NewGuid(), Guid.NewGuid()));
            answers["in-Space 404"] = await owner.GetAsync(TaskRoutes.TaskOf(spaceId, Guid.NewGuid()));
            answers["401"] = await new WireSession(client).GetAsync(SpaceRoutes.ActiveOf(spaceId));
            answers["400"] = await owner.SendWithHeadersAsync(HttpMethod.Post, ProjectRoutes.ListIn(spaceId), MalformedBody(), NoKey);

            var first = await owner.SendWithHeadersAsync(HttpMethod.Post, ProjectRoutes.ListIn(spaceId), JsonContent.Create(new { name = "Keyed " + Suffix() }, options: JsonOptions), KeyHeaders(key));

            Assert.True(first.StatusCode == HttpStatusCode.Created, $"The key's first use: {Described(first)}");

            answers["422"] = await owner.SendWithHeadersAsync(HttpMethod.Post, ProjectRoutes.ListIn(spaceId), JsonContent.Create(new { name = "Other " + Suffix() }, options: JsonOptions), KeyHeaders(key));
            answers["409"] = await LeaveOverWireAsync(owner, spaceId);
        });

        Assert.Equal("owner", answers["anchor: own Space"].Header(RoleHeader.HeaderName));
        Assert.Equal(HttpStatusCode.NotFound, answers["boundary 404"].StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, answers["in-Space 404"].StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, answers["401"].StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, answers["400"].StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, answers["422"].StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, answers["409"].StatusCode);
        Assert.Contains(ProblemTypes.OwnershipHeld, answers["409"].Body, StringComparison.Ordinal);

        foreach (var (what, answer) in answers.Where(pair => !pair.Key.StartsWith("anchor", StringComparison.Ordinal)))
        {
            Assert.True(answer.Header(RoleHeader.HeaderName) is null, $"The {what} carries the Role header: {Described(answer)}");
        }

        Assert.Equal(answers["boundary 404"].HeaderNames, answers["in-Space 404"].HeaderNames);
    }

    /// <summary>
    /// A replay answers the stored status and body with the Role of the <b>replaying</b> request, never one stored with the
    /// record: an Admin creates a Project under a key (<c>201</c>, <c>admin</c>), the Owner demotes it to Member through the
    /// product, and the same request replayed answers the same <c>201</c> body carrying <c>member</c>. Demoted again to
    /// Viewer, the replay is refused before the record is read - the capability check precedes the replay - so it answers
    /// <c>403 insufficient-role</c> naming <c>manage-projects</c>, carrying <c>viewer</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "AD-1")]
    [Trait("Requirement", "FR-16")]
    public async Task A_replay_carries_the_Role_of_the_replaying_request_and_never_a_stored_one()
    {
        var database = Available();
        var name = "Replayed " + Suffix();
        var key = Guid.NewGuid().ToString("D");
        var answers = new List<WireResponse>();

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var world = await MatrixWorldAsync(client, database, wire, mail);
            var owner = world.Actors.Single(actor => actor.Role == Role.Owner);
            var admin = world.Actors.Single(actor => actor.Role == Role.Admin);

            Task<WireResponse> CreateAsync() =>
                admin.Session.SendWithHeadersAsync(HttpMethod.Post, ProjectRoutes.ListIn(world.SpaceId), JsonContent.Create(new { name }, options: JsonOptions), KeyHeaders(key));

            answers.Add(await CreateAsync());

            foreach (var lower in LowerRoles)
            {
                var demoted = await ChangeRoleOverWireAsync(owner.Session, world.SpaceId, admin.MembershipId, lower);

                Assert.True(demoted.StatusCode == HttpStatusCode.OK, $"The Owner's demotion to {lower}: {Described(demoted)}");

                answers.Add(await CreateAsync());
            }

            await AssertOneOwnerEachAsync(wire, world.SpaceId);
        });

        var (original, asMember, asViewer) = (answers[0], answers[1], answers[2]);

        Assert.True(original.StatusCode == HttpStatusCode.Created, Described(original));
        Assert.Equal("admin", original.Header(RoleHeader.HeaderName));

        Assert.True(asMember.StatusCode == HttpStatusCode.Created, Described(asMember));
        Assert.Equal(original.Body, asMember.Body);
        Assert.Equal("member", asMember.Header(RoleHeader.HeaderName));

        Assert.True(asViewer.StatusCode == HttpStatusCode.Forbidden, Described(asViewer));
        Assert.Equal(StableCode.Of(Capability.ManageProjects), CapabilityOf(asViewer));
        Assert.Equal("viewer", asViewer.Header(RoleHeader.HeaderName));
    }

    /// <summary>
    /// An Admin refused outside its target rule - changing another Admin's Role - gets the same <c>403 insufficient-role</c>
    /// a denied Role gets, carrying <c>admin</c>: the client reads a genuine Admin limit from the header rather than a stale
    /// Role, and shows the Admin-limit copy only then.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-13")]
    [Trait("Requirement", "AD-1")]
    [Trait("Assumption", "PRD-12-4")]
    public async Task A_refusal_outside_the_Admins_target_rule_carries_admin()
    {
        var database = Available();
        WireResponse? refused = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var world = await MatrixWorldAsync(client, database, wire, mail);
            var owner = world.Actors.Single(actor => actor.Role == Role.Owner);
            var admin = world.Actors.Single(actor => actor.Role == Role.Admin);
            var otherAddress = RandomAddress();

            _ = await JoinedThroughTheProductAsync(client, wire, mail, (owner.Session, world.SpaceId), otherAddress, DisplayName(), "admin");

            var other = await wire.MembershipOfAsync(world.SpaceId, await AccountIdOfAsync(database, otherAddress));

            refused = await ChangeRoleOverWireAsync(admin.Session, world.SpaceId, other.Id, "member");
        });

        Assert.True(refused?.StatusCode == HttpStatusCode.Forbidden, Described(refused));
        Assert.Equal(MembershipWire.CapabilityCode, CapabilityOf(refused));
        Assert.Equal("admin", refused.Header(RoleHeader.HeaderName));
    }

    /// <summary>
    /// The actor's reads on one surface each answer <c>200</c> carrying its Role, and a Project create answers what the
    /// matrix decides (<c>201</c> or <c>403</c>) carrying it too.
    /// </summary>
    private static async Task ExpectEchoesAsync(List<string> failures, Guid spaceId, IReadOnlyList<string> reads, MatrixActor actor, bool viaToken)
    {
        var expected = StableCode.Of(actor.Role);
        var surface = viaToken ? "Token" : "Session";

        foreach (var read in reads)
        {
            ExpectEcho(failures, $"{actor.Name} / {surface} / GET {read}", await SendAsAsync(actor, viaToken, HttpMethod.Get, read, body: null), HttpStatusCode.OK, expected);
        }

        var write = await SendAsAsync(actor, viaToken, HttpMethod.Post, ProjectRoutes.ListIn(spaceId), JsonContent.Create(new { name = "Echoed " + Suffix() }, options: JsonOptions));
        var status = RoleCapabilityMatrix.Decide(actor.Role, Capability.ManageProjects) == CapabilityDecision.Denied ? HttpStatusCode.Forbidden : HttpStatusCode.Created;

        ExpectEcho(failures, $"{actor.Name} / {surface} / POST project", write, status, expected);
    }

    private static void ExpectEcho(List<string> failures, string what, WireResponse answer, HttpStatusCode status, string role)
    {
        if (answer.StatusCode != status)
        {
            failures.Add($"{what}: expected {status:D}, got {Described(answer)}");
        }

        if (!string.Equals(answer.Header(RoleHeader.HeaderName), role, StringComparison.Ordinal))
        {
            failures.Add($"{what}: expected {RoleHeader.HeaderName}: {role}, got '{answer.Header(RoleHeader.HeaderName)}'");
        }
    }
}
