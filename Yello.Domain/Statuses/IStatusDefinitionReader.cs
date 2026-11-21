namespace Yello.Domain.Statuses;

/// <summary>
/// Reads the Status definitions of the Space the current unit of work is scoped to.
/// </summary>
/// <remarks>
/// <b>It takes no Space, deliberately.</b> The Space is the request's, resolved once by the
/// pipeline (AR-3), and both isolation layers are already narrowed to it by the time any slice
/// runs. A <c>spaceId</c> parameter here would be a second, caller-supplied answer to a question
/// the pipeline has already settled - and the first place a slice could pass the wrong one.
/// </remarks>
public interface IStatusDefinitionReader
{
    /// <summary>
    /// The active Space's default Status definitions, in Space order. A definition a Project owns is never one of them.
    /// </summary>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// The definitions, ascending by position and then by id in the database's order, so a tie comes back
    /// the same way on every request.
    /// </returns>
    Task<IReadOnlyList<StatusDefinition>> ListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// One Project's delta: the definitions it owns and its operations in the delta's order.
    /// </summary>
    /// <remarks>
    /// Whether the Project exists is not this read's to say: a Project that matches nothing has an empty delta, and the
    /// caller has already read the Project.
    /// </remarks>
    /// <param name="projectId">The Project.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The delta.</returns>
    Task<ProjectStatusDelta> DeltaOfAsync(Guid projectId, CancellationToken cancellationToken);
}
