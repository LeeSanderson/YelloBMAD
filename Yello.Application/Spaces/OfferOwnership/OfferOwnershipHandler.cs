using Yello.Application.Memberships;
using Yello.Application.Outbox;

namespace Yello.Application.Spaces.OfferOwnership;

/// <summary>
/// Offers ownership of the active Space to one of its Memberships, and tells the recipient by email. FR-8, FR-43, AD-26.
/// </summary>
/// <remarks>
/// <para>
/// <b>Everything cross-cutting happened before this ran</b> (AR-3): the pipeline resolved the Space, checked
/// <c>OfferOwnershipTransfer</c> and replayed an <c>Idempotency-Key</c> it had already seen, so a replay never reaches this
/// code and never sends a second email. This slice reads no Role: the store's guarded statement proves the caller is the
/// Owner and the recipient another Membership of this Space.
/// </para>
/// <para>
/// <b>Nothing moves</b>: the offer is a row and an email. No Role changes and no <c>MembershipChanged</c> is published.
/// </para>
/// <para>
/// <b>One unit of work</b>: the offer, its email and its send record commit or roll back together, and the dispatcher sends
/// only after the commit.
/// </para>
/// </remarks>
/// <param name="offers">Makes and reads the offer.</param>
/// <param name="memberships">Reads who the offer names and who makes it, inside this Space.</param>
/// <param name="spaceNames">Reads the Space's name, for the email.</param>
/// <param name="links">The link the email carries.</param>
/// <param name="outbox">Enqueues the email.</param>
public sealed class OfferOwnershipHandler(
    IOwnershipOfferStore offers,
    IMembershipStore memberships,
    IActiveSpaceNameReader spaceNames,
    OwnershipOfferLinks links,
    IOutbox outbox)
{
    /// <summary>
    /// Makes the offer, then enqueues its one email.
    /// </summary>
    /// <param name="context">The active Space, as the pipeline resolved it on this request.</param>
    /// <param name="recipientMembershipId">The recipient, from the body.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>What happened, with the offer as it now stands when it was made.</returns>
    public Task<OfferOwnershipResult> HandleAsync(ActiveSpaceContext context, Guid recipientMembershipId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return OfferAsync(context.AccountId, recipientMembershipId, cancellationToken);
    }

    private static OfferOwnershipResult Refused(OwnershipOfferOutcome outcome) => new(outcome, null);

    private async Task<OfferOwnershipResult> OfferAsync(Guid actorAccountId, Guid recipientMembershipId, CancellationToken cancellationToken)
    {
        if (await memberships.OwnershipOfferNoticeAsync(actorAccountId, recipientMembershipId, cancellationToken).ConfigureAwait(false) is not { } notice)
        {
            return Refused(OwnershipOfferOutcome.InvalidRecipient);
        }

        if (notice.OffererName is not { } offererName)
        {
            return Refused(OwnershipOfferOutcome.LostMembership);
        }

        var made = await offers.OfferAsync(actorAccountId, recipientMembershipId, offererName, cancellationToken).ConfigureAwait(false);

        if (made.Outcome != OwnershipOfferOutcome.Offered)
        {
            return Refused(made.Outcome);
        }

        // Null only when the Space went after the pipeline resolved it; the refusal rolls the offer back with the unit of work.
        if (await spaceNames.ReadAsync(cancellationToken).ConfigureAwait(false) is not { } spaceName
            || await offers.ReadPendingAsync(cancellationToken).ConfigureAwait(false) is not { } pending)
        {
            return Refused(OwnershipOfferOutcome.SpaceGone);
        }

        var email = OwnershipOfferEmail.Render(spaceName, offererName, links.Home, pending.ExpiresAt);

        await outbox.EnqueueAsync(
            new OutboxEmail(OutboxKinds.OwnershipOffered, notice.RecipientAddress, email.Subject, email.Body),
            cancellationToken).ConfigureAwait(false);

        return new OfferOwnershipResult(
            OwnershipOfferOutcome.Offered,
            new OwnershipOfferView(pending.Id, pending.RecipientMembershipId, notice.RecipientName, pending.ExpiresAt, pending.ReadAt));
    }
}

/// <summary>
/// The outcome of <see cref="OfferOwnershipHandler.HandleAsync"/>.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Offer">The offer, when it was made.</param>
public sealed record OfferOwnershipResult(OwnershipOfferOutcome Outcome, OwnershipOfferView? Offer);
