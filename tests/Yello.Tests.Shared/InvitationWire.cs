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
}
