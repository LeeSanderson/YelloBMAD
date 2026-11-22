using Yello.Domain.Tasks;

namespace Yello.Application.Tasks.ViewTaskDescription;

/// <summary>
/// Reads one Task's description in the active Space: the projection as text, and the encoded state an editor bootstraps
/// its replica from.
/// </summary>
/// <remarks>
/// The text is the projection column, never computed from the log here, so this read and the List View read the same
/// representation.
/// </remarks>
/// <param name="log">The description log.</param>
public sealed class ViewTaskDescriptionHandler(ITaskDescriptionLog log)
{
    /// <summary>
    /// Reads the description.
    /// </summary>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The description, or <c>null</c> when no Task in the active Space has that id.</returns>
    public Task<DescriptionRead?> HandleAsync(Guid taskId, CancellationToken cancellationToken) =>
        log.ReadAsync(taskId, cancellationToken);
}
