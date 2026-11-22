using Yello.Domain.Statuses;

namespace Yello.Application.Tasks;

/// <summary>
/// The one mapping decision a move into another Project makes, shared by the single move and the bulk move so the two
/// forms answer on the same terms.
/// </summary>
/// <remarks>
/// A supplied Status is applied even when the Task's own would have been kept, so a request means the same thing whatever
/// the destination's set is when it arrives.
/// </remarks>
public static class CrossProjectStatus
{
    /// <summary>
    /// Decides the Status the moved Tasks take in the destination.
    /// </summary>
    /// <param name="destination">The destination's effective set, read under its lock.</param>
    /// <param name="sourceStatusId">The Status the Tasks hold now.</param>
    /// <param name="supplied">The Status the request names, or <c>null</c> to keep the Tasks' own.</param>
    /// <returns>The decision, with the Status they would land in.</returns>
    public static CrossProjectStatusDecision Decide(IReadOnlyList<EffectiveStatus> destination, Guid sourceStatusId, Guid? supplied)
    {
        if (supplied is { } named)
        {
            return EffectiveStatusSet.Contains(destination, named)
                ? new CrossProjectStatusDecision(CrossProjectStatusOutcome.Lands, named)
                : new CrossProjectStatusDecision(CrossProjectStatusOutcome.Invalid, named);
        }

        return EffectiveStatusSet.Contains(destination, sourceStatusId)
            ? new CrossProjectStatusDecision(CrossProjectStatusOutcome.Lands, sourceStatusId)
            : new CrossProjectStatusDecision(CrossProjectStatusOutcome.Required, sourceStatusId);
    }
}

/// <summary>
/// What <see cref="CrossProjectStatus.Decide"/> decided.
/// </summary>
/// <param name="Outcome">Whether the Tasks can land, and if not, why.</param>
/// <param name="Landing">The Status they land in when <see cref="Outcome"/> is <see cref="CrossProjectStatusOutcome.Lands"/>.</param>
public readonly record struct CrossProjectStatusDecision(CrossProjectStatusOutcome Outcome, Guid Landing);

/// <summary>
/// The three ways the mapping decision ends.
/// </summary>
public enum CrossProjectStatusOutcome
{
    /// <summary>
    /// The destination holds the Status supplied, or the Tasks' own when none was.
    /// </summary>
    Lands,

    /// <summary>
    /// A Status was supplied and the destination does not hold it.
    /// </summary>
    Invalid,

    /// <summary>
    /// No Status was supplied and the destination lacks the Tasks' own.
    /// </summary>
    Required,
}
