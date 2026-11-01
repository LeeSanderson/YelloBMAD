using System.Net.Http.Json;
using System.Text.Json;

namespace Yello.Tests.Shared;

/// <summary>
/// The Invitation route, the problem types and the fixed values story 4.1 adds, stated once on the test side because
/// <c>Yello.Contracts</c> does not hold them yet. Written by the story 4.1 ATDD run (2026-11-01). <b>Temporary</b>: on
/// activation each use is replaced by the contract member (<c>InvitationRoutes.Issue</c>,
/// <c>InvitationRoutes.InvitationsOf</c>, <c>ProblemTypes.AlreadyAMember</c>, <c>ProblemTypes.InvitationPending</c>,
/// <c>ProblemTypes.RoleNotOfferable</c>, <c>ProblemTypes.InvalidEmailAddress</c>) and this file is deleted, as story
/// 2.4's <c>LabelScaffold</c> was.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded against Lee's decisions and the story's recommended options.</b> Q2 (Lee): Owner and Admin may both
/// offer Admin, Member and Viewer, never Owner. Q5 (a): success is <c>204</c> with no body and no <c>Location</c>. Q12
/// (a): a live pending Invitation for the same address is <c>409 invitation-pending</c>. Q10 (a): <c>ExpiresAt</c> is
/// seven days after the database clock at insert (PRD section 12 #9, unconfirmed).
/// </para>
/// <para>
/// <b>Two choices the story left to the dev phase, assumed here and recorded in the ATDD checklist:</b> an unknown or
/// missing Role answers the same <c>400 role-not-offerable</c> as <c>owner</c> (no existing validation type fits, and
/// the client copy "Choose Admin, Member or Viewer." serves all three); an implausible address answers a new
/// <c>400 invalid-email-address</c>, following the one-type-per-field precedent (<c>invalid-space-name</c>,
/// <c>invalid-label-name</c>). If the dev phase decides otherwise, correct this file first and record it.
/// </para>
/// </remarks>
public static class InvitationScaffold
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// <c>POST</c> an Invitation into the Space: story 4.1 Task 5's <c>InvitationRoutes.Issue</c>.
    /// </summary>
    public static string Issue => "/api/v1/spaces/{spaceId:guid}/invitations";

    /// <summary>
    /// The capability the route declares, as the <c>403</c> body names it.
    /// </summary>
    public static string CapabilityCode => "issue-and-revoke-invitations";

    /// <summary>
    /// <c>409</c>: the address already holds a Membership in this Space (AC4). Disclosable: the issuer can see it.
    /// </summary>
    public static string AlreadyAMember => "urn:yello:problem:already-a-member";

    /// <summary>
    /// <c>409</c>: the Space already holds an actionable pending Invitation for the address (AC13, Q12 (a)).
    /// </summary>
    public static string InvitationPending => "urn:yello:problem:invitation-pending";

    /// <summary>
    /// <c>400</c>: the Role is <c>owner</c>, unknown or missing (AC2; assumed for unknown and missing).
    /// </summary>
    public static string RoleNotOfferable => "urn:yello:problem:role-not-offerable";

    /// <summary>
    /// <c>400</c>: the address fails the plausibility rule registration uses (AC3; the type is assumed).
    /// </summary>
    public static string InvalidEmailAddress => "urn:yello:problem:invalid-email-address";

    /// <summary>
    /// The days between issue and expiry (PRD section 12 #9, an assumption every case resting on it traits).
    /// </summary>
    public static int ExpiryDays => 7;

    /// <summary>
    /// The path the email's link opens on the client origin, before the token (AC7). 4.3 builds the route.
    /// </summary>
    public static string LinkPath => "/invitations/";

    /// <summary>
    /// The longest address the plausibility rule admits.
    /// </summary>
    public static int EmailAddressMaxLength => 254;

    /// <summary>
    /// The Space's Invitations: <c>InvitationRoutes.InvitationsOf</c>.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string InvitationsOf(Guid spaceId) => $"/api/v1/spaces/{spaceId:D}/invitations";

    /// <summary>
    /// The request body: exactly <c>emailAddress</c> and <c>role</c> (AC1), the Role as its stable code.
    /// </summary>
    /// <param name="emailAddress">The address, or <c>null</c> to omit nothing but send JSON null.</param>
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
