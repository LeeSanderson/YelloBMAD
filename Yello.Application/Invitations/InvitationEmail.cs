using System.Globalization;

namespace Yello.Application.Invitations;

/// <summary>
/// The invitation email's template. FR-39, I-14, UJ-2, UX-DR34; story 4.1, AC7, open question 9 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>Its parameter list is its content contract.</b> It takes the Space's name, the Role's display name, the issuer's
/// display name, the link and the expiry - strings, a URI and an instant - and nothing else. No Domain type can be passed
/// in, so no Project, Task, Label, Status, other Member or other Space can reach the email, and neither can the issuer's
/// email address. A gate holds the signature.
/// </para>
/// <para>
/// <b>Plain text, English only for now</b> (ledgered to UX): terse, factual, no exclamation marks
/// (<c>EXPERIENCE.md:99</c>). The expiry is a UTC date, <c>yyyy-MM-dd</c>.
/// </para>
/// </remarks>
public static class InvitationEmail
{
    /// <summary>
    /// Renders the subject and the body.
    /// </summary>
    /// <param name="spaceName">The Space offered.</param>
    /// <param name="roleName">The Role's display name: <c>Admin</c>, <c>Member</c> or <c>Viewer</c>.</param>
    /// <param name="issuerName">The issuer's display name - never their address.</param>
    /// <param name="link">The link carrying only the opaque token.</param>
    /// <param name="expiresAt">When the offer expires.</param>
    /// <returns>The rendered email.</returns>
    public static RenderedEmail Render(string spaceName, string roleName, string issuerName, Uri link, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(link);

        var expiry = DateOnly.FromDateTime(expiresAt.UtcDateTime).ToString("O", CultureInfo.InvariantCulture);

        var subject = $"{issuerName} invited you to {spaceName} on Yello";
        var body =
            $"{issuerName} invited you to join {spaceName} as {roleName}.\n\nOpen this link to see the invitation: {link.OriginalString}\n\nThe link expires on {expiry} (UTC).\n";

        return new RenderedEmail(subject, body);
    }
}

/// <summary>
/// A rendered email's subject and plain-text body.
/// </summary>
/// <param name="Subject">The subject.</param>
/// <param name="Body">The plain-text body.</param>
public sealed record RenderedEmail(string Subject, string Body);
