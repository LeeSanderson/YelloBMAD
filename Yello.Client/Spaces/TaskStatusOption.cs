namespace Yello.Client.Spaces;

/// <summary>
/// One Status the Task detail panel's picker offers: a column of the Board's own server answer, so the
/// picker offers exactly the Project's effective set (UX-DR11; story 2.3).
/// </summary>
/// <param name="Id">The Status's id.</param>
/// <param name="Name">Its name.</param>
public sealed record TaskStatusOption(Guid Id, string Name);
