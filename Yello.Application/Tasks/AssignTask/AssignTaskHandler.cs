using Yello.Application.Memberships;
using Yello.Application.Outbox;
using Yello.Application.Spaces;
using Yello.Domain.Tasks;

namespace Yello.Application.Tasks.AssignTask;

/// <summary>
/// Assigns a Task of the active Space to one of its Memberships, or unassigns it, and tells the new Assignee by email when
/// someone else made the change.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything cross-cutting happened before this ran</b>: the pipeline resolved the Space, checked
/// <c>AssignTask</c> against the matrix and replayed an <c>Idempotency-Key</c> it had already seen, so a replay never
/// reaches this code and never sends a second email. This slice reads no Role and records nothing.
/// </para>
/// <para>
/// <b>Who is told</b>: the new Assignee, only when the Assignee changed to a Membership whose Account is not the actor's. An
/// unassignment, assigning the current Assignee again and assigning oneself send nothing; a reassignment tells the new
/// Assignee only.
/// </para>
/// <para>
/// <b>One unit of work</b>: the assignment and its outbox row commit or roll back together, and the dispatcher sends only
/// after the commit.
/// </para>
/// </remarks>
/// <param name="assignments">Assigns the Task.</param>
/// <param name="tasks">Reads the Labels it carries, for the answer.</param>
/// <param name="memberships">Reads who the email is to and from, and the work it names, inside this Space.</param>
/// <param name="spaceNames">Reads the Space's name, for the email.</param>
/// <param name="links">The link the email carries.</param>
/// <param name="outbox">Enqueues the email.</param>
public sealed class AssignTaskHandler(
    ITaskAssignments assignments,
    ITaskStore tasks,
    IMembershipStore memberships,
    IActiveSpaceNameReader spaceNames,
    AssignmentLinks links,
    IOutbox outbox)
{
    /// <summary>
    /// Writes the Assignee, then enqueues the email only when the Assignee changed to someone other than the actor.
    /// </summary>
    /// <param name="context">The active Space, as the pipeline resolved it on this request.</param>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="membershipId">The Membership, or <c>null</c> to unassign.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened, with the Task and its Labels when it was assigned.</returns>
    public Task<AssignTaskResult> HandleAsync(
        ActiveSpaceContext context,
        Guid taskId,
        Guid? membershipId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return AssignAsync(context.AccountId, taskId, membershipId, cancellationToken);
    }

    private async Task<AssignTaskResult> AssignAsync(Guid actorAccountId, Guid taskId, Guid? membershipId, CancellationToken cancellationToken)
    {
        var assignment = await assignments.AssignAsync(taskId, membershipId, cancellationToken).ConfigureAwait(false);

        if (assignment is not { Outcome: TaskAssignmentOutcome.Assigned, Task: { } task })
        {
            return new AssignTaskResult(assignment.Outcome, null, []);
        }

        if (membershipId is { } assignee && assignee != assignment.Previous)
        {
            await NotifyAsync(actorAccountId, assignee, taskId, cancellationToken).ConfigureAwait(false);
        }

        var labels = await tasks.LabelIdsOfAsync([taskId], cancellationToken).ConfigureAwait(false);

        return new AssignTaskResult(TaskAssignmentOutcome.Assigned, task, labels.GetValueOrDefault(taskId, []));
    }

    private async Task NotifyAsync(Guid actorAccountId, Guid assignee, Guid taskId, CancellationToken cancellationToken)
    {
        // Null only when the Space or the Task went after the statement, which the same unit of work makes unreachable;
        // nothing is sent rather than an email naming something that is not there.
        if (await memberships.AssignmentNoticeAsync(actorAccountId, assignee, taskId, cancellationToken).ConfigureAwait(false) is not { } notice
            || notice.RecipientAccountId == actorAccountId
            || await spaceNames.ReadAsync(cancellationToken).ConfigureAwait(false) is not { } spaceName)
        {
            return;
        }

        var email = AssignmentEmail.Render(notice.AssignerName, spaceName, notice.ProjectName, notice.TaskTitle, links.Home);

        await outbox.EnqueueAsync(
            new OutboxEmail(OutboxKinds.TaskAssigned, notice.RecipientAddress, email.Subject, email.Body),
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// The outcome of <see cref="AssignTaskHandler.HandleAsync"/>.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Task">The Task as stored, when it was assigned.</param>
/// <param name="LabelIds">The Labels it carries, by name then id.</param>
public sealed record AssignTaskResult(TaskAssignmentOutcome Outcome, TaskItem? Task, IReadOnlyList<Guid> LabelIds);
