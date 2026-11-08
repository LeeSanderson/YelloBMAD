using System.Data.Common;
using System.Net;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Spaces;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Removal and leaving under contention. Both take the Space's Memberships application lock
/// (<c>Yello/Memberships/{spaceId:D}</c>) before their one guarded <c>DELETE</c>, as the Role change does, so every case
/// holds that lock on a connection under the Space's key, fires the request(s), waits on <c>LCK_M_%</c>, then commits.
/// Never a delay.
/// </summary>
/// <remarks>
/// <para>
/// <b>Seeding.</b> Participants are seeded through <see cref="WireDatabase.AddMembershipAsync"/> and made present in the
/// Space with a read first: the overlap is the point, and an accept per participant (with its own lock, its own publish
/// and its own presence insert) would bury it.
/// </para>
/// <para>
/// <b>Not here:</b> an acceptance against a held Memberships lock, which the Role change's race file already proves
/// answers without waiting (<c>An_acceptance_answers_while_the_Spaces_Memberships_lock_is_held</c>); the removal takes
/// the same lock and adds nothing to that proof.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// The Owner's removal of a Viewer and an Admin's Viewer-to-Member change of the same Viewer, held on the Memberships lock
    /// until both are blocked, then released. Each sees the other's commit, so the end is one of the two serial outcomes:
    /// removal first (the change then finds no row: the in-Space <c>404</c>), or change first then removal (<c>200</c>,
    /// <c>204</c>). Either way the removal answers <c>204</c>, the Viewer is gone, and one Owner is left. The guarded
    /// statements' row locks would serialise the two without the application lock too, so the deciding assertion is that
    /// both waited on the held lock.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AR-12")]
    [Trait("Assumption", "PRD-12-4")]
    public async Task A_removal_and_a_Role_change_of_one_target_held_on_the_Memberships_lock_end_in_a_serial_outcome()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, adminAddress, targetAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var targetId = Guid.Empty;
        WireResponse[] raced = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Removal raced " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);
            var admin = await SignedInAsync(client, adminAddress);
            await wire.AddMembershipAsync(spaceId, await AccountIdOfAsync(database, adminAddress), "admin");
            _ = await SignedInAsync(client, targetAddress);
            targetId = await AccountIdOfAsync(database, targetAddress);
            await wire.AddMembershipAsync(spaceId, targetId, "viewer");

            var target = await wire.MembershipOfAsync(spaceId, targetId);

            await MadePresentAsync(owner, spaceId);
            await MadePresentAsync(admin, spaceId);
            await owner.FetchAntiforgeryTokenAsync();
            await admin.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, MembershipsLockOf(spaceId), cancellationToken);

            var removal = RemoveOverWireAsync(owner, spaceId, target.Id, Guid.NewGuid().ToString("D"));
            var change = ChangeRoleOverWireAsync(admin, spaceId, target.Id, "member", Guid.NewGuid().ToString("D"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(removal, change), cancellationToken);
            await held.CommitAsync(cancellationToken);

            raced = await Task.WhenAll(removal, change);
        });

        var (removed, changed) = (raced[0], raced[1]);
        var compared = $"removal: {Described(removed)}; change: {Described(changed)}";

        Assert.True(removed.StatusCode == HttpStatusCode.NoContent, "The Owner's removal succeeds in either order. " + compared);
        Assert.True(changed.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound, "The change answers 200 (it ran first) or the in-Space 404 (it ran second). " + compared);

        if (changed.StatusCode == HttpStatusCode.NotFound)
        {
            Assert.Equal(ProblemTypes.NotAvailable, ProblemType(changed));
        }

        Assert.DoesNotContain(await wire.MembershipRowsInAsync(spaceId), row => row.AccountId == targetId);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// An Admin's removal of a Member is queued behind the held Memberships lock after the pipeline has read its Role; the
    /// holder then demotes that Admin to Member in its own transaction and commits. The guarded statement re-checks the
    /// actor's Role and finds the authority gone: <c>403</c>, recorded, the Member still there. Without the actor clause the
    /// removal answers <c>204</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-2")]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AD-20")]
    [Trait("Assumption", "PRD-12-4")]
    public async Task An_Admin_demoted_while_its_removal_is_queued_is_refused_403_and_the_target_stays()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, adminAddress, targetAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var (adminId, targetId) = (Guid.Empty, Guid.Empty);
        WireResponse? queued = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            _ = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Removal demoted " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);
            var admin = await SignedInAsync(client, adminAddress);
            adminId = await AccountIdOfAsync(database, adminAddress);
            await wire.AddMembershipAsync(spaceId, adminId, "admin");
            _ = await SignedInAsync(client, targetAddress);
            targetId = await AccountIdOfAsync(database, targetAddress);
            await wire.AddMembershipAsync(spaceId, targetId, "member");

            var (adminRow, target) = (await wire.MembershipOfAsync(spaceId, adminId), await wire.MembershipOfAsync(spaceId, targetId));

            await MadePresentAsync(admin, spaceId);
            await admin.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, MembershipsLockOf(spaceId), cancellationToken);

            var removal = RemoveOverWireAsync(admin, spaceId, target.Id);

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, removal, cancellationToken);
            await DemoteUncommittedAsync(holder, held, adminRow.Id, cancellationToken);
            await held.CommitAsync(cancellationToken);

            queued = await removal;
        });

        Assert.True(queued?.StatusCode == HttpStatusCode.Forbidden, $"The Admin demoted while its removal was queued: {Described(queued)}");
        Assert.Equal(ProblemTypes.InsufficientRole, ProblemType(queued));
        Assert.Equal("Member", (await wire.MembershipOfAsync(spaceId, targetId)).Role);
        Assert.Contains(await RefusalsOfAsync(database, adminId), row => row.TargetSpaceId == spaceId && row.Kind == "InsufficientRole" && row.Capability == nameof(Capability.RemoveMembership));
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// A Member's leave and the Owner's removal of that Member, held on the Memberships lock until both are blocked, then
    /// released: exactly one answers <c>204</c>; the other answers <c>404 not-available</c> (the in-Space not-found if the
    /// removal ran second, the boundary <c>404</c> if the leave did); the Member is gone; and the run logs exactly one
    /// <c>MembershipChanged</c>, naming the Member.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AR-16")]
    public async Task A_leave_and_a_removal_of_one_Membership_held_on_the_lock_commit_once_and_publish_once()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var memberId = Guid.Empty;
        WireResponse[] raced = [];

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Leave raced " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);
            var member = await SignedInAsync(client, memberAddress);
            memberId = await AccountIdOfAsync(database, memberAddress);
            await wire.AddMembershipAsync(spaceId, memberId, "member");

            var row = await wire.MembershipOfAsync(spaceId, memberId);

            await MadePresentAsync(owner, spaceId);
            await MadePresentAsync(member, spaceId);
            await owner.FetchAntiforgeryTokenAsync();
            await member.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, MembershipsLockOf(spaceId), cancellationToken);

            var leave = LeaveOverWireAsync(member, spaceId, Guid.NewGuid().ToString("D"));
            var removal = RemoveOverWireAsync(owner, spaceId, row.Id, Guid.NewGuid().ToString("D"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(leave, removal), cancellationToken);
            await held.CommitAsync(cancellationToken);

            raced = await Task.WhenAll(leave, removal);
        });

        var compared = $"leave: {Described(raced[0])}; removal: {Described(raced[1])}";
        var publishes = log.Split('\n').Where(line => line.Contains(MembershipWire.PublishedEvent, StringComparison.Ordinal)).ToList();

        Assert.True(raced.Count(answer => answer.StatusCode == HttpStatusCode.NoContent) == 1, "Exactly one commits. " + compared);
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(Assert.Single(raced, answer => answer.StatusCode == HttpStatusCode.NotFound)));
        Assert.DoesNotContain(await wire.MembershipRowsInAsync(spaceId), row => row.AccountId == memberId);
        Assert.True(publishes.Count == 1, $"Expected one publish; found {publishes.Count}: {string.Join(" | ", publishes)}");
        Assert.Equal(1, PublishesNaming(log, memberId, spaceId));
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// An actor removed while its own Role change or removal waits on the Memberships lock is answered as its very next
    /// request would be: the boundary <c>404 not-available</c>, recorded <c>CrossSpace</c> by the pipeline, never a
    /// <c>403</c> that tells it it still reached the Space's authority check. The holder deletes the Admin's Membership in
    /// its own transaction while the request waits, then commits; the target is unchanged. Mapping the lost Membership to
    /// the Qualified refusal turns this into a <c>403</c>.
    /// </summary>
    /// <param name="action">What the Admin was doing.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("change")]
    [InlineData("removal")]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-20")]
    [Trait("Requirement", "NFR-2")]
    public async Task An_Admin_removed_while_its_action_waits_meets_the_boundary_404_recorded_CrossSpace(string action)
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, adminAddress, targetAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var (adminId, targetId) = (Guid.Empty, Guid.Empty);
        MembershipRow? before = null;
        WireResponse? queued = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            _ = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Actor removed " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);
            var admin = await SignedInAsync(client, adminAddress);
            adminId = await AccountIdOfAsync(database, adminAddress);
            await wire.AddMembershipAsync(spaceId, adminId, "admin");
            _ = await SignedInAsync(client, targetAddress);
            targetId = await AccountIdOfAsync(database, targetAddress);
            await wire.AddMembershipAsync(spaceId, targetId, "member");

            var adminRow = await wire.MembershipOfAsync(spaceId, adminId);

            before = await wire.MembershipOfAsync(spaceId, targetId);

            await MadePresentAsync(admin, spaceId);
            await admin.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, MembershipsLockOf(spaceId), cancellationToken);

            var waiting = action == "change"
                ? ChangeRoleOverWireAsync(admin, spaceId, before.Id, "viewer")
                : RemoveOverWireAsync(admin, spaceId, before.Id);

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, waiting, cancellationToken);
            await RemoveUncommittedAsync(holder, held, adminRow.Id, cancellationToken);
            await held.CommitAsync(cancellationToken);

            queued = await waiting;
        });

        Assert.True(queued?.StatusCode == HttpStatusCode.NotFound, $"The Admin removed while its {action} waited: {Described(queued)}");
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(queued));
        Assert.Equal(before, await wire.MembershipOfAsync(spaceId, targetId));

        var refusals = (await RefusalsOfAsync(database, adminId)).Where(row => row.TargetSpaceId == spaceId).ToList();

        Assert.Contains(refusals, row => row.Kind == "CrossSpace");
        Assert.DoesNotContain(refusals, row => row.Kind == "InsufficientRole");
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// A Token issued by an Account whose Membership is deleted under the request is the boundary <c>404</c>, recorded
    /// <c>CrossSpace</c> - never a <c>500</c>, and no Token is left bound there. The issue takes no Memberships lock, so the
    /// race holds the Membership row itself: the holder deletes it uncommitted, the issue resolves the committed Membership
    /// (RCSI) and blocks on the foreign key check of its insert, then the holder commits and the insert fails <c>547</c>,
    /// which the pipeline re-resolves and answers as the next request would be answered. <b>Not skipped</b>: the raw-SQL
    /// removal reaches the product's issue path today, so it runs as a standing gate on that path.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-20")]
    public async Task A_Token_issue_racing_the_removal_of_its_issuer_is_the_boundary_404_and_never_a_500()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var memberId = Guid.Empty;
        var crossSpaceBefore = 0;
        WireResponse? issued = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            _ = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Issue raced " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);
            var member = await SignedInAsync(client, memberAddress);
            memberId = await AccountIdOfAsync(database, memberAddress);
            await wire.AddMembershipAsync(spaceId, memberId, "member");

            var row = await wire.MembershipOfAsync(spaceId, memberId);

            await MadePresentAsync(member, spaceId);
            await member.FetchAntiforgeryTokenAsync();
            crossSpaceBefore = (await RefusalsOfAsync(database, memberId)).Count(refusal => refusal.TargetSpaceId == spaceId && refusal.Kind == "CrossSpace");

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await RemoveUncommittedAsync(holder, held, row.Id, cancellationToken);

            var issue = IssueAsync(member, spaceId, "raced");

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, issue, cancellationToken);
            await held.CommitAsync(cancellationToken);

            issued = await issue;
        });

        Assert.True(issued?.StatusCode == HttpStatusCode.NotFound, $"The Token issue racing its issuer's removal: {Described(issued)}");
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(issued));
        Assert.Equal(0, await wire.TokenCountAsync(memberId, spaceId));
        Assert.Equal(
            crossSpaceBefore + 1,
            (await RefusalsOfAsync(database, memberId)).Count(refusal => refusal.TargetSpaceId == spaceId && refusal.Kind == "CrossSpace"));
    }

    /// <summary>
    /// A read in the Space, so the Account's presence row exists before the race and its request does not insert one on
    /// the way (an insert would queue behind the other participant's and serialise the pair before the race).
    /// </summary>
    private static async Task MadePresentAsync(WireSession session, Guid spaceId) =>
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync(SpaceRoutes.ActiveOf(spaceId))).StatusCode);

    /// <summary>
    /// The holder's deletion of one Membership, uncommitted, in its own transaction, under the Space's key: the product's
    /// statement shape, which the Token cascade follows.
    /// </summary>
    private static async Task RemoveUncommittedAsync(SqlConnection holder, DbTransaction held, Guid membershipId, CancellationToken cancellationToken)
    {
        await using DbCommand command = holder.CreateCommand();

        command.Transaction = held;
        command.CommandText = "DELETE FROM dbo.[Membership] WHERE Id = @id";
        command.Parameters.Add(new SqlParameter("@id", membershipId));

        Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
    }
}
