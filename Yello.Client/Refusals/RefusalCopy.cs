using System.Collections.Frozen;
using Yello.Contracts;

namespace Yello.Client.Refusals;

/// <summary>
/// Which of the client's own strings a refusal is rendered with. AC9; UX-DR32, UX-DR35.
/// </summary>
/// <remarks>
/// <para>
/// <b>The <c>type</c> decides; prose never does.</b> The server's <c>title</c> and <c>detail</c> are
/// not even deserialised (<see cref="ProblemResponse"/>). A <c>403</c> is rendered from a resource
/// keyed on its capability code, a bound refusal from one keyed on its bound code with the limit as
/// the only argument, the two idempotency refusals from one each (story 2.2), and everything else
/// from the one not-available heading.
/// </para>
/// <para>
/// <b>No branch on which not-found occurred.</b> A Space the Account holds no Membership in, a
/// Space that does not exist, an unmatched address and an unrecognised problem all resolve to the
/// same key, because the client has nothing to tell them apart with and must not try.
/// </para>
/// </remarks>
public static class RefusalCopy
{
    /// <summary>
    /// The not-available heading's resource key.
    /// </summary>
    public static string NotAvailableKey => "NotAvailable";

    /// <summary>
    /// The resource key for a <c>403</c> whose capability code the client does not recognise.
    /// </summary>
    public static string RoleDoesNotPermitKey => "RoleDoesNotPermit";

    /// <summary>
    /// The refusal page's one link's resource key.
    /// </summary>
    public static string RouteBackKey => "RefusalRouteBack";

    /// <summary>
    /// The resource key for <c>409 idempotency-key-in-flight</c>: the request was already sent and its
    /// first use is still finishing (story 2.2, AC13).
    /// </summary>
    public static string IdempotencyKeyInFlightKey => "IdempotencyKeyInFlight";

    /// <summary>
    /// The resource key for <c>422 idempotency-key-reused</c>: the key was already used for a different
    /// request (story 2.2, AC13).
    /// </summary>
    public static string IdempotencyKeyReusedKey => "IdempotencyKeyReused";

    /// <summary>
    /// The resource key for <c>400 invalid-task-title</c>, met by a Task creation and a Task edit.
    /// </summary>
    public static string InvalidTaskTitleKey => "TaskTitleInvalid";

    /// <summary>
    /// The resource key for <c>400 invalid-task-status</c>: a Task edit's Status is not one of its Project's
    /// (story 2.3).
    /// </summary>
    public static string InvalidTaskStatusKey => "InvalidTaskStatus";

    /// <summary>
    /// The resource key for <c>400 invalid-due-date</c> (story 2.3).
    /// </summary>
    public static string InvalidDueDateKey => "InvalidDueDate";

    /// <summary>
    /// The resource key for <c>400 invalid-task-edit</c>: an edit body the server could not read (story 2.3).
    /// </summary>
    public static string InvalidTaskEditKey => "InvalidTaskEdit";

    /// <summary>
    /// The copy key for <c>400 invalid-label-name</c> (story 2.4): a name outside 1-50 characters.
    /// </summary>
    public static string InvalidLabelNameKey => "LabelNameInvalid";

    /// <summary>
    /// The copy key for <c>400 invalid-label-colour</c> (story 2.4): a colour outside the palette.
    /// </summary>
    public static string InvalidLabelColourKey => "InvalidLabelColour";

    /// <summary>
    /// The copy key for <c>409 label-name-taken</c> (story 2.4): the Space already holds that name.
    /// </summary>
    public static string LabelNameTakenKey => "LabelNameTaken";

    /// <summary>
    /// The copy key for <c>409 label-in-use</c> (story 2.4, AC3): the Label is on Tasks and was not deleted.
    /// </summary>
    public static string LabelInUseKey => "LabelInUse";

    /// <summary>
    /// The copy key for <c>400 invalid-task-labels</c> (story 2.4): a Label of the set is not the Space's.
    /// </summary>
    public static string InvalidTaskLabelsKey => "InvalidTaskLabels";

    /// <summary>
    /// The copy key for <c>400 invalid-assignee</c>: the person chosen is not a Member of this Space any more.
    /// </summary>
    public static string InvalidAssigneeKey => "invalid-assignee";

    /// <summary>
    /// The copy key for <c>409 task-position-unavailable</c> (story 2.6, open question 3 (a)): the position chosen is no
    /// longer one.
    /// </summary>
    public static string TaskPositionUnavailableKey => "TaskPositionUnavailable";

    /// <summary>
    /// The copy key for <c>409 task-position-full</c> (story 2.9, AC18, open question 11 (a)): no key fits at the position
    /// chosen, so another must be chosen.
    /// </summary>
    public static string TaskPositionFullKey => "TaskPositionFull";

    /// <summary>
    /// The copy key for <c>400 invalid-list-query</c> (story 2.10): a List View query the server could not read.
    /// </summary>
    public static string InvalidListQueryKey => "ListQueryInvalid";

    /// <summary>
    /// The copy key for <c>400 invalid-list-cursor</c> (story 2.10): a cursor the server did not issue for the sort.
    /// </summary>
    public static string InvalidListCursorKey => "ListCursorInvalid";

    /// <summary>
    /// The copy key for <c>400 invalid-space-name</c> (story 3.1, open question 6 (a)): a Space name outside 1-200
    /// characters, met by a Space creation and a Space rename.
    /// </summary>
    public static string InvalidSpaceNameKey => "SpaceNameInvalid";

    /// <summary>
    /// An implausible email address on an Invitation (story 4.1, AC3), beside the email field - the client's own check
    /// says the same sentence before anything is sent.
    /// </summary>
    public static string InvalidEmailAddressKey => "invalid-email-address";

    /// <summary>
    /// The Role offered is not one an Invitation may carry (story 4.1, AC2), beside the Role group.
    /// </summary>
    public static string RoleNotOfferableKey => "role-not-offerable";

    /// <summary>
    /// The address already holds a Membership in this Space (story 4.1, AC4), beside the email field.
    /// </summary>
    public static string AlreadyAMemberKey => "already-a-member";

    /// <summary>
    /// The address already has a pending Invitation to this Space (story 4.1, AC13), beside the email field.
    /// </summary>
    public static string InvitationPendingKey => "invitation-pending";

    /// <summary>
    /// The Invitation a revoke named is no longer pending - accepted, revoked or expired (story 4.2, AC4, Q2 (a)); said in
    /// the Invitation list after it is read again. Names no address.
    /// </summary>
    public static string InvitationNotPendingKey => "invitation-not-pending";

    /// <summary>
    /// Story 4.3 (AC6, Lee's Q6 (a)): the uniform answer for a revoked, accepted, expired, lapsed or unrecognised Invitation.
    /// </summary>
    public static string InvitationNotValidKey => "invitation-not-valid";

    /// <summary>
    /// Story 4.3 (AC7, open question 13 (a)): the accept by a signed-in Account the Invitation does not name.
    /// </summary>
    public static string InvitationForAnotherAccountKey => "invitation-for-another-account";

    /// <summary>
    /// The copy key for <c>owner-role-fixed</c>: a change to the Owner's own row (story 4.4, Q10 (a)).
    /// </summary>
    public static string OwnerRoleFixedKey => "owner-role-fixed";

    /// <summary>
    /// The copy key for <c>role-not-assignable</c>: <c>owner</c> or an unknown code in a Role change (story 4.4, Q10 (a)).
    /// </summary>
    public static string RoleNotAssignableKey => "role-not-assignable";

    /// <summary>
    /// The copy key for <c>ownership-held</c>: the Owner removing its own Membership or leaving.
    /// </summary>
    public static string OwnershipHeldKey => "ownership-held";

    /// <summary>
    /// The copy key for <c>ownership-offer-pending</c>: another Ownership Offer of the Space is still pending.
    /// </summary>
    public static string OwnershipOfferPendingKey => "ownership-offer-pending";

    /// <summary>
    /// The copy key for <c>ownership-offer-not-pending</c>: the Ownership Offer is no longer open.
    /// </summary>
    public static string OwnershipOfferNotPendingKey => "ownership-offer-not-pending";

    /// <summary>
    /// The copy key for <c>400 invalid-offer-recipient</c>: the person chosen is not another Membership of this Space.
    /// </summary>
    public static string InvalidOfferRecipientKey => "invalid-offer-recipient";

    /// <summary>
    /// The resource a refusal is rendered with.
    /// </summary>
    /// <param name="problem">The problem body, or <c>null</c> for an address the client cannot route.</param>
    /// <returns>The resource key, and the limit to format it with when it is a bound refusal.</returns>
    public static RefusalMessage For(ProblemResponse? problem) => For(problem, hasCopy: _ => true);

    /// <summary>
    /// The resource a refusal is rendered with, given which resource keys the client has copy for.
    /// </summary>
    /// <remarks>
    /// A capability code the client has no copy for - a server newer than a cached client - falls
    /// back to the generic capability statement, so the page never renders a bare code.
    /// </remarks>
    /// <param name="problem">The problem body, or <c>null</c> for an address the client cannot route.</param>
    /// <param name="hasCopy">Whether the client has a resource for a key.</param>
    /// <returns>The resource key, and the limit to format it with when it is a bound refusal.</returns>
    public static RefusalMessage For(ProblemResponse? problem, Func<string, bool> hasCopy)
    {
        ArgumentNullException.ThrowIfNull(hasCopy);

        var type = problem?.Type;

        if (string.Equals(type, ProblemTypes.InsufficientRole, StringComparison.Ordinal))
        {
            var capability = problem!.Capability;

            return new RefusalMessage(
                string.IsNullOrWhiteSpace(capability) || !hasCopy(capability) ? RoleDoesNotPermitKey : capability,
                Limit: null);
        }

        if (string.Equals(type, ProblemTypes.BoundReached, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(problem!.Bound)
            && problem.Limit is { } limit)
        {
            return new RefusalMessage(problem.Bound, limit);
        }

        // None of these is a not-found. A person told their request is still in flight must not be told it
        // does not exist (story 2.2, AC13; deferred-work.md:460), and a person whose edit had one field refused
        // must not be told their Task is gone (story 2.3, AC12).
        if (OwnKey(type) is { } own)
        {
            return new RefusalMessage(own, Limit: null);
        }

        return new RefusalMessage(NotAvailableKey, Limit: null);
    }

    /// <summary>
    /// The refusals that carry copy of their own, by type. A dictionary rather than a chain of comparisons since story
    /// 2.4 added five, so each type is one line and the lookup is ordinal. Frozen, so nothing static in the client is a
    /// mutable collection (story 3.4, AC12).
    /// </summary>
    private static readonly FrozenDictionary<string, string> OwnKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [ProblemTypes.IdempotencyKeyInFlight] = IdempotencyKeyInFlightKey,
        [ProblemTypes.IdempotencyKeyReused] = IdempotencyKeyReusedKey,
        [ProblemTypes.InvalidTaskTitle] = InvalidTaskTitleKey,
        [ProblemTypes.InvalidTaskStatus] = InvalidTaskStatusKey,
        [ProblemTypes.InvalidDueDate] = InvalidDueDateKey,
        [ProblemTypes.InvalidTaskEdit] = InvalidTaskEditKey,
        [ProblemTypes.InvalidLabelName] = InvalidLabelNameKey,
        [ProblemTypes.InvalidLabelColour] = InvalidLabelColourKey,
        [ProblemTypes.LabelNameTaken] = LabelNameTakenKey,
        [ProblemTypes.LabelInUse] = LabelInUseKey,
        [ProblemTypes.InvalidTaskLabels] = InvalidTaskLabelsKey,
        [ProblemTypes.InvalidAssignee] = InvalidAssigneeKey,
        [ProblemTypes.TaskPositionUnavailable] = TaskPositionUnavailableKey,
        [ProblemTypes.TaskPositionFull] = TaskPositionFullKey,
        [ProblemTypes.InvalidListQuery] = InvalidListQueryKey,
        [ProblemTypes.InvalidListCursor] = InvalidListCursorKey,
        [ProblemTypes.InvalidSpaceName] = InvalidSpaceNameKey,
        [ProblemTypes.InvalidEmailAddress] = InvalidEmailAddressKey,
        [ProblemTypes.RoleNotOfferable] = RoleNotOfferableKey,
        [ProblemTypes.AlreadyAMember] = AlreadyAMemberKey,
        [ProblemTypes.InvitationPending] = InvitationPendingKey,
        [ProblemTypes.InvitationNotPending] = InvitationNotPendingKey,
        [ProblemTypes.InvitationNotValid] = InvitationNotValidKey,
        [ProblemTypes.InvitationForAnotherAccount] = InvitationForAnotherAccountKey,
        [ProblemTypes.OwnerRoleFixed] = OwnerRoleFixedKey,
        [ProblemTypes.RoleNotAssignable] = RoleNotAssignableKey,
        [ProblemTypes.OwnershipHeld] = OwnershipHeldKey,
        [ProblemTypes.OwnershipOfferPending] = OwnershipOfferPendingKey,
        [ProblemTypes.OwnershipOfferNotPending] = OwnershipOfferNotPendingKey,
        [ProblemTypes.InvalidOfferRecipient] = InvalidOfferRecipientKey,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static string? OwnKey(string? type) =>
        type is not null && OwnKeys.TryGetValue(type, out var key) ? key : null;
}

/// <summary>
/// One refusal, as the client will render it.
/// </summary>
/// <param name="Key">The resource key in <c>ClientCopy.resx</c>.</param>
/// <param name="Limit">The bound's value, the resource's one format argument; <c>null</c> otherwise.</param>
public sealed record RefusalMessage(string Key, int? Limit);
