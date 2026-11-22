namespace Yello.Contracts.Spaces;

/// <summary>
/// One Project's own destination in a <see cref="RemoveSpaceStatusRequest"/>.
/// </summary>
/// <param name="ProjectId">The Project, named at most once per request.</param>
/// <param name="DestinationStatusId">A Status of that Project's set after the removal: a default it holds or one it added.</param>
public sealed record SpaceStatusDestinationRequest(Guid ProjectId, Guid DestinationStatusId);
