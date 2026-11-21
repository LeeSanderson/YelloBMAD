using Yello.Application.Spaces;
using Yello.Domain;
using Yello.Domain.Statuses;

namespace Yello.Application.Statuses.AddSpaceStatus;

/// <summary>
/// Adds a default Status to the Space, last in Space order or right after another default.
/// </summary>
/// <remarks>
/// <b>No Project row is written.</b> Every Project's set is derived from the defaults, so the new default takes its Space
/// slot in each of them - before any Status a Project added and has not placed - on its next read.
/// </remarks>
/// <param name="writer">Changes the defaults under the Space-level Status lock.</param>
/// <param name="identifiers">Produces the new default's id.</param>
/// <param name="clock">Stamps it.</param>
public sealed class AddSpaceStatusHandler(SpaceDefaultStatusWriter writer, IIdentifierGenerator identifiers, TimeProvider clock)
{
    /// <summary>
    /// Adds the default.
    /// </summary>
    /// <param name="context">The active Space, as the pipeline resolved it on this request.</param>
    /// <param name="name">The name, already normalised.</param>
    /// <param name="afterStatusId">The default it goes after, or <c>null</c> for last.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened, and the defaults when the Status was added.</returns>
    public Task<SpaceStatusWriteResult> HandleAsync(ActiveSpaceContext context, string name, Guid? afterStatusId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var added = new StatusDefinition
        {
            Id = identifiers.Next(),
            SpaceId = context.SpaceId,
            Name = name,
            Position = 0,
            CreatedAt = clock.GetUtcNow(),
        };

        return writer.WriteAsync((defaults, deltas) => SpaceDefaultStatusPlan.Add(defaults, deltas, added, afterStatusId), cancellationToken);
    }
}
