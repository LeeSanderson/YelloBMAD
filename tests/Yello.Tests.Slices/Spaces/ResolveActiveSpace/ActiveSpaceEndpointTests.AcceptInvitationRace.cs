using System.Data.Common;
using System.Net;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;
using LabelWire = Yello.Tests.Slices.Tasks.LabelWire;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 4.3's races (AC5; Q8 (a); ledger <c>:725</c>): the accept takes the Space's Invitations lock
/// (<c>Yello/Invitations/{spaceId:D}</c>) first, then runs one guarded <c>UPDATE</c> composed with the shared actionable
/// predicate and the caller's address, then inserts one Membership - so two accepts, an accept and a revoke, and an accept
/// and an issue each serialise, and no second Membership and no Member-with-a-new-pending-Invitation can exist.
/// <c>TESTING-CONVENTIONS.md</c> "A race is controlled, never hoped for" and "When the race is about a read, the test does
/// the other write".
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 4.3 ATDD run (2026-11-01); activated by its dev phase (2026-11-02).</b> Deterministic: the test holds the lock (or
/// the row) on a connection opened under the Space's key, fires the request(s), waits on <c>LCK_M_%</c> through
/// <see cref="TaskWire.WaitUntilBlockedOnALockAsync"/> (or <see cref="LabelWire.WaitUntilBlockedOrAnsweredAsync"/> where
/// answering at once is the defect), then commits. Never a delay.
/// </para>
/// <para>
/// <b>Presence.</b> The invitee is not a Member of the Space before it accepts, so it has no <c>SpacePresence</c> row there
/// to serialise on; the Owner's own presence row was written by its issue, before the hold.
/// </para>
/// <para>
/// <b>The held revoke</b> is 4.2's guarded statement played by the test, uncommitted: <c>UPDATE ... SET State = Revoked</c>
/// keeps <c>SpaceId</c>, so the BLOCK AFTER UPDATE predicate admits it; the holder carries the Space's key, so the FILTER
/// predicate shows the row. The mirror (a revoke racing a held acceptance) is 4.2's
/// <c>A_revoke_racing_an_acceptance_that_commits_under_it_answers_409_and_leaves_it_accepted</c>, kept green.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// AC5 race 1, Q8 (a), AD-28 - two accepts of the same token by the same Account (distinct <c>Idempotency-Key</c>s) held
    /// on the Space's Invitations lock until both are blocked, then released: exactly one <c>200</c> and one uniform
    /// <c>404 invitation-not-valid</c>; exactly one Membership; the row <c>Accepted</c>. Plant 3 (load-check-save): two
    /// <c>200</c>s or a <c>500</c> from the alternate key.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-11")]
    [Trait("Requirement", "AR-32")]
    [Trait("Requirement", "AD-28")]
    public async Task Two_accepts_of_one_Invitation_held_on_the_Space_lock_admit_one_and_answer_the_other_uniformly()
    {
        var database = Available();
        var wire = Wire();
        Invited? invited = null;
        WireResponse[] raced = [];

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            invited = await InviteAsync(client, database, wire, mail, "raced-twice", "member");

            var invitee = await RegisteredAndSignedInAsync(client, invited.Invitee, DisplayName());

            await invitee.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(invited.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, InvitationsLockOf(invited.SpaceId), cancellationToken);

            var first = AcceptAsync(invitee, invited.Token, Guid.NewGuid().ToString("D"));
            var second = AcceptAsync(invitee, invited.Token, Guid.NewGuid().ToString("D"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(first, second), cancellationToken);
            await held.CommitAsync(cancellationToken);

            raced = await Task.WhenAll(first, second);
        });

        Assert.Single(raced, answer => answer.StatusCode == HttpStatusCode.OK);

        var refused = Assert.Single(raced, answer => answer.StatusCode != HttpStatusCode.OK);

        Assert.True(refused.StatusCode == HttpStatusCode.NotFound, $"{refused.StatusCode} {refused.Body}");
        Assert.Equal(ProblemTypes.InvitationNotValid, ProblemType(refused));
        Assert.Single(await MembershipsInAsync(database, invited!.SpaceId), row => row.AccountId != invited.OwnerId);
        Assert.Equal("Accepted", Assert.Single(await wire.InvitationsInAsync(invited.SpaceId)).State);
    }

    /// <summary>
    /// AC5 race 2, AD-27, AD-28 - an accept racing a revoke that commits under it: the test holds the row set
    /// <c>Revoked</c> uncommitted, fires the accept, waits until it is blocked on the row, then commits. The accept's guarded
    /// <c>UPDATE</c> re-evaluates against the committed <c>Revoked</c>, matches no row, and answers the uniform <c>404</c>;
    /// no Membership; the row stays <c>Revoked</c>. Plant 2 (no actionable guard) or 3 (load-check-save, which read the
    /// committed <c>Pending</c> before the holder committed): a Membership.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-11")]
    [Trait("Requirement", "FR-12")]
    [Trait("Requirement", "AD-27")]
    public async Task An_accept_racing_a_revoke_that_commits_under_it_answers_uniformly_and_joins_nothing()
    {
        var database = Available();
        var wire = Wire();
        Invited? invited = null;
        WireResponse? raced = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            invited = await InviteAsync(client, database, wire, mail, "raced-revoke", "member");

            var invitee = await RegisteredAndSignedInAsync(client, invited.Invitee, DisplayName());

            await invitee.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(invited.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await RevokeUncommittedAsync(holder, held, invited.InvitationId, cancellationToken);

            var racing = AcceptAsync(invitee, invited.Token);

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            raced = await racing;
        });

        Assert.True(raced?.StatusCode == HttpStatusCode.NotFound, $"{raced?.StatusCode} {raced?.Body}");
        Assert.Equal(ProblemTypes.InvitationNotValid, ProblemType(raced!));
        Assert.DoesNotContain(await MembershipsInAsync(database, invited!.SpaceId), row => row.AccountId != invited.OwnerId);
        Assert.Equal("Revoked", Assert.Single(await wire.InvitationsInAsync(invited.SpaceId)).State);
    }

    /// <summary>
    /// AC5 race 3, Q8 (a), ledger <c>:725</c> - the accept waits on the Space's Invitations lock: with the lock held, the
    /// accept must not answer (<see cref="LabelWire.WaitUntilBlockedOrAnsweredAsync"/>); an issue to the same address then
    /// queues behind it; on release the accept is <c>200</c> and the issue is refused <c>409</c> (<c>already-a-member</c>,
    /// or <c>invitation-pending</c> if it ran first), and the address holds exactly one Invitation (the accepted one) and
    /// one Membership. Plant 5 (the accept without the lock): it answers while the lock is held.
    /// </summary>
    /// <remarks>
    /// <b>Why the decisive assertion is "it waited".</b> With one Invitation per address, the issue's own AC13 check
    /// (<c>invitation-pending</c>) refuses an issue that reads before the accept commits, so the end state alone cannot show
    /// a missing lock; the wait can. Recorded for the dev phase in the ATDD checklist.
    /// </remarks>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-11")]
    [Trait("Requirement", "FR-10")]
    [Trait("Requirement", "AD-27")]
    public async Task An_accept_waits_on_the_Space_Invitations_lock_and_an_issue_to_the_same_address_queues_behind_it()
    {
        var database = Available();
        var wire = Wire();
        Invited? invited = null;
        var answeredWhileHeld = true;
        WireResponse? accepted = null;
        WireResponse? issued = null;
        var inviteeId = Guid.Empty;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            invited = await InviteAsync(client, database, wire, mail, "raced-issue", "member");

            var invitee = await RegisteredAndSignedInAsync(client, invited.Invitee, DisplayName());

            inviteeId = await AccountIdOfAsync(database, invited.Invitee);
            await invitee.FetchAntiforgeryTokenAsync();
            await invited.Owner.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(invited.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, InvitationsLockOf(invited.SpaceId), cancellationToken);

            var accepting = AcceptAsync(invitee, invited.Token);

            await LabelWire.WaitUntilBlockedOrAnsweredAsync(observer, 1, accepting, cancellationToken);
            answeredWhileHeld = accepting.IsCompleted;

            var issuing = IssueAsync(invited.Owner, invited.SpaceId, invited.Invitee, "viewer");

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, answeredWhileHeld ? 1 : 2, issuing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            accepted = await accepting;
            issued = await issuing;
        });

        Assert.False(answeredWhileHeld, $"The accept answered while the Space's Invitations lock was held: {accepted?.StatusCode} {accepted?.Body}");
        Assert.True(accepted?.StatusCode == HttpStatusCode.OK, $"{accepted?.StatusCode} {accepted?.Body}");
        Assert.True(issued?.StatusCode == HttpStatusCode.Conflict, $"{issued?.StatusCode} {issued?.Body}");
        var issueType = ProblemType(issued!);

        Assert.True(issueType == ProblemTypes.AlreadyAMember || issueType == ProblemTypes.InvitationPending, $"The issue answered {issueType}.");
        Assert.Equal("Accepted", Assert.Single(await wire.InvitationsInAsync(invited!.SpaceId), row => row.EmailAddress == invited.Invitee).State);
        Assert.Single(await MembershipsInAsync(database, invited.SpaceId), row => row.AccountId == inviteeId);
    }

    /// <summary>
    /// What 4.2's revoke writes, held uncommitted in <paramref name="held"/>: the row set <c>Revoked</c>.
    /// </summary>
    private static async Task RevokeUncommittedAsync(SqlConnection holder, DbTransaction held, Guid invitationId, CancellationToken cancellationToken)
    {
        await using DbCommand command = holder.CreateCommand();

        command.Transaction = held;
        command.CommandText = "UPDATE dbo.[Invitation] SET State = N'Revoked' WHERE Id = @id AND State = N'Pending'";
        command.Parameters.Add(new SqlParameter("@id", invitationId));

        Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
    }
}
