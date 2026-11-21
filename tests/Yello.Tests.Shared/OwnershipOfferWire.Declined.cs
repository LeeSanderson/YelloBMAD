using System.Text.Json;

namespace Yello.Tests.Shared;

/// <summary>
/// The offering Owner's read of a declined offer: <c>GET /api/v1/spaces/{spaceId}/ownership-offers/declined</c>, <c>200</c>
/// with the Space's most recent offer when that offer was declined, or <c>204</c> with no body otherwise.
/// </summary>
public static partial class OwnershipOfferWire
{
    /// <summary>
    /// The declined answer's JSON members, in ordinal order: <c>DeclinedOwnershipOfferResponse(Id, RecipientName)</c>. Never
    /// an address, a recipient id or a Role.
    /// </summary>
    public static IReadOnlyList<string> DeclinedResponseMembers { get; } = ["id", "recipientName"];

    /// <summary>
    /// Reads the declined read's <c>200</c>.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The declined offer as the Owner is told it.</returns>
    public static DeclinedOwnershipOfferAnswer ParseDeclined(string body) =>
        JsonSerializer.Deserialize<DeclinedOwnershipOfferAnswer>(body, JsonOptions) ?? throw new InvalidOperationException("The answer carried no declined offer.");
}

/// <summary>
/// A declined offer as the offering Owner reads it: <c>DeclinedOwnershipOfferResponse</c>'s shape, restated so a contract
/// change is seen.
/// </summary>
/// <param name="Id">The declined offer.</param>
/// <param name="RecipientName">The recipient's display name now, or <c>null</c> once its Membership is gone.</param>
public sealed record DeclinedOwnershipOfferAnswer(Guid Id, string? RecipientName);
