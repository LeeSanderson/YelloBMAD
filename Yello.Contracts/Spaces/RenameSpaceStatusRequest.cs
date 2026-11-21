namespace Yello.Contracts.Spaces;

/// <summary>
/// The body of a rename of one of the Space's default Statuses.
/// </summary>
/// <param name="Name">The new name, 1-100 characters once trimmed, unique among the defaults ignoring case.</param>
/// <param name="ReplaceProjectNames">
/// The one decision for every Project that gave the Status a name of its own: <c>true</c> replaces their names with the new
/// one, <c>false</c> keeps them. Absent until the caller has seen those Projects in a
/// <see cref="ProblemTypes.StatusRenameConflict"/> refusal; ignored when no Project conflicts.
/// </param>
public sealed record RenameSpaceStatusRequest(string? Name, bool? ReplaceProjectNames);
