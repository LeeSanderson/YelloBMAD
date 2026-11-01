using System.Net.Http.Json;
using System.Text.Json;

namespace Yello.Tests.Shared;

/// <summary>
/// The test side of story 4.1's Invitation route: the request body, fresh addresses, and the fixed values the suites
/// assert against. The route and the problem types are <c>Yello.Contracts</c>' (<c>InvitationRoutes</c>,
/// <c>ProblemTypes</c>); this holds only what the contract does not. It replaced the ATDD run's temporary
/// <c>InvitationScaffold</c>, as story 2.4's <c>LabelScaffold</c> was replaced.
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
    /// <b>Story 4.2 ATDD run, temporary.</b> The revoke route's pattern, as story 4.2 Task 2 names it
    /// (<c>InvitationRoutes.Invitation</c>, Q1 (a)). The dev phase swaps every use for the contract constant and deletes
    /// this, as 4.1's <c>InvitationScaffold</c> was deleted.
    /// </summary>
    public static string RevokeRoute => "/api/v1/spaces/{spaceId:guid}/invitations/{invitationId:guid}";

    /// <summary>
    /// <b>Story 4.2 ATDD run, temporary.</b> One Invitation's path, as a client calls it (<c>InvitationRoutes.InvitationOf</c>
    /// once Task 2 declares it). Built from the contract's collection path, so only the leaf is the scaffold's.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="invitationId">The Invitation.</param>
    /// <returns>The path.</returns>
    public static string RevokePathOf(Guid spaceId, Guid invitationId) =>
        $"{Yello.Contracts.Invitations.InvitationRoutes.InvitationsOf(spaceId)}/{invitationId:D}";

    /// <summary>
    /// <b>Story 4.2 ATDD run, temporary.</b> The revoke's refusal for an Invitation that is accepted, revoked or expired
    /// (Q2 (a)): <c>ProblemTypes.InvitationNotPending</c> once Task 2 declares it.
    /// </summary>
    public static string NotPendingProblemType => "urn:yello:problem:invitation-not-pending";

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
}

/// <summary>
/// One row of story 4.2's Invitation list as the test reads it: the Q5 (a) shape, <c>InvitationResponse</c> once Task 2
/// declares it. Kept in the harness because <c>Yello.Tests.Client</c>'s fake serialises the same shape.
/// </summary>
/// <param name="Id">The Invitation's id, which the revoke names.</param>
/// <param name="EmailAddress">The stored, trimmed address.</param>
/// <param name="Role">The Role's stable code: <c>admin</c>, <c>member</c> or <c>viewer</c>.</param>
/// <param name="IssuerName">The issuer's display name as captured at issue.</param>
/// <param name="IssuedAt">The row's <c>CreatedAt</c>.</param>
public sealed record ListedInvitation(Guid Id, string EmailAddress, string Role, string IssuerName, DateTimeOffset IssuedAt);
