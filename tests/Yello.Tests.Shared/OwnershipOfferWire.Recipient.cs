using System.Text.Json;

namespace Yello.Tests.Shared;

/// <summary>
/// The recipient's side of an Ownership Offer: the read of the offer naming the caller
/// (<c>GET /api/v1/spaces/{spaceId}/ownership-offers/mine</c>, <c>200</c> or <c>204</c>), the acceptance
/// (<c>POST .../ownership-offers/{offerId}/acceptance</c>, no body, <c>204</c>) and the decline
/// (<c>POST .../ownership-offers/{offerId}/decline</c>, no body, <c>204</c>).
/// </summary>
/// <remarks>
/// The routes are <c>OwnershipOfferRoutes</c>' own; these are the readings of the recipient's answer.
/// </remarks>
public static partial class OwnershipOfferWire
{
    /// <summary>
    /// The recipient's answer's JSON members, in ordinal order: <c>OwnershipOfferForRecipientResponse(Id, OffererName,
    /// ExpiresAt, ReadAt)</c>. Never a recipient id, an address, a Role or another Membership.
    /// </summary>
    public static IReadOnlyList<string> RecipientResponseMembers { get; } = ["expiresAt", "id", "offererName", "readAt"];

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
