using Xunit;
using Yello.Application.Memberships;
using Yello.Application.Outbox;
using Yello.Application.Spaces;
using Yello.Application.Tasks.AssignTask;
using Yello.Domain.Memberships;
using Yello.Domain.Tasks;

namespace Yello.Tests.Slices.Tasks.AssignTask;

/// <summary>
/// The assignment slice tells the new Assignee by email exactly when the Assignee changed to a Membership whose Account is
/// not the actor's - never on an unassignment, a no-op, a self-assignment or a refusal - and the email names the assigner,
/// the Space, the Project and the Task, links to the client with no id, and goes to the Assignee's address alone.
/// </summary>
/// <remarks>
/// No database: the wire cases in <c>ActiveSpaceEndpointTests.AssignTask</c> hold the statement, the lock, the outbox row,
/// the send record and the drop.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "FR-40")]
[Trait("Assumption", "PRD-12-10")]
public sealed class AssignTaskHandlerTests
{
    private static readonly Uri Origin = new("https://client.yello.test/somewhere?x=1");

    private static readonly Guid Actor = Guid.NewGuid();

    private static readonly Guid Recipient = Guid.NewGuid();

    /// <summary>
    /// A change to another Account's Membership enqueues one <c>task-assigned</c> email to that Account's address, naming the
    /// assigner, the Space, the Project and the Task, and linking to the client's origin with nothing after it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_change_to_another_Accounts_Membership_enqueues_one_email_to_it()
    {
        var membership = Guid.NewGuid();
        var (handler, outbox) = Handler(TaskAssignment.Assigned(TaskOf(membership), previous: null), Recipient);

        var result = await handler.HandleAsync(Context(), Guid.NewGuid(), membership, TestContext.Current.CancellationToken);

        Assert.Equal(TaskAssignmentOutcome.Assigned, result.Outcome);

        var email = Assert.Single(outbox.Enqueued);

        Assert.Equal(OutboxKinds.TaskAssigned, email.Kind);
        Assert.Equal("recipient@example.test", email.Recipient);
        Assert.Equal("Sam Lee assigned you a Task in Harbour", email.Subject);
        Assert.Contains("Sam Lee assigned you \"Paint the bollards\" in the Project Quayside in the Space Harbour on Yello.", email.Body, StringComparison.Ordinal);
        Assert.Contains("https://client.yello.test/\n", email.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("somewhere", email.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("!", email.Subject + email.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Nothing is enqueued for an unassignment, for assigning the current Assignee again, for assigning oneself, or for a
    /// refused assignment. Anchor: the same handler with another Account as the recipient does enqueue.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task No_email_for_an_unassignment_a_no_op_a_self_assignment_or_a_refusal()
    {
        var membership = Guid.NewGuid();
        var token = TestContext.Current.CancellationToken;

        var (unassign, unassignOutbox) = Handler(TaskAssignment.Assigned(TaskOf(null), previous: membership), Recipient);
        var (noOp, noOpOutbox) = Handler(TaskAssignment.Assigned(TaskOf(membership), previous: membership), Recipient);
        var (self, selfOutbox) = Handler(TaskAssignment.Assigned(TaskOf(membership), previous: null), Actor);
        var (refused, refusedOutbox) = Handler(TaskAssignment.InvalidAssignee, Recipient);
        var (anchor, anchorOutbox) = Handler(TaskAssignment.Assigned(TaskOf(membership), previous: Guid.NewGuid()), Recipient);

        _ = await unassign.HandleAsync(Context(), Guid.NewGuid(), null, token);
        _ = await noOp.HandleAsync(Context(), Guid.NewGuid(), membership, token);
        _ = await self.HandleAsync(Context(), Guid.NewGuid(), membership, token);
        var refusal = await refused.HandleAsync(Context(), Guid.NewGuid(), membership, token);
        _ = await anchor.HandleAsync(Context(), Guid.NewGuid(), membership, token);

        Assert.Empty(unassignOutbox.Enqueued);
        Assert.Empty(noOpOutbox.Enqueued);
        Assert.Empty(selfOutbox.Enqueued);
        Assert.Empty(refusedOutbox.Enqueued);
        Assert.Equal(TaskAssignmentOutcome.InvalidAssignee, refusal.Outcome);
        Assert.Null(refusal.Task);
        Assert.Single(anchorOutbox.Enqueued);
    }

    private static ActiveSpaceContext Context() => new(Actor, Guid.NewGuid(), Role.Member);

    private static TaskItem TaskOf(Guid? assignee) => new()
    {
        Id = Guid.NewGuid(),
        SpaceId = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        StatusId = Guid.NewGuid(),
        Title = "Paint the bollards",
        PositionKey = "a",
        CreatedAt = DateTimeOffset.UnixEpoch,
        AssigneeMembershipId = assignee,
    };

    private static (AssignTaskHandler Handler, RecordingOutbox Outbox) Handler(TaskAssignment decided, Guid recipientAccountId)
    {
        var outbox = new RecordingOutbox();
        var notice = new AssignmentNotice(recipientAccountId, "recipient@example.test", "Sam Lee", "Quayside", "Paint the bollards");

        return (new AssignTaskHandler(new FixedAssignments(decided), new NoLabels(), new FixedNotice(notice), new FixedName("Harbour"), new AssignmentLinks(Origin), outbox), outbox);
    }

    private sealed class RecordingOutbox : IOutbox
    {
        public List<OutboxEmail> Enqueued { get; } = [];

        public Task EnqueueAsync(OutboxEmail email, CancellationToken cancellationToken)
        {
            Enqueued.Add(email);

            return Task.CompletedTask;
        }
    }

    private sealed class FixedAssignments(TaskAssignment decided) : ITaskAssignments
    {
        public Task<TaskAssignment> AssignAsync(Guid taskId, Guid? membershipId, CancellationToken cancellationToken) => Task.FromResult(decided);
    }

    private sealed class FixedName(string? name) : IActiveSpaceNameReader
    {
        public Task<string?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(name);
    }

    private sealed class NoLabels : ITaskStore
    {
        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LabelIdsOfAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(new Dictionary<Guid, IReadOnlyList<Guid>>());

        public Task<TaskItem?> CreateAsync(Guid spaceId, Guid projectId, Guid statusId, string title, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The assignment slice creates nothing.");

        public Task<TaskItem?> FindAsync(Guid taskId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The assignment slice reads the Task from the statement.");

        public Task<TaskItem?> EditAsync(Guid taskId, TaskEdit edit, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The assignment slice edits nothing.");

        public Task<bool> ReplaceLabelsAsync(Guid taskId, IReadOnlyList<Guid> labelIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The assignment slice applies no Label.");

        public Task<TaskDeletion> DeleteAsync(Guid taskId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The assignment slice deletes nothing.");

        public Task<TaskMove> MoveAsync(Guid taskId, Guid statusId, Guid? afterTaskId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The assignment slice moves nothing.");
    }

    private sealed class FixedNotice(AssignmentNotice notice) : IMembershipStore
    {
        public Task<AssignmentNotice?> AssignmentNoticeAsync(Guid actorAccountId, Guid membershipId, Guid taskId, CancellationToken cancellationToken) =>
            Task.FromResult<AssignmentNotice?>(notice);

        public Task<IReadOnlyList<AssigneeCandidate>> ListAssigneesAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("The assignment slice lists no candidates.");

        public Task<(IReadOnlyList<ListedMembership> Rows, int Total)> ListAsync(Guid callerAccountId, MembershipCursor? after, int take, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The assignment slice lists no Memberships.");

        public Task<RoleChangeResult> ChangeRoleAsync(Guid actorAccountId, Guid membershipId, Role proposed, RoleChangeAuthority authority, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The assignment slice changes no Role.");

        public Task<ListedMembership?> ReadAsync(Guid callerAccountId, Guid membershipId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The assignment slice reads no Membership row.");

        public Task<RemovalOutcome> RemoveAsync(Guid actorAccountId, Guid membershipId, RemovalAuthority authority, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The assignment slice removes nothing.");

        public Task<RemovalOutcome> LeaveAsync(Guid actorAccountId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The assignment slice leaves nothing.");
    }
}
