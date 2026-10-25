namespace Yello.Contracts.Tasks;

/// <summary>
/// One Task, as its creation, its read and its edit answer it and its Board column lists it.
/// </summary>
/// <remarks>
/// <para>
/// <b>No <c>SpaceId</c></b>, as <c>StatusResponse</c>'s remarks explain: the Space is the route's.
/// </para>
/// <para>
/// <b><see cref="DueDate"/> was appended by story 2.3</b> - adding a field is legal in v1 (AD-19, FR-37);
/// removing or retyping one is not.
/// </para>
/// </remarks>
/// <param name="Id">Its id.</param>
/// <param name="Title">Its title.</param>
/// <param name="StatusId">Its Status.</param>
/// <param name="PositionKey">Where it sits in its column (AD-15): readable here, never writable.</param>
/// <param name="CreatedAt">When it was created, in UTC.</param>
/// <param name="DueDate">When it is due, in UTC and ISO 8601 with offset on the wire, or <c>null</c> for none.</param>
public sealed record TaskResponse(
    Guid Id,
    string Title,
    Guid StatusId,
    string PositionKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DueDate = null);
