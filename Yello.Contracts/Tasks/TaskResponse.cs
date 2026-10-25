namespace Yello.Contracts.Tasks;

/// <summary>
/// One Task, as its creation answers it and its Board column lists it.
/// </summary>
/// <remarks>
/// <b>No <c>SpaceId</c></b>, as <c>StatusResponse</c>'s remarks explain: the Space is the route's.
/// </remarks>
/// <param name="Id">Its id.</param>
/// <param name="Title">Its title.</param>
/// <param name="StatusId">Its Status.</param>
/// <param name="PositionKey">Where it sits in its column (AD-15): readable here, never writable.</param>
/// <param name="CreatedAt">When it was created, in UTC.</param>
public sealed record TaskResponse(Guid Id, string Title, Guid StatusId, string PositionKey, DateTimeOffset CreatedAt);
