using System.Collections.Concurrent;
using System.Text.Json;
using Yello.Contracts;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// The settled offer on the fake: the Owner's declined read, <c>GET /api/v1/spaces/{id}/ownership-offers/declined</c>, and a
/// refusal a case sets for the recipient's next decline.
/// </summary>
/// <remarks>
/// <para>
/// <b>As the Host answers them.</b> The declined read is the Owner's alone (the pipeline's boundary <c>404</c> and the
/// <c>403</c> naming <c>offer-ownership-transfer</c> first); then <c>200</c> with <c>{ id, recipientName }</c> while the
/// Space's latest offer is the one in <see cref="DeclinedOffers"/>, or <c>204</c>. A new offer makes the latest offer a pending
/// one, so it clears the Space's entry. The route is <see cref="OwnershipOfferWire.DeclinedOf"/>'s stand-in until the contract
/// declares it.
/// </para>
/// <para>
/// <b>Nothing here changes an earlier case.</b> The client sends no declined read until the Ownership section learns to, and
/// <see cref="NextOfferDeclineRefusal"/> is unset unless a case sets it.
/// </para>
/// </remarks>
internal sealed partial class FakeYelloApi
{
    /// <summary>
    /// Each Space whose latest offer was declined, as the declined read answers it: (offer id, the recipient's name now, or
    /// <c>null</c> once its Membership is gone). Set by a case, or by <see cref="DeclineElsewhere"/>; cleared by a new offer.
    /// </summary>
    public ConcurrentDictionary<Guid, (Guid Id, string? RecipientName)> DeclinedOffers { get; } = new();

    /// <summary>
    /// When set, the next decline is refused with this instead of applied. A refusal of <c>ownership-offer-not-pending</c>, or
    /// the not-found an offer that no longer names this Account gets, also removes the offer naming this Account, as the
    /// Host's next read would find none.
    /// </summary>
    public (int Status, string ProblemType)? NextOfferDeclineRefusal { get; set; }

    /// <summary>
    /// Every declined read sent in <paramref name="spaceId"/>, in order.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The requests.</returns>
    public IReadOnlyList<FakeRequest> DeclinedReads(Guid spaceId) =>
        [.. _requests.Where(request => request.Method == "GET" && request.Path == OwnershipOfferWire.DeclinedOf(spaceId))];

    /// <summary>
    /// The recipient declines the Space's pending offer in another tab: the offer is no longer pending, a revoke of it is
    /// <c>409 ownership-offer-not-pending</c>, and the declined read names it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="recipientName">The recipient's name now, or <c>null</c> once its Membership is gone.</param>
    public void DeclineElsewhere(Guid spaceId, string? recipientName)
    {
        if (!PendingOffers.TryRemove(spaceId, out var offer))
        {
            throw new InvalidOperationException("The Space holds no pending offer to decline.");
        }

        _closedOffers.GetOrAdd(spaceId, _ => []).Add(offer.Id);
        DeclinedOffers[spaceId] = (offer.Id, recipientName);
    }

    private (int Status, string Body) ReadDeclined(Guid spaceId) =>
        DeclinedOffers.TryGetValue(spaceId, out var declined)
            ? (200, JsonSerializer.Serialize(new { id = declined.Id, recipientName = declined.RecipientName }, JsonOptions))
            : (204, string.Empty);

    /// <summary>
    /// The refusal a case set for the next decline, once; a refusal that closes the offer removes it.
    /// </summary>
    private (int Status, string Body)? TakeDeclineRefusal(Guid spaceId)
    {
        if (NextOfferDeclineRefusal is not { } refusal)
        {
            return null;
        }

        NextOfferDeclineRefusal = null;

        if (refusal.ProblemType == ProblemTypes.OwnershipOfferNotPending || refusal.ProblemType == ProblemTypes.NotAvailable)
        {
            OffersForMe.TryRemove(spaceId, out _);
        }

        return (refusal.Status, Problem(refusal.ProblemType));
    }
}
