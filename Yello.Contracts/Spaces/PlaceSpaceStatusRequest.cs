namespace Yello.Contracts.Spaces;

/// <summary>
/// The body of a place of one of the Space's default Statuses: the default it goes after, and nothing else.
/// </summary>
/// <param name="AfterStatusId">Another default of the Space, or <c>null</c> to place it first.</param>
public sealed record PlaceSpaceStatusRequest(Guid? AfterStatusId);
