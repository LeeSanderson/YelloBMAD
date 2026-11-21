using System.Text.Json;

namespace Yello.Tests.Shared;

/// <summary>
/// The offering Owner's read of a declined offer: <c>GET /api/v1/spaces/{spaceId}/ownership-offers/declined</c>, <c>200</c>
/// with the Space's most recent offer when that offer was declined, or <c>204</c> with no body otherwise.
/// </summary>
/// <remarks>
/// <para>
/// <b>The route members here are stand-ins</b> until <c>OwnershipOfferRoutes</c> declares <c>Declined</c> and
/// <c>DeclinedOf</c>. The develop phase swaps every use (Slices, Isolation, the browser fake) for the contract members and
/// deletes the stand-ins. <see cref="DeclinedResponseMembers"/>, <see cref="ParseDeclined"/> and
/// <see cref="DeclinedOwnershipOfferAnswer"/> are lasting readings.
/// </para>
/// </remarks>
public static partial class OwnershipOfferWire
{
    /// <summary>
    /// Stand-in for <c>OwnershipOfferRoutes.Declined</c>: the Owner's read of a declined offer, as the Host maps it.
    /// </summary>
    public static string DeclinedRoute => "/api/v1/spaces/{spaceId:guid}/ownership-offers/declined";

    /// <summary>
    /// The declined answer's JSON members, in ordinal order: <c>DeclinedOwnershipOfferResponse(Id, RecipientName)</c>. Never
    /// an address, a recipient id or a Role.
    /// </summary>
    public static IReadOnlyList<string> DeclinedResponseMembers { get; } = ["id", "recipientName"];

    /// <summary>
    /// Stand-in for <c>OwnershipOfferRoutes.DeclinedOf</c>: the declined read of one Space, as a client calls it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string DeclinedOf(Guid spaceId) => $"/api/v1/spaces/{spaceId:D}/ownership-offers/declined";

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
