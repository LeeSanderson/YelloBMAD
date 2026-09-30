namespace Yello.Contracts.Spaces;

/// <summary>
/// One Status, as the Board reads it.
/// </summary>
/// <remarks>
/// No <c>SpaceId</c>: the caller named the Space in the path, and echoing it back per row would be
/// a second place for the two to disagree.
/// </remarks>
/// <param name="Id">The Status's identity, stable across a rename.</param>
/// <param name="Name">The Status's name.</param>
/// <param name="Position">Where it sits in the Board's order, ascending.</param>
public sealed record StatusResponse(Guid Id, string Name, int Position);
