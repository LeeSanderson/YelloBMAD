namespace Yello.Contracts.Spaces;

/// <summary>
/// One Project in a Space-level removal's report: a Project of the request's Space holding the Status with Tasks in it.
/// </summary>
/// <param name="ProjectId">The Project.</param>
/// <param name="ProjectName">Its name.</param>
/// <param name="TaskCount">How many of its Tasks hold the Status.</param>
/// <param name="Destinations">Its own set after the removal, in Board order: what its destination picker offers.</param>
public sealed record SpaceStatusOccupancyResponse(Guid ProjectId, string ProjectName, int TaskCount, IReadOnlyList<StatusChoiceResponse> Destinations);

/// <summary>
/// One Status a destination picker offers.
/// </summary>
/// <param name="Id">The Status.</param>
/// <param name="Name">The Project's own name for it.</param>
public sealed record StatusChoiceResponse(Guid Id, string Name);
