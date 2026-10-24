using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Yello.Domain.Accounts;

namespace Yello.Host.Sessions;

/// <summary>
/// Makes sign-out end a Session on the server: every presented ticket is checked against the
/// Account's <see cref="Account.SessionsValidAfter"/>, and one issued before it is refused. FR-2 /
/// CAP-2, story 1.4 AC3; story 1.7, Lee's decision (a) of 2026-10-24.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> A Session is an encrypted cookie ticket with no row of its own
/// (<see cref="SessionCookiePolicy"/>), so <c>SignOutAsync</c> alone only tells the browser to drop
/// its copy - a lifted or second copy kept working until the ticket's lifetime ran out. FR-2 says "a
/// Session that has expired or been invalidated grants access to nothing", and AC11 says sign-out
/// "ends the Session on the server". Lee's decision (a): one instant per Account, stamped at
/// sign-out, compared on every request.
/// </para>
/// <para>
/// <b>Evaluated on read, never swept (AD-27), and never on a timer (AD-10).</b> The comparison runs
/// inside the cookie handler's own validation of the ticket, on the request that presents it: one
/// read of one column of one row by primary key. Nothing is cached - a cache here would be a
/// revocation that took effect late - and nothing runs when no request does.
/// </para>
/// <para>
/// <b>The issue instant is the ticket's own, at full precision.</b> The framework's
/// <c>IssuedUtc</c> is serialised to whole seconds, so a sign-in in the same second as a sign-out
/// would compare equal to it and be refused. <see cref="Stamp"/> writes the instant to the ticket's
/// properties in round-trip form; <c>IssuedUtc</c> is the fallback for a ticket issued before this
/// existed.
/// </para>
/// <para>
/// <b>A refused ticket is simply not a Session</b>: the request continues unauthenticated, so a
/// Session-only route answers <c>401</c> and the client's session-end path runs, exactly as for an
/// expired ticket. An Account that no longer exists admits nothing either.
/// </para>
/// </remarks>
internal static class SessionRevocation
{
    /// <summary>
    /// The ticket property the issue instant is written under.
    /// </summary>
    internal const string IssuedAtItem = ".yello.issued";

    /// <summary>
    /// Writes the issue instant onto a ticket's properties, at sign-in.
    /// </summary>
    /// <param name="properties">The properties the ticket is issued with.</param>
    /// <param name="issuedAt">Now, from the injected clock.</param>
    /// <returns>The same properties, for chaining.</returns>
    internal static AuthenticationProperties Stamp(AuthenticationProperties properties, DateTimeOffset issuedAt)
    {
        properties.Items[IssuedAtItem] = issuedAt.ToString("O", CultureInfo.InvariantCulture);

        return properties;
    }

    /// <summary>
    /// The cookie handler's validation hook: refuses a ticket issued before the Account last signed
    /// out.
    /// </summary>
    /// <param name="context">The ticket being validated, on the request that presented it.</param>
    /// <returns>A task that completes once the ticket is admitted or rejected.</returns>
    internal static Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A principal with no Account claim is not a Yello Session, and every endpoint already
        // treats it as unauthenticated - there is nothing to look up.
        return SessionPrincipal.AccountIdOf(context.Principal) is { } accountId
            ? ValidateAccountAsync(context, accountId)
            : Task.CompletedTask;
    }

    private static async Task ValidateAccountAsync(CookieValidatePrincipalContext context, Guid accountId)
    {
        var standing = await context.HttpContext.RequestServices
            .GetRequiredService<IAccountSessionStore>()
            .FindAsync(accountId, context.HttpContext.RequestAborted)
            .ConfigureAwait(false);

        if (standing is null || !standing.Admits(IssuedAt(context.Properties)))
        {
            context.RejectPrincipal();
        }
    }

    private static DateTimeOffset? IssuedAt(AuthenticationProperties properties) =>
        properties.Items.TryGetValue(IssuedAtItem, out var stamped)
        && DateTimeOffset.TryParseExact(stamped, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var issued)
            ? issued
            : properties.IssuedUtc;
}
