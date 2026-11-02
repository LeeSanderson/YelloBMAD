using System.Net.Http.Json;
using System.Text.Json;

namespace Yello.Tests.Shared;

/// <summary>
/// The test side of stories 4.1, 4.2 and 4.3's Invitation routes: the request bodies, fresh addresses, the list's row and
/// the view's shapes, the token read from an email, and the fixed values the suites assert against. The routes and the problem types are <c>Yello.Contracts</c>'
/// (<c>InvitationRoutes</c>, <c>ProblemTypes</c>); this holds only what the contract does not. It replaced 4.1's ATDD
/// <c>InvitationScaffold</c>; story 4.2's dev phase swapped its own ATDD stand-ins (the revoke route, path and problem
/// type) for the contract members and deleted them.
/// </summary>
public static class InvitationWire
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The capability the route declares, as the <c>403</c> body names it.
    /// </summary>
    public static string CapabilityCode => "issue-and-revoke-invitations";

    /// <summary>
    /// The days between issue and expiry: PRD section 12 #9, an assumption every case resting on it traits. Stated here,
    /// independently of the product's column default, so a change to either is seen.
    /// </summary>
    public static int ExpiryDays => 7;

    /// <summary>
    /// The path the email's link opens on the client origin, before the token (AC7). Story 4.3 builds the route.
    /// </summary>
    public static string LinkPath => "/invitations/";

    /// <summary>
    /// The longest address the plausibility rule admits.
    /// </summary>
    public static int EmailAddressMaxLength => 254;

    /// <summary>
    /// The request body: exactly <c>emailAddress</c> and <c>role</c> (AC1), the Role as its stable code.
    /// </summary>
    /// <param name="emailAddress">The address, or <c>null</c> to send JSON null.</param>
    /// <param name="role">The Role's stable code, or <c>null</c>.</param>
    /// <returns>The body.</returns>
    public static HttpContent Body(string? emailAddress, string? role) =>
        JsonContent.Create(new { emailAddress, role }, options: JsonOptions);

    /// <summary>
    /// A fresh address nobody else uses (<c>TESTING-CONVENTIONS.md</c>: never a shared email address).
    /// </summary>
    /// <param name="label">A short label naming the case, for reading a failure.</param>
    /// <returns>The address.</returns>
    public static string FreshAddress(string label) => $"invitee-{label}-{Guid.NewGuid():N}@example.test";

    /// <summary>
    /// The list row's JSON members, in ordinal order: exactly the five Q5 (a) names (story 4.2 AC1). Never the token hash,
    /// the normalised address, the issuer's Account, the Space, the state or the expiry.
    /// </summary>
    public static IReadOnlyList<string> ListedMembers { get; } = ["emailAddress", "id", "issuedAt", "issuerName", "role"];

    /// <summary>
    /// Reads a <c>200</c> list body (story 4.2 AC1) as rows, in the order the server sent them.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<ListedInvitation> ParseList(string body) =>
        JsonSerializer.Deserialize<List<ListedInvitation>>(body, JsonOptions) ?? [];

    /// <summary>
    /// Each list row's JSON member names, in ordinal order, so a shape assertion sees an extra member the record would
    /// silently drop.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>One ordered name list per row.</returns>
    public static IReadOnlyList<IReadOnlyList<string>> ListMemberNames(string body)
    {
        using var document = JsonDocument.Parse(body);

        return [.. document.RootElement.EnumerateArray().Select(row => row.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal).ToList())];
    }

    // ---- Story 4.3 (ATDD run, 2026-11-01) ----------------------------------------------------------------------------
    //
    // TEMPORARY stand-ins, as 4.2's ATDD run left RevokeRoute here: OfferRoute, AcceptanceRoute, TokenHeaderName,
    // NotValidProblemType and ForAnotherAccountProblemType are what Lee's Q4 (a), Q6 (a) and the story's Q13 (a) name, and
    // Task 3 declares them in Yello.Contracts (InvitationRoutes, ProblemTypes). The dev phase swaps every use for the
    // contract member and deletes these five. Everything else below stays: it is the test's reading of the wire.

    /// <summary>
    /// TEMPORARY (story 4.3 ATDD): the view, <c>GET</c> with the token in <see cref="TokenHeaderName"/> (Lee's Q4 (a)).
    /// </summary>
    public static string OfferRoute => "/api/v1/invitations/offer";

    /// <summary>
    /// TEMPORARY (story 4.3 ATDD): the accept, <c>POST</c> with <see cref="AcceptBody"/> (Lee's Q4 (a)).
    /// </summary>
    public static string AcceptanceRoute => "/api/v1/invitations/acceptance";

    /// <summary>
    /// TEMPORARY (story 4.3 ATDD): the request header the view's token travels in, never a URL (Lee's Q4 (a)).
    /// </summary>
    public static string TokenHeaderName => "Yello-Invitation-Token";

    /// <summary>
    /// TEMPORARY (story 4.3 ATDD): the one answer for a revoked, accepted, expired, lapsed or unrecognised Invitation, on
    /// both routes - <c>404</c>, a type of its own, distinct from the boundary's <c>not-available</c> (Lee's Q6 (a)).
    /// </summary>
    public static string NotValidProblemType => "urn:yello:problem:invitation-not-valid";

    /// <summary>
    /// TEMPORARY (story 4.3 ATDD): the accept's <c>403</c> for a signed-in Account the Invitation does not name (Q13 (a)).
    /// </summary>
    public static string ForAnotherAccountProblemType => "urn:yello:problem:invitation-for-another-account";

    /// <summary>
    /// The bound the accept consults, as the <c>409 bound-reached</c> body names it (AR-30, UX-DR35).
    /// </summary>
    public static string MembershipsPerSpaceBoundCode => "memberships-per-space";

    /// <summary>
    /// Memberships per Space, NFR-8 - stated here independently of <c>BoundRegistry</c>, so a change to either is seen.
    /// </summary>
    public static int MembershipsPerSpace => 100;

    /// <summary>
    /// The view's JSON members, in ordinal order, when a Session is present (Lee's Q5 (a)): the Space's name, the Role's
    /// stable code, the issuer's captured name and the addressee indicator. Never the address, an id, the expiry, the
    /// state or a count (UX-DR18). Without a Session the indicator is <c>null</c> or absent.
    /// </summary>
    public static IReadOnlyList<string> OfferMembers { get; } = ["addressedToYou", "issuerName", "role", "spaceName"];

    /// <summary>
    /// The accept's <c>200</c> body's one member (Q10 (a)).
    /// </summary>
    public static IReadOnlyList<string> AcceptedMembers { get; } = ["spaceId"];

    /// <summary>
    /// The prefix every Invitation token carries (<c>InvitationTokenSecret.Prefix</c>, internal to Infrastructure; story
    /// 4.1's gate <c>An_Invitation_token_is_256_random_bits_behind_its_own_prefix_and_is_stored_as_its_SHA_256</c> holds
    /// the product to it).
    /// </summary>
    public static string TokenPrefix => "yello_inv_";

    /// <summary>
    /// The accept's body: the token and nothing else (Lee's Q4 (a); the Role and Space come from the row).
    /// </summary>
    /// <param name="invitationToken">The token, or <c>null</c> to send JSON null.</param>
    /// <returns>The body.</returns>
    public static HttpContent AcceptBody(string? invitationToken) =>
        JsonContent.Create(new { invitationToken }, options: JsonOptions);

    /// <summary>
    /// The view's headers: the token in <see cref="TokenHeaderName"/>.
    /// </summary>
    /// <param name="token">The token, sent exactly as given.</param>
    /// <returns>The headers.</returns>
    public static Dictionary<string, string> TokenHeaders(string token) =>
        new(StringComparer.Ordinal) { [TokenHeaderName] = token };

    /// <summary>
    /// A well-formed token that was never issued: the prefix and 32 fresh random bytes, Base64Url.
    /// </summary>
    /// <returns>The token.</returns>
    public static string UnrecognisedToken() =>
        TokenPrefix + System.Buffers.Text.Base64Url.EncodeToString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Tokens that cannot name any Invitation, each malformed a different way (AC6's "unrecognised" arm): the API Token's
    /// prefix, no prefix, too short, too long, and characters outside Base64Url.
    /// </summary>
    /// <returns>The tokens.</returns>
    public static IReadOnlyList<string> MalformedTokens()
    {
        var body = System.Buffers.Text.Base64Url.EncodeToString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        return
        [
            "yello_pat_" + body,
            body,
            TokenPrefix + body[..10],
            TokenPrefix + body + body,
            TokenPrefix + new string('!', body.Length),
        ];
    }

    /// <summary>
    /// The token in an invitation email: the text after the one <see cref="LinkPath"/> link, up to the next whitespace.
    /// Whatever origin the Host was configured with, the path and the token are the email's (story 4.1 AC7).
    /// </summary>
    /// <param name="email">The email.</param>
    /// <returns>The token.</returns>
    public static string TokenIn(DroppedEmail email)
    {
        ArgumentNullException.ThrowIfNull(email);

        var start = email.PlainText.IndexOf(LinkPath + TokenPrefix, StringComparison.Ordinal);

        if (start < 0)
        {
            throw new InvalidOperationException("The email carries no invitation link.");
        }

        var token = new string([.. email.PlainText[(start + LinkPath.Length)..].TakeWhile(character => !char.IsWhiteSpace(character))]);

        return token.Length > TokenPrefix.Length ? token : throw new InvalidOperationException("The invitation link carries no token.");
    }

    /// <summary>
    /// Reads a <c>200</c> view body (AC2).
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The offer.</returns>
    public static OfferedInvitation ParseOffer(string body) =>
        JsonSerializer.Deserialize<OfferedInvitation>(body, JsonOptions) ?? throw new InvalidOperationException("The view answered no offer.");

    /// <summary>
    /// A JSON object body's member names, in ordinal order, so a shape assertion sees a member the record would drop.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The names.</returns>
    public static IReadOnlyList<string> MemberNames(string body)
    {
        using var document = JsonDocument.Parse(body);

        return [.. document.RootElement.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal)];
    }

    /// <summary>
    /// The Space id an accept's <c>200</c> body names (Q10 (a)).
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The Space.</returns>
    public static Guid AcceptedSpaceId(string body)
    {
        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("spaceId").GetGuid();
    }
}

/// <summary>
/// Story 4.3's view body as the test reads it: <c>InvitationOfferResponse</c>'s shape (Task 3, Lee's Q5 (a)), restated
/// independently so a contract change is seen by the suites rather than silently followed. Kept in the harness because
/// <c>Yello.Tests.Client</c>'s fake serialises the same shape.
/// </summary>
/// <param name="SpaceName">The invited Space's name.</param>
/// <param name="Role">The Role's stable code: <c>admin</c>, <c>member</c> or <c>viewer</c>.</param>
/// <param name="IssuerName">The issuer's display name as captured at issue.</param>
/// <param name="AddressedToYou">With a Session, whether its Account's address is the invited one; otherwise <c>null</c>.</param>
public sealed record OfferedInvitation(string SpaceName, string Role, string IssuerName, bool? AddressedToYou);

/// <summary>
/// One row of story 4.2's Invitation list as the test reads it: the Q5 (a) shape of <c>InvitationResponse</c>, restated
/// independently so a contract change is seen by the suites rather than silently followed. Kept in the harness because
/// <c>Yello.Tests.Client</c>'s fake serialises the same shape.
/// </summary>
/// <param name="Id">The Invitation's id, which the revoke names.</param>
/// <param name="EmailAddress">The stored, trimmed address.</param>
/// <param name="Role">The Role's stable code: <c>admin</c>, <c>member</c> or <c>viewer</c>.</param>
/// <param name="IssuerName">The issuer's display name as captured at issue.</param>
/// <param name="IssuedAt">The row's <c>CreatedAt</c>.</param>
public sealed record ListedInvitation(Guid Id, string EmailAddress, string Role, string IssuerName, DateTimeOffset IssuedAt);
