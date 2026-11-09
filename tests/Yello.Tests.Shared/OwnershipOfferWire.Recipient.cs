using System.Text.Json;

namespace Yello.Tests.Shared;

/// <summary>
/// The recipient's side of an Ownership Offer: the read of the offer naming the caller
/// (<c>GET /api/v1/spaces/{spaceId}/ownership-offers/mine</c>, <c>200</c> or <c>204</c>), the acceptance
/// (<c>POST .../ownership-offers/{offerId}/acceptance</c>, no body, <c>204</c>) and the decline
/// (<c>POST .../ownership-offers/{offerId}/decline</c>, no body, <c>204</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>The route members here are stand-ins</b> until <c>OwnershipOfferRoutes</c> declares <c>Mine</c>/<c>MineOf</c>,
/// <c>Acceptance</c>/<c>AcceptanceOf</c> and <c>Decline</c>/<c>DeclineOf</c>. The develop phase swaps every use (Slices,
/// Isolation, the browser fake) for the contract members and deletes the stand-ins. <see cref="RecipientResponseMembers"/>,
/// <see cref="ParseForRecipient"/> and <see cref="OwnershipOfferForRecipientAnswer"/> are lasting readings.
/// </para>
/// </remarks>
public static partial class OwnershipOfferWire
{
    /// <summary>
    /// Stand-in for <c>OwnershipOfferRoutes.Mine</c>: the recipient's read, as the Host maps it.
    /// </summary>
    public static string MineRoute => "/api/v1/spaces/{spaceId:guid}/ownership-offers/mine";

    /// <summary>
    /// Stand-in for <c>OwnershipOfferRoutes.Acceptance</c>: the acceptance, as the Host maps it.
    /// </summary>
    public static string AcceptanceRoute => "/api/v1/spaces/{spaceId:guid}/ownership-offers/{offerId:guid}/acceptance";

    /// <summary>
    /// Stand-in for <c>OwnershipOfferRoutes.Decline</c>: the decline, as the Host maps it.
    /// </summary>
    public static string DeclineRoute => "/api/v1/spaces/{spaceId:guid}/ownership-offers/{offerId:guid}/decline";

    /// <summary>
    /// The recipient's answer's JSON members, in ordinal order: <c>OwnershipOfferForRecipientResponse(Id, OffererName,
    /// ExpiresAt, ReadAt)</c>. Never a recipient id, an address, a Role or another Membership.
    /// </summary>
    public static IReadOnlyList<string> RecipientResponseMembers { get; } = ["expiresAt", "id", "offererName", "readAt"];

    /// <summary>
    /// Stand-in for <c>OwnershipOfferRoutes.MineOf</c>.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string MineOf(Guid spaceId) => $"/api/v1/spaces/{spaceId:D}/ownership-offers/mine";

    /// <summary>
    /// Stand-in for <c>OwnershipOfferRoutes.AcceptanceOf</c>.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="offerId">The offer.</param>
    /// <returns>The path.</returns>
    public static string AcceptanceOf(Guid spaceId, Guid offerId) => $"/api/v1/spaces/{spaceId:D}/ownership-offers/{offerId:D}/acceptance";

    /// <summary>
    /// Stand-in for <c>OwnershipOfferRoutes.DeclineOf</c>.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="offerId">The offer.</param>
    /// <returns>The path.</returns>
    public static string DeclineOf(Guid spaceId, Guid offerId) => $"/api/v1/spaces/{spaceId:D}/ownership-offers/{offerId:D}/decline";

    /// <summary>
    /// Reads the recipient's <c>200</c>.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The offer as the recipient is told it.</returns>
    public static OwnershipOfferForRecipientAnswer ParseForRecipient(string body) =>
        JsonSerializer.Deserialize<OwnershipOfferForRecipientAnswer>(body, JsonOptions) ?? throw new InvalidOperationException("The answer carried no offer.");
}

/// <summary>
/// The offer as its named recipient reads it: <c>OwnershipOfferForRecipientResponse</c>'s shape, restated so a contract
/// change is seen.
/// </summary>
/// <param name="Id">The offer, which the acceptance and the decline name.</param>
/// <param name="OffererName">The offerer's display name as captured when the offer was made; never a live read.</param>
/// <param name="ExpiresAt">When the offer stops being actionable, by the database clock.</param>
/// <param name="ReadAt">The database clock when the answer was read.</param>
public sealed record OwnershipOfferForRecipientAnswer(Guid Id, string OffererName, DateTimeOffset ExpiresAt, DateTimeOffset ReadAt);
