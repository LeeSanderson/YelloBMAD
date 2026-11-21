using Yello.Domain.Statuses;

namespace Yello.Application.Statuses;

/// <summary>
/// A Project's effective Status set as every slice reads it: the Space's defaults and that Project's delta, through
/// <see cref="EffectiveStatusSet"/>.
/// </summary>
/// <remarks>
/// <b>Read on every call, never kept.</b> A write reads the set again under the Project's lock, so nothing outlives the
/// statement that read it - not even within the request.
/// </remarks>
public static class ProjectStatuses
{
    /// <summary>
    /// Reads one Project's effective set.
    /// </summary>
    /// <param name="statuses">Reads the active Space's definitions and deltas.</param>
    /// <param name="projectId">The Project, already known to be the active Space's.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The effective set, in Board order.</returns>
    public static Task<IReadOnlyList<EffectiveStatus>> EffectiveOfAsync(
        this IStatusDefinitionReader statuses,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        return ReadAsync(statuses, projectId, cancellationToken);
    }

    private static async Task<IReadOnlyList<EffectiveStatus>> ReadAsync(IStatusDefinitionReader statuses, Guid projectId, CancellationToken cancellationToken)
    {
        var defaults = await statuses.ListAsync(cancellationToken).ConfigureAwait(false);
        var delta = await statuses.DeltaOfAsync(projectId, cancellationToken).ConfigureAwait(false);

        return EffectiveStatusSet.Of(defaults, delta);
    }
}
