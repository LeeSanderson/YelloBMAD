namespace Yello.Contracts.Labels;

/// <summary>
/// The body of a Label definition: its name and its colour, and nothing else. The Space is the route's. FR-22;
/// story 2.4.
/// </summary>
/// <param name="Name">The name, 1-50 characters once trimmed, unique in the Space ignoring case.</param>
/// <param name="Colour">One of the palette's keys, such as <c>rust</c>.</param>
public sealed record CreateLabelRequest(string? Name, string? Colour);
