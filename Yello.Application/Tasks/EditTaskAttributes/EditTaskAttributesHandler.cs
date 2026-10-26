using Yello.Domain.Statuses;
using Yello.Domain.Tasks;

namespace Yello.Application.Tasks.EditTaskAttributes;

/// <summary>
/// Edits a Task's title, Status, due date and Labels in the active Space. FR-20 ("Edit Task attributes"), FR-22;
/// story 2.3, AC1, AC4, AC9; story 2.4, AC2, AC7, AC10.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything cross-cutting happened before this ran</b> (AR-3): the pipeline resolved the Space, checked
/// <c>ManageTasks</c> and replayed an <c>Idempotency-Key</c> it had already seen. This slice reads no Role,
/// counts nothing and consults no bound - an edit adds no Task.
/// </para>
/// <para>
/// <b>The Status is checked before anything is written</b> against the effective set of the Task's own Project
/// (AD-17), so another Space's Status id is refused as a validation <c>400</c> rather than reaching the
/// composite foreign key <c>(SpaceId, StatusId)</c>, which would answer it as a <c>500</c>.
/// </para>
/// <para>
/// <b>Applying Labels is a Task edit</b> (story 2.4, open question 1 (a)): the same capability, the same
/// idempotency, and the whole set replaces the Task's own. Which of the Labels exist is the store's to decide under
/// its lock (AC10); a set with any missing - unknown, another Space's, or deleted under the request - comes back as
/// <see cref="EditTaskAttributesOutcome.LabelsInvalid"/>, and the endpoint's <c>400</c> rolls back whatever the
/// same edit had already written.
/// </para>
/// </remarks>
/// <param name="store">Reads and writes the Task.</param>
/// <param name="statuses">Reads the Space's Status definitions.</param>
public sealed class EditTaskAttributesHandler(ITaskStore store, IStatusDefinitionReader statuses)
{
    /// <summary>
    /// Applies the edit.
    /// </summary>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="edit">What changes; the title and due date already normalised.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened, and the Task as stored when it was edited.</returns>
    public Task<EditTaskAttributesResult> HandleAsync(Guid taskId, TaskEdit edit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(edit);

        return EditAsync(taskId, edit, cancellationToken);
    }

    private async Task<EditTaskAttributesResult> EditAsync(Guid taskId, TaskEdit edit, CancellationToken cancellationToken)
    {
        var task = await store.FindAsync(taskId, cancellationToken).ConfigureAwait(false);

        if (task is null)
        {
            return EditTaskAttributesResult.NotFound;
        }

        if (edit.StatusId is { } statusId)
        {
            // Until story 6.1 adds a Project's own delta, every Project's effective set is its Space's
            // definitions; EffectiveStatusSet is where that delta will land.
            var effective = EffectiveStatusSet.Of(await statuses.ListAsync(cancellationToken).ConfigureAwait(false));

            if (!EffectiveStatusSet.Contains(effective, statusId))
            {
                return EditTaskAttributesResult.StatusOutsideTheEffectiveSet;
            }
        }

        var edited = await store.EditAsync(taskId, edit, cancellationToken).ConfigureAwait(false);

        if (edited is null)
        {
            return EditTaskAttributesResult.NotFound;
        }

        if (edit.ChangesLabels && !await store.ReplaceLabelsAsync(taskId, edit.LabelIds ?? [], cancellationToken).ConfigureAwait(false))
        {
            return EditTaskAttributesResult.LabelsInvalid;
        }

        var labels = await store.LabelIdsOfAsync([taskId], cancellationToken).ConfigureAwait(false);

        return EditTaskAttributesResult.Edited(edited, labels.GetValueOrDefault(taskId, []));
    }
}

/// <summary>
/// The outcome of <see cref="EditTaskAttributesHandler.HandleAsync"/>.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Task">The Task as stored, when <see cref="Outcome"/> is <see cref="EditTaskAttributesOutcome.Edited"/>.</param>
/// <param name="LabelIds">The Labels the Task carries afterwards, by name then id; empty unless edited.</param>
public sealed record EditTaskAttributesResult(EditTaskAttributesOutcome Outcome, TaskItem? Task, IReadOnlyList<Guid> LabelIds)
{
    /// <summary>
    /// No Task in the active Space has the id.
    /// </summary>
    public static EditTaskAttributesResult NotFound { get; } = new(EditTaskAttributesOutcome.NotFound, null, []);

    /// <summary>
    /// The Status is not in the Task's Project's effective set; nothing was written.
    /// </summary>
    public static EditTaskAttributesResult StatusOutsideTheEffectiveSet { get; } = new(EditTaskAttributesOutcome.StatusOutsideTheEffectiveSet, null, []);

    /// <summary>
    /// A Label of the submitted set is not visible in the active Space; the edit must not be committed.
    /// </summary>
    public static EditTaskAttributesResult LabelsInvalid { get; } = new(EditTaskAttributesOutcome.LabelsInvalid, null, []);

    /// <summary>
    /// The edit was applied.
    /// </summary>
    /// <param name="task">The Task as stored.</param>
    /// <param name="labelIds">The Labels it carries, by name then id.</param>
    /// <returns>The result.</returns>
    public static EditTaskAttributesResult Edited(TaskItem task, IReadOnlyList<Guid> labelIds) => new(EditTaskAttributesOutcome.Edited, task, labelIds);
}

/// <summary>
/// What an edit came to.
/// </summary>
public enum EditTaskAttributesOutcome
{
    /// <summary>
    /// The edit was applied.
    /// </summary>
    Edited,

    /// <summary>
    /// No Task in the active Space has the id.
    /// </summary>
    NotFound,

    /// <summary>
    /// The Status is not in the Task's Project's effective set.
    /// </summary>
    StatusOutsideTheEffectiveSet,

    /// <summary>
    /// A Label of the submitted set is unknown, another Space's, or was deleted under the edit (story 2.4, AC10).
    /// </summary>
    LabelsInvalid,
}
