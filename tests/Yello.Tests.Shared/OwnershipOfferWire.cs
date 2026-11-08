using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Yello.Tests.Shared;

/// <summary>
/// The test side of offering ownership of a Space: the offer (<c>POST /api/v1/spaces/{spaceId}/ownership-offers</c>, body
/// <c>{ membershipId }</c>, answered <c>201</c> with the offer), the Owner's read of the pending offer
/// (<c>GET .../ownership-offers/pending</c>, <c>200</c> or <c>204</c>), the revoke (<c>DELETE .../ownership-offers/{offerId}</c>,
/// <c>204</c>), and the fixed values the suites assert against. Routes and problem types are the contract's own
/// (<c>OwnershipOfferRoutes</c>, <c>ProblemTypes</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Bodies are JSON text or anonymous objects</b>, never a contract type, so a case can send what the route must refuse: an
/// address instead of a Membership, no member at all, or a body that is not an object.
/// </para>
/// </remarks>
public static partial class OwnershipOfferWire
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The capability every offer route declares, as a <c>403</c> body names it: the matrix's "Offer to transfer ownership".
    /// </summary>
    public static string CapabilityCode => "offer-ownership-transfer";

    /// <summary>
    /// The outbox kind of the offer email, as the send record stores it.
    /// </summary>
    public static string OwnershipOfferedKind => "ownership-offered";

    /// <summary>
    /// The days an offer stays open, stamped by the database clock. PRD section 12 assumption 3, unconfirmed.
    /// </summary>
    public static int ExpiryDays => 7;

    /// <summary>
    /// The offer answer's JSON members, in ordinal order: <c>OwnershipOfferResponse(Id, RecipientMembershipId, RecipientName,
    /// ExpiresAt, ReadAt)</c>. Never an address, a Role, an Account id or a Space id.
    /// </summary>
    public static IReadOnlyList<string> ResponseMembers { get; } = ["expiresAt", "id", "readAt", "recipientMembershipId", "recipientName"];

    /// <summary>
    /// The offer body: <c>{ membershipId }</c>.
    /// </summary>
    /// <param name="membershipId">The recipient Membership.</param>
    /// <returns>The body.</returns>
    public static HttpContent Body(Guid? membershipId) => JsonContent.Create(new { membershipId }, options: JsonOptions);

    /// <summary>
    /// A body of the case's own text, for the refusals of a body that is not an object or names no Membership.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <returns>The body.</returns>
    public static HttpContent RawBody(string json) => new StringContent(json, Encoding.UTF8, "application/json");

    /// <summary>
    /// Reads an offer answer (the offer's <c>201</c> or the pending read's <c>200</c>).
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The offer as answered.</returns>
    public static OwnershipOfferAnswer Parse(string body) =>
        JsonSerializer.Deserialize<OwnershipOfferAnswer>(body, JsonOptions) ?? throw new InvalidOperationException("The answer carried no offer.");

    /// <summary>
    /// Whether <paramref name="email"/> is an offer email: the subject the copy gives ("{offerer} offered you ownership of
    /// {space} on Yello"). An Invitation or an assignment email to the same address is not one.
    /// </summary>
    /// <param name="email">A dropped email.</param>
    /// <returns>Whether it is an offer email.</returns>
    public static bool IsOwnershipOfferEmail(DroppedEmail email)
    {
        ArgumentNullException.ThrowIfNull(email);

        return email.Subject.Contains("offered you ownership of", StringComparison.Ordinal);
    }

    /// <summary>
    /// The offer emails to <paramref name="address"/> among <paramref name="emails"/>.
    /// </summary>
    /// <param name="emails">Every dropped email.</param>
    /// <param name="address">The recipient.</param>
    /// <returns>The offer emails to it.</returns>
    public static IReadOnlyList<DroppedEmail> OfferEmailsTo(IEnumerable<DroppedEmail> emails, string address) =>
        [.. emails.Where(email => string.Equals(email.To, address, StringComparison.OrdinalIgnoreCase) && IsOwnershipOfferEmail(email))];

    /// <summary>
    /// Every absolute URL in <paramref name="text"/>, for the "no means of accepting" assertions: a link carrying an id or
    /// naming an accept route would let a forwarded email act or disclose.
    /// </summary>
    /// <param name="text">The email's text.</param>
    /// <returns>The URLs, in order.</returns>
    public static IReadOnlyList<string> LinksIn(string text) =>
        [.. Links.Matches(text ?? string.Empty).Select(match => match.Value.TrimEnd('.', ',', ')', '"', '\''))];

    /// <summary>
    /// Whether <paramref name="text"/> holds anything shaped like a GUID, in any of its usual spellings.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>Whether a GUID appears.</returns>
    public static bool HoldsAGuid(string text) => Guids.IsMatch(text ?? string.Empty);

    [GeneratedRegex(@"https?://[^\s<>""]+", RegexOptions.IgnoreCase)]
    private static partial Regex Links { get; }

    [GeneratedRegex(@"[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}", RegexOptions.IgnoreCase)]
    private static partial Regex Guids { get; }
}

/// <summary>
/// One offer as the wire answers it: <c>OwnershipOfferResponse</c>'s shape, restated so a contract change is seen.
/// </summary>
/// <param name="Id">The offer, which the revoke names.</param>
/// <param name="RecipientMembershipId">The Membership offered ownership.</param>
/// <param name="RecipientName">The recipient's display name now; never an address.</param>
/// <param name="ExpiresAt">When the offer stops being actionable, by the database clock.</param>
/// <param name="ReadAt">The database clock when the answer was read, so the client counts days from the server's time.</param>
public sealed record OwnershipOfferAnswer(Guid Id, Guid RecipientMembershipId, string RecipientName, DateTimeOffset ExpiresAt, DateTimeOffset ReadAt);
