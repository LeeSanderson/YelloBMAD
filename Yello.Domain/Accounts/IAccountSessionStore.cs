namespace Yello.Domain.Accounts;

/// <summary>
/// Where an Account's Sessions are ended, and where a presented Session is checked against that.
/// FR-2; story 1.7, Lee's decision (a) of 2026-10-24.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two operations on one column, and nothing else.</b> Signing out stamps
/// <see cref="Account.SessionsValidAfter"/>; every authenticated request reads it back and compares
/// the ticket's issue instant. The read is one row by primary key, on the request that presents the
/// ticket - evaluated on read rather than swept (AD-27), and never on a timer (AD-10).
/// </para>
/// <para>
/// <b>A third port rather than a member on <see cref="IAccountCredentialStore"/></b>: sign-in reads a
/// credential, and this reads nothing of the kind. Keeping them apart keeps the email address and
/// the hash out of a read that runs on every request.
/// </para>
/// </remarks>
public interface IAccountSessionStore
{
    /// <summary>
    /// Reads where an Account's Sessions stand.
    /// </summary>
    /// <param name="accountId">The Account a presented Session names.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The standing, or <c>null</c> when no such Account exists - which admits nothing.</returns>
    Task<SessionStanding?> FindAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>
    /// Ends every Session the Account was issued before <paramref name="at"/> - or, when the clock is
    /// not past the Account's stored instant, every Session issued up to and including that instant.
    /// </summary>
    /// <param name="accountId">The Account signing out.</param>
    /// <param name="at">The instant, from the injected clock.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>A task that completes once the instant is stored.</returns>
    Task EndSessionsAsync(Guid accountId, DateTimeOffset at, CancellationToken cancellationToken);
}

/// <summary>
/// Where one Account's Sessions stand: the instant before which they are ended, if any.
/// </summary>
/// <param name="ValidAfter">
/// <see cref="Account.SessionsValidAfter"/>; <c>null</c> when the Account has never signed out.
/// </param>
public sealed record SessionStanding(DateTimeOffset? ValidAfter)
{
    /// <summary>
    /// Whether a Session issued at <paramref name="issuedAt"/> is still good.
    /// </summary>
    /// <param name="issuedAt">The ticket's issue instant, or <c>null</c> when it carries none.</param>
    /// <returns>
    /// <c>true</c> when the Account has never signed out, or the ticket was issued at or after the
    /// last sign-out. A ticket with no issue instant is admitted only while nothing has been ended.
    /// </returns>
    public bool Admits(DateTimeOffset? issuedAt) =>
        ValidAfter is not { } validAfter
        || (issuedAt is { } issued && issued >= validAfter);
}
