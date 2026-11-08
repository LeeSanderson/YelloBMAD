using System.Globalization;
using Yello.Application.Invitations;

namespace Yello.Application.Spaces;

/// <summary>
/// The Ownership Offer email's template: the one notification the recipient of an offer receives. FR-43, I-14.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its parameter list is its content contract.</b> It takes the Space's name, the offerer's display name, the link and the
/// expiry - strings, a URI and an instant - and nothing else. No Domain type can be passed in, so nothing of the Space's
/// contents, its other Memberships or another Space can reach the email: delivery cannot be recalled, so anything it carried
/// would outlive the Membership that justified it. A gate holds the signature.
/// </para>
/// <para>
/// <b>It carries no means of accepting</b>: the link opens the client with no path, no id and no token. Acceptance happens
/// inside the Space, and an id in a link that can be forwarded would tell its reader that the Space exists.
/// </para>
/// <para>
/// <b>Plain text, English only for now</b>, terse and factual, with no exclamation marks, as the Invitation's.
/// </para>
/// </remarks>
public static class OwnershipOfferEmail
{
    /// <summary>
    /// Renders the subject and the body.
    /// </summary>
    /// <param name="spaceName">The Space offered.</param>
    /// <param name="offererName">The offering Owner's display name, never their address.</param>
    /// <param name="link">The client's address, with no path naming a Space or an offer.</param>
    /// <param name="expiresAt">When the offer stops being open, by the database clock.</param>
    /// <returns>The rendered email.</returns>
    public static RenderedEmail Render(string spaceName, string offererName, Uri link, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(link);

        var utc = expiresAt.UtcDateTime;

        // The invariant culture's short time is HH:mm, so this reads 2026-11-15 09:30.
        var expiry = DateOnly.FromDateTime(utc).ToString("O", CultureInfo.InvariantCulture) + " " + TimeOnly.FromDateTime(utc).ToString("t", CultureInfo.InvariantCulture);

        var subject = $"{offererName} offered you ownership of {spaceName} on Yello";
        var body =
            $"{offererName} has offered you ownership of {spaceName} on Yello.\n\n"
            + $"You stay as you are until you answer. To accept or decline, sign in to Yello and open {spaceName}.\n\n"
            + $"The offer is open until {expiry} UTC.\n\n"
            + $"Open Yello: {link.OriginalString}\n";

        return new RenderedEmail(subject, body);
    }
}
