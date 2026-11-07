using System.Data.Common;
using System.Net;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using LabelWire = Yello.Tests.Slices.Tasks.LabelWire;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 4.4's races (AC11; Q17 (a), Lee's Q1 (a)): the Role change takes the Space's Memberships application lock
/// (<c>Yello/Memberships/{spaceId:D}</c>) first, then runs its one guarded <c>UPDATE</c> - the target's Role in the actor's
/// <c>From</c> set, the new Role in its <c>To</c> set, and the actor still holding the Role its authority was computed from.
/// <c>TESTING-CONVENTIONS.md</c> "A race is controlled, never hoped for".
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 4.4 ATDD run (2026-11-02).</b> Deterministic: the test holds the lock on a
/// connection opened under the Space's key, fires the request(s), waits on <c>LCK_M_%</c> through
/// <see cref="TaskWire.WaitUntilBlockedOnALockAsync"/> (or <see cref="LabelWire.WaitUntilBlockedOrAnsweredAsync"/> where
/// waiting is the defect), then commits. Never a delay.
/// </para>
/// <para>
/// <b>Seeding.</b> The races' Owner, Admins and targets are seeded through <see cref="WireDatabase.AddMembershipAsync"/>:
/// the point is the overlap, and an accept per participant (with its own lock and its own <c>MembershipChanged</c>, Q18 (a))
/// would bury it; recorded. Race 3's invitee joins through the product, because the accept is its subject.
/// </para>
/// <para>
/// <b>Race 3 is a standing gate</b>: the Role change takes the Memberships lock and acceptance takes the Invitations lock
/// only, so the accept answers while the Memberships lock is held. It turns red if a later story makes acceptance queue
/// behind a Role change, which Q17 (a) says it must not.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// AC11 race 1, Q17 (a), S-2 - two writers on one target: the Owner's Viewer to Admin and an Admin's Viewer to Member,
    /// distinct keys, held on the Space's Memberships lock until both are blocked, then released. The second to run sees the
    /// first's commit, so the end state is one of the two serial outcomes - the Owner's first (the target is Admin, the
    /// Admin's change is then out of authority: <c>403</c>, recorded) or the Admin's first (Member, then the Owner's Member to
    /// Admin: both <c>200</c>) - and either way the target ends Admin, the Owner's change succeeded, and the Space holds
    /// exactly one Owner. Plant 5 (the lock removed) is expected masked on the end state - the guarded statement's row lock
    /// serialises the two as well - and is recorded so; the deciding assertion is that both waited on the held lock.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-13")]
    [Trait("Requirement", "AR-12")]
    [Trait("Assumption", "PRD-12-4")]
    public async Task An_Owners_and_an_Admins_changes_to_one_target_held_on_the_Memberships_lock_end_in_a_serial_outcome()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, adminAddress, targetAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var (adminId, targetId) = (Guid.Empty, Guid.Empty);
        WireResponse[] raced = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Raced " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);
            var admin = await SignedInAsync(client, adminAddress);
            adminId = await AccountIdOfAsync(database, adminAddress);
            await wire.AddMembershipAsync(spaceId, adminId, "admin");
            _ = await SignedInAsync(client, targetAddress);
            targetId = await AccountIdOfAsync(database, targetAddress);
            await wire.AddMembershipAsync(spaceId, targetId, "viewer");

            var target = await wire.MembershipOfAsync(spaceId, targetId);

            await owner.FetchAntiforgeryTokenAsync();
            await admin.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, MembershipsLockOf(spaceId), cancellationToken);

            var byOwner = ChangeRoleOverWireAsync(owner, spaceId, target.Id, "admin", Guid.NewGuid().ToString("D"));
            var byAdmin = ChangeRoleOverWireAsync(admin, spaceId, target.Id, "member", Guid.NewGuid().ToString("D"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(byOwner, byAdmin), cancellationToken);
            await held.CommitAsync(cancellationToken);

            raced = await Task.WhenAll(byOwner, byAdmin);
        });

        var (ownerAnswer, adminAnswer) = (raced[0], raced[1]);
        var compared = $"Owner: {Described(ownerAnswer)}; Admin: {Described(adminAnswer)}";

        Assert.True(ownerAnswer.StatusCode == HttpStatusCode.OK, "The Owner's change must succeed in either order. " + compared);
        Assert.True(adminAnswer.StatusCode is HttpStatusCode.OK or HttpStatusCode.Forbidden, "The Admin's change answers 200 (it ran first) or 403 (it ran second). " + compared);
        Assert.Equal("Admin", (await wire.MembershipOfAsync(spaceId, targetId)).Role);

        if (adminAnswer.StatusCode == HttpStatusCode.Forbidden)
        {
            Assert.Equal(ProblemTypes.InsufficientRole, ProblemType(adminAnswer));
            Assert.Contains(await RefusalsOfAsync(database, adminId), row => row.TargetSpaceId == spaceId && row.Kind == "InsufficientRole");
        }

        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// AC11 race 2, Lee's Q1 (a) (the actor-still-holds-its-Role clause), NFR-2 "no tolerance" - an Admin's Member to
    /// Viewer is queued behind the held Memberships lock after the pipeline has already read its Role; the holder then, in
    /// its own transaction, demotes that Admin to Member (the product's statement shape) and commits. The queued change must
    /// answer <c>403 insufficient-role</c>, recorded, with the target still a Member: the guarded statement re-checks the
    /// actor's Role and finds the authority gone. Plant 4: the actor clause removed - it answers <c>200</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-2")]
    [Trait("Requirement", "FR-13")]
    [Trait("Requirement", "AD-20")]
    [Trait("Assumption", "PRD-12-4")]
    public async Task An_Admin_demoted_while_its_change_is_queued_is_refused_403_and_the_target_is_unchanged()
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
            await SeedNamedSpaceAsync(database, spaceId, "Demoted in flight " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);
            var admin = await SignedInAsync(client, adminAddress);
            adminId = await AccountIdOfAsync(database, adminAddress);
            await wire.AddMembershipAsync(spaceId, adminId, "admin");
            _ = await SignedInAsync(client, targetAddress);
            targetId = await AccountIdOfAsync(database, targetAddress);
            await wire.AddMembershipAsync(spaceId, targetId, "member");

            var (adminRow, target) = (await wire.MembershipOfAsync(spaceId, adminId), await wire.MembershipOfAsync(spaceId, targetId));

            await admin.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, MembershipsLockOf(spaceId), cancellationToken);

            var change = ChangeRoleOverWireAsync(admin, spaceId, target.Id, "viewer");

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, change, cancellationToken);
            await DemoteUncommittedAsync(holder, held, adminRow.Id, cancellationToken);
            await held.CommitAsync(cancellationToken);

            queued = await change;
        });

        Assert.True(queued?.StatusCode == HttpStatusCode.Forbidden, $"The Admin demoted while queued: {Described(queued)}");
        Assert.Equal(ProblemTypes.InsufficientRole, ProblemType(queued));
        Assert.Equal("Member", (await wire.MembershipOfAsync(spaceId, targetId)).Role);
        Assert.Equal("Member", (await wire.MembershipOfAsync(spaceId, adminId)).Role);
        Assert.Contains(await RefusalsOfAsync(database, adminId), row => row.TargetSpaceId == spaceId && row.Kind == "InsufficientRole");
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// AC11 race 3, Q17 (a) - a Role change never blocks an acceptance in the same Space, and the two never deadlock: with the
    /// Space's Memberships lock held, an accept (which takes <c>Yello/Invitations/{spaceId}</c>, 4.3) answers <c>200</c>
    /// before the lock is released. That proves the two locks independent. <b>Green today, left active</b> (see the file's
    /// remarks). If Q17 ever chooses one shared lock, this case is inverted and the order stated.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-11")]
    [Trait("Requirement", "FR-13")]
    public async Task An_acceptance_answers_while_the_Spaces_Memberships_lock_is_held()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, inviteeAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var answeredWhileHeld = false;
        WireResponse? accepted = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedNamedSpaceAsync(database, spaceId, "Independent locks " + Suffix(), await AccountIdOfAsync(database, ownerAddress), Role.Owner);
            var invitee = await RegisteredAndSignedInAsync(client, inviteeAddress, DisplayName());

            await owner.FetchAntiforgeryTokenAsync();

            var issued = await IssueAsync(owner, spaceId, inviteeAddress, "member");

            Assert.True(issued.StatusCode == HttpStatusCode.NoContent, $"Issuing through the product: {Described(issued)}");

            var token = InvitationWire.TokenIn(await mail.WaitForEmailToAsync(inviteeAddress));

            await invitee.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, MembershipsLockOf(spaceId), cancellationToken);

            var accept = AcceptAsync(invitee, token);

            await LabelWire.WaitUntilBlockedOrAnsweredAsync(observer, 1, accept, cancellationToken);

            answeredWhileHeld = accept.IsCompleted;

            await held.CommitAsync(cancellationToken);

            accepted = await accept;
        });

        Assert.True(answeredWhileHeld, $"The accept waited on the held Memberships lock; it answered {Described(accepted)} only after the release.");
        Assert.True(accepted?.StatusCode == HttpStatusCode.OK, $"The accept: {Described(accepted)}");
        Assert.Equal("Member", (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, inviteeAddress))).Role);
    }

    /// <summary>
    /// The product's application lock for a Space's Memberships, as story 4.4 Q17 (a) names it.
    /// </summary>
    private static string MembershipsLockOf(Guid spaceId) => $"Yello/Memberships/{spaceId:D}";

    /// <summary>
    /// The holder's demotion of the acting Admin, uncommitted, in its own transaction - the product's statement shape (a
    /// guarded <c>UPDATE</c> of one row that keeps its Space, so a BLOCK AFTER UPDATE predicate admits it under the holder's
    /// key).
    /// </summary>
    private static async Task DemoteUncommittedAsync(SqlConnection holder, DbTransaction held, Guid membershipId, CancellationToken cancellationToken)
    {
        await using DbCommand command = holder.CreateCommand();

        command.Transaction = held;
        command.CommandText = "UPDATE dbo.[Membership] SET Role = N'Member' WHERE Id = @id AND Role = N'Admin'";
        command.Parameters.Add(new SqlParameter("@id", membershipId));

        Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
    }
}
