namespace Yello.Contracts.Spaces;

/// <summary>
/// The body of a removal of one of the Space's defaults: where its Tasks go, in every Project that can take one destination
/// and in each Project that cannot.
/// </summary>
/// <remarks>
/// The destinations travel with the removal they map, so no request removes a default without the mapping it needs.
/// </remarks>
/// <param name="DestinationStatusId">
/// Another of the Space's defaults, or <c>null</c> when no Task holds the Status, or before the caller has seen the
/// <see cref="ProblemTypes.StatusDestinationRequired"/> report.
/// </param>
/// <param name="Exceptions">
/// A destination of its own for each Project whose set after the removal lacks the Space-wide destination. Ignored for any
/// other Project.
/// </param>
public sealed record RemoveSpaceStatusRequest(Guid? DestinationStatusId, IReadOnlyList<SpaceStatusDestinationRequest>? Exceptions);
