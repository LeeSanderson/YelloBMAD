namespace Yello.Application.Outbox;

/// <summary>
/// One email, rendered in full at enqueue: the outbox row holds exactly this, so the dispatcher never reads Space data.
/// AD-10 / AR-33; story 4.1 (Lee's Q6).
/// </summary>
/// <param name="Kind">The message kind's stable code, such as <see cref="OutboxKinds.InvitationIssued"/>.</param>
/// <param name="Recipient">The one recipient's address.</param>
/// <param name="Subject">The rendered subject.</param>
/// <param name="Body">The rendered plain-text body.</param>
public sealed record OutboxEmail(string Kind, string Recipient, string Subject, string Body);

/// <summary>
/// The kinds of message the outbox carries. A general mechanism keyed by kind.
/// </summary>
public static class OutboxKinds
{
    /// <summary>
    /// An Invitation was issued (story 4.1, FR-39).
    /// </summary>
    public static string InvitationIssued => "invitation-issued";

    /// <summary>
    /// A Task was assigned to someone other than the Account that assigned it.
    /// </summary>
    public static string TaskAssigned => "task-assigned";

    /// <summary>
    /// An Owner offered ownership of a Space to one of its Memberships.
    /// </summary>
    public static string OwnershipOffered => "ownership-offered";
}
