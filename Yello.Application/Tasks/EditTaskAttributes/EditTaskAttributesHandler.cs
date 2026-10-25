using Yello.Domain.Statuses;
using Yello.Domain.Tasks;

namespace Yello.Application.Tasks.EditTaskAttributes;

/// <summary>
/// Edits a Task's title, Status and due date in the active Space. FR-20 ("Edit Task attributes"); story 2.3,
/// AC1, AC4, AC9.
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

        return edited is null ? EditTaskAttributesResult.NotFound : EditTaskAttributesResult.Edited(edited);
    }
}

/// <summary>
/// The outcome of <see cref="EditTaskAttributesHandler.HandleAsync"/>.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Task">The Task as stored, when <see cref="Outcome"/> is <see cref="EditTaskAttributesOutcome.Edited"/>.</param>
public sealed record EditTaskAttributesResult(EditTaskAttributesOutcome Outcome, TaskItem? Task)
{
    /// <summary>
    /// No Task in the active Space has the id.
    /// </summary>
    public static EditTaskAttributesResult NotFound { get; } = new(EditTaskAttributesOutcome.NotFound, null);

    /// <summary>
    /// The Status is not in the Task's Project's effective set; nothing was written.
    /// </summary>
    public static EditTaskAttributesResult StatusOutsideTheEffectiveSet { get; } = new(EditTaskAttributesOutcome.StatusOutsideTheEffectiveSet, null);

    /// <summary>
    /// The edit was applied.
    /// </summary>
    /// <param name="task">The Task as stored.</param>
    /// <returns>The result.</returns>
    public static EditTaskAttributesResult Edited(TaskItem task) => new(EditTaskAttributesOutcome.Edited, task);
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
}
