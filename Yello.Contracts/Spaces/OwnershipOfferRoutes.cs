namespace Yello.Contracts.Spaces;

/// <summary>
/// The Ownership Offer endpoints' paths, stated once for the client that calls them and the Host that serves them.
/// </summary>
/// <remarks>
/// <b><c>{spaceId}</c> first after the version</b>, so the pipeline resolves the route because of its shape. All three
/// declare <c>OfferOwnershipTransfer</c>, which only the Owner holds, and are served on both surfaces.
/// </remarks>
public static class OwnershipOfferRoutes
{
    /// <summary>
    /// <c>POST</c> an <see cref="OfferOwnershipRequest"/> here: <c>201</c> with the <see cref="OwnershipOfferResponse"/> and a
    /// <c>Location</c> of <see cref="Pending"/>; <c>400 invalid-offer-recipient</c> for a body naming no other Membership of
    /// this Space; <c>409 ownership-offer-pending</c> while another offer is pending.
    /// </summary>
    public static string Offers => "/api/v1/spaces/{spaceId:guid}/ownership-offers";

    /// <summary>
    /// <c>GET</c> here: <c>200</c> with the actionable pending <see cref="OwnershipOfferResponse"/>, or <c>204</c> with no body
    /// when there is none. A pending offer past its expiry reads as none.
    /// </summary>
    public static string Pending => "/api/v1/spaces/{spaceId:guid}/ownership-offers/pending";

    /// <summary>
    /// <c>DELETE</c> here revokes the offer: <c>204</c> with no body; <c>409 ownership-offer-not-pending</c> for an offer of
    /// this Space that is no longer actionable; the boundary's <c>404</c> for an id that names no offer in this Space.
    /// </summary>
    public static string Offer => "/api/v1/spaces/{spaceId:guid}/ownership-offers/{offerId:guid}";

    /// <summary>
    /// The offer collection of one Space, as a client calls it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string OffersOf(Guid spaceId) => $"/api/v1/spaces/{spaceId:D}/ownership-offers";

    /// <summary>
    /// The pending-offer read of one Space, as a client calls it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string PendingOf(Guid spaceId) => $"/api/v1/spaces/{spaceId:D}/ownership-offers/pending";

    /// <summary>
    /// One offer of one Space, as a client calls it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="offerId">The offer.</param>
    /// <returns>The path.</returns>
    public static string OfferOf(Guid spaceId, Guid offerId) => $"/api/v1/spaces/{spaceId:D}/ownership-offers/{offerId:D}";
}
