using System.Data.Common;
using System.Net;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Assignment under contention. An assignment takes the Space's Memberships application lock
/// (<c>Yello/Memberships/{spaceId:D}</c>) in <b>Shared</b> mode before its one guarded <c>UPDATE</c>; a removal, a leave and a
/// Role change take the same name <b>Exclusive</b>. So an assignment and a removal serialise, and two assignments in one
/// Space do not wait on each other. Each case holds that lock on a connection under the Space's key, sends the request,
/// waits on <c>LCK_M_%</c> (an application-lock wait shows as <c>LCK_M_S</c> or <c>LCK_M_X</c>), writes what the competing
/// writer would in the holder's transaction, then commits. Never a delay.
/// </summary>
/// <remarks>
/// <para>
/// <b>Seeding.</b> Participants are registered through the product and given their Memberships directly
/// (<see cref="WireDatabase.AddMembershipAsync"/>), then made present in the Space with a read: the overlap is the point, and
/// an accept per participant would bury it under its own lock and presence insert. Every request carries an anti-forgery
/// token fetched after sign-in.
/// </para>
/// <para>
/// <b>The holder's statements are the product's shapes</b> under the holder's Space key: the removal's
/// <c>DELETE FROM dbo.[Membership] WHERE Id = @id</c> (<see cref="RemoveUncommittedAsync"/>), and an assignment's
/// <c>UPDATE dbo.[Task] SET AssigneeMembershipId = @m WHERE SpaceId = @s AND Id = @t</c>, whose Membership is of the same
/// Space, so the composite key admits it.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// Race 1, an assignment against a removal of its target: the test holds the Memberships lock Exclusive (as a removal
    /// would), sends the Owner's assignment of a Task to Member M, waits until it is blocked, removes M in the holder's
    /// transaction and commits. The assignment then finds no such Membership: <c>400 invalid-assignee</c>, the Task
    /// unassigned, never a <c>500</c>, and no email to M. An assignment that skipped the lock would have stored M before the
    /// removal and failed it, or met the key as a <c>547</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-21")]
    [Trait("Requirement", "AD-5")]
    [Trait("Requirement", "AR-8")]
    public async Task An_assignment_queued_behind_the_removal_of_its_target_is_400_invalid_assignee_and_sends_nothing()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, targetAddress, fenceAddress) = (RandomAddress(), RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var taskId = Guid.Empty;
        IReadOnlyList<DroppedEmail> toTarget = [];
        WireResponse? queued = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedAssignableSpaceAsync(database, spaceId, "Assign raced " + Suffix(), await AccountIdOfAsync(database, ownerAddress));
            _ = await SignedInAsync(client, targetAddress);
            await wire.AddMembershipAsync(spaceId, await AccountIdOfAsync(database, targetAddress), "member");

            _ = await SignedInAsync(client, fenceAddress);
            await wire.AddMembershipAsync(spaceId, await AccountIdOfAsync(database, fenceAddress), "member");

            var target = (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, targetAddress))).Id;
            var fence = (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, fenceAddress))).Id;
            var (_, fenceTask) = await TaskThroughTheProductAsync(owner, spaceId, "Fence " + Suffix());

            (_, taskId) = await TaskThroughTheProductAsync(owner, spaceId, "Raced " + Suffix());
            await MadePresentAsync(owner, spaceId);
            await owner.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await HoldMembershipsAndRemoveAsync(spaceId, target, async () =>
            {
                var assignment = AssignOverWireAsync(owner, spaceId, taskId, target, Guid.NewGuid().ToString("D"));

                return (assignment, async () => queued = await assignment);
            }, cancellationToken);

            // The fence: an email enqueued after the queued request answered arrives after any email it could have sent.
            Assert.Equal(HttpStatusCode.OK, (await AssignOverWireAsync(owner, spaceId, fenceTask, fence)).StatusCode);
            _ = await mail.WaitForEmailsToAsync(fenceAddress, AssignmentWire.IsAssignmentEmail);
            toTarget = AssignmentWire.AssignmentEmailsTo(await mail.ReadAllAsync(), targetAddress);
        });

        Assert.True(queued?.StatusCode == HttpStatusCode.BadRequest, $"The queued assignment: {Described(queued)}");
        Assert.Equal(ProblemTypes.InvalidAssignee, ProblemType(queued));
        Assert.Null(await wire.AssigneeOfAsync(spaceId, taskId));
        Assert.Empty(toTarget);
    }

    /// <summary>
    /// Race 2, the deciding race for the removal's unassignment: the test holds the Memberships lock Shared (as an in-flight
    /// assignment would), sends the Owner's removal of M, waits until it is blocked, assigns M's Task to M in the holder's
    /// transaction and commits. The removal, admitted only after that commit, unassigns inside its own transaction and then
    /// deletes: <c>204</c>, the Task survives unassigned, no <c>547</c>. A removal without its unassign fails here.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-21")]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AD-5")]
    public async Task A_removal_queued_behind_an_assignment_of_its_target_unassigns_and_answers_204()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, targetAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var taskId = Guid.Empty;
        WireResponse? queued = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedAssignableSpaceAsync(database, spaceId, "Removal raced " + Suffix(), await AccountIdOfAsync(database, ownerAddress));
            _ = await SignedInAsync(client, targetAddress);
            await wire.AddMembershipAsync(spaceId, await AccountIdOfAsync(database, targetAddress), "member");

            var target = (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, targetAddress))).Id;

            (_, taskId) = await TaskThroughTheProductAsync(owner, spaceId, "Assigned in flight " + Suffix());
            await MadePresentAsync(owner, spaceId);
            await owner.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeSharedApplicationLockAsync(holder, held, MembershipsLockOf(spaceId), cancellationToken);

            var removal = RemoveOverWireAsync(owner, spaceId, target, Guid.NewGuid().ToString("D"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, removal, cancellationToken);
            await AssignUncommittedAsync(holder, held, spaceId, taskId, target, cancellationToken);
            await held.CommitAsync(cancellationToken);

            queued = await removal;
        });

        Assert.True(queued?.StatusCode == HttpStatusCode.NoContent, $"The queued removal: {Described(queued)}");
        Assert.Contains(await wire.TasksInAsync(spaceId), row => row.Id == taskId);
        Assert.Null(await wire.AssigneeOfAsync(spaceId, taskId));
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// Race 3, assignments never queue on one another: with the Memberships lock held Shared (as one assignment would), an
    /// assignment of another Task in the same Space answers <c>200</c> before the holder releases. Taken Exclusive, it would
    /// wait.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-21")]
    public async Task Two_assignments_in_one_Space_do_not_wait_on_each_other()
    {
        var database = Available();
        var wire = Wire();
        var (ownerAddress, memberAddress) = (RandomAddress(), RandomAddress());
        var spaceId = Guid.NewGuid();
        var answeredWhileHeld = false;
        WireResponse? answered = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await SignedInAsync(client, ownerAddress);
            await SeedAssignableSpaceAsync(database, spaceId, "Shared " + Suffix(), await AccountIdOfAsync(database, ownerAddress));
            _ = await SignedInAsync(client, memberAddress);
            await wire.AddMembershipAsync(spaceId, await AccountIdOfAsync(database, memberAddress), "member");

            var member = (await wire.MembershipOfAsync(spaceId, await AccountIdOfAsync(database, memberAddress))).Id;
            var (_, taskId) = await TaskThroughTheProductAsync(owner, spaceId, "Not queued " + Suffix());

            await MadePresentAsync(owner, spaceId);
            await owner.FetchAntiforgeryTokenAsync();

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeSharedApplicationLockAsync(holder, held, MembershipsLockOf(spaceId), cancellationToken);

            var assignment = AssignOverWireAsync(owner, spaceId, taskId, member);

            await Tasks.LabelWire.WaitUntilBlockedOrAnsweredAsync(observer, 1, assignment, cancellationToken);

            answeredWhileHeld = assignment.IsCompleted;

            await held.CommitAsync(cancellationToken);

            answered = await assignment;
        });

        Assert.True(answeredWhileHeld, $"The assignment waited on another assignment's Shared hold; it answered {Described(answered)} only after the release.");
        Assert.True(answered?.StatusCode == HttpStatusCode.OK, $"The assignment: {Described(answered)}");
    }

    /// <summary>
    /// Holds the Space's Memberships lock Exclusive on a connection under the Space's key, starts the request
    /// <paramref name="send"/> builds, waits until it is blocked on a lock, removes <paramref name="membershipId"/> in the
    /// holder's transaction, commits, then lets the case read the answer.
    /// </summary>
    private async Task HoldMembershipsAndRemoveAsync(
        Guid spaceId,
        Guid membershipId,
        Func<Task<(Task Racing, Func<Task> Answered)>> send,
        CancellationToken cancellationToken)
    {
        var database = Available();

        await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
        await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
        await using var held = await holder.BeginTransactionAsync(cancellationToken);

        await TakeApplicationLockAsync(holder, held, MembershipsLockOf(spaceId), cancellationToken);

        var (racing, answered) = await send();

        await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
        await RemoveUncommittedAsync(holder, held, membershipId, cancellationToken);
        await held.CommitAsync(cancellationToken);
        await answered();
    }

    /// <summary>
    /// The Memberships lock in the mode an assignment takes it: <c>Shared</c>, compatible with other Shared holders and
    /// incompatible with the Exclusive one a removal, a leave or a Role change takes.
    /// </summary>
    private static async Task TakeSharedApplicationLockAsync(SqlConnection holder, DbTransaction held, string resource, CancellationToken cancellationToken)
    {
        await using DbCommand command = holder.CreateCommand();

        command.Transaction = held;
        command.CommandText = "EXEC sp_getapplock @Resource = @resource, @LockMode = 'Shared', @LockOwner = 'Transaction'";
        command.Parameters.Add(new SqlParameter("@resource", resource));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// The holder's assignment of one Task, uncommitted, under the Space's key: the product's statement shape, naming a
    /// Membership of the same Space so the composite key admits it.
    /// </summary>
    private static async Task AssignUncommittedAsync(SqlConnection holder, DbTransaction held, Guid spaceId, Guid taskId, Guid membershipId, CancellationToken cancellationToken)
    {
        await using DbCommand command = holder.CreateCommand();

        command.Transaction = held;
        command.CommandText = "UPDATE dbo.[Task] SET AssigneeMembershipId = @membership WHERE SpaceId = @space AND Id = @task";
        command.Parameters.Add(new SqlParameter("@membership", membershipId));
        command.Parameters.Add(new SqlParameter("@space", spaceId));
        command.Parameters.Add(new SqlParameter("@task", taskId));

        Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
    }
}
