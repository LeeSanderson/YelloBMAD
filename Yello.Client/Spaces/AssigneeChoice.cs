namespace Yello.Client.Spaces;

/// <summary>
/// One Assignee candidate as the client shows it: its label - the display name, with a Membership-scoped discriminator
/// only when another candidate's name looks the same - and its initials.
/// </summary>
/// <param name="MembershipId">The Membership an assignment names.</param>
/// <param name="Label">How the person is named, never an address.</param>
/// <param name="Initials">The avatar's initials, from the display name alone.</param>
public sealed record AssigneeChoice(Guid MembershipId, string Label, string Initials);
