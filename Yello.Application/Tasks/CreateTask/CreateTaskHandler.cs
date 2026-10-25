using Yello.Application.Spaces;
using Yello.Domain.Statuses;
using Yello.Domain.Tasks;

namespace Yello.Application.Tasks.CreateTask;

/// <summary>
/// Creates a Task in a Project of the active Space, in the first Status of that Project's effective set.
/// FR-19; story 2.2, AC2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything cross-cutting happened before this ran</b> (AR-3): the pipeline resolved the Space,
/// checked <c>ManageTasks</c> against the matrix, replayed an <c>Idempotency-Key</c> it had already seen
/// and consulted the Tasks-per-Project bound. This slice reads the Space off the resolved context - never
/// its Role, never a Space from the request - and counts nothing.
/// </para>
/// <para>
/// <b>"The first Status" is true by construction.</b> The request carries no Status; this slice takes the
/// first of the effective set (AD-16) and nothing else could be chosen.
/// </para>
/// </remarks>
/// <param name="store">Writes the Task.</param>
/// <param name="statuses">Reads the Space's Status definitions.</param>
public sealed class CreateTaskHandler(ITaskStore store, IStatusDefinitionReader statuses)
{
    /// <summary>
    /// Creates the Task.
    /// </summary>
    /// <param name="context">The active Space, as the pipeline resolved it on this request.</param>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="title">The title, already normalised.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The created Task, or <c>null</c> when no Project in the active Space has that id.</returns>
    public Task<TaskItem?> HandleAsync(
        ActiveSpaceContext context,
        Guid projectId,
        string title,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return CreateAsync(context.SpaceId, projectId, title, cancellationToken);
    }

    private async Task<TaskItem?> CreateAsync(Guid spaceId, Guid projectId, string title, CancellationToken cancellationToken)
    {
        var effective = EffectiveStatusSet.Of(await statuses.ListAsync(cancellationToken).ConfigureAwait(false));

        // Every Space is seeded with three Statuses at registration and nothing removes one until Epic 6,
        // whose migration keeps at least one. An empty set is a broken Space, not a request to refuse.
        var first = effective.Count > 0
            ? effective[0]
            : throw new InvalidOperationException("The active Space defines no Status.");

        return await store.CreateAsync(spaceId, projectId, first.Id, title, cancellationToken).ConfigureAwait(false);
    }
}
