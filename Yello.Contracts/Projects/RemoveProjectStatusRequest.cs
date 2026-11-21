namespace Yello.Contracts.Projects;

/// <summary>
/// The body of a removal: where the removed Status's Tasks go, and nothing else.
/// </summary>
/// <remarks>
/// The destination travels with the removal it maps, so no request removes a Status without the mapping it needs.
/// </remarks>
/// <param name="DestinationStatusId">
/// A Status of the set the removal leaves, or <c>null</c> when the Status holds no Task.
/// </param>
public sealed record RemoveProjectStatusRequest(Guid? DestinationStatusId);
