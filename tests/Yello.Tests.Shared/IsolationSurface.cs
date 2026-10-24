namespace Yello.Tests.Shared;

/// <summary>
/// The surfaces every isolation and revocation case runs on (AD-4): what the browser sends, and what a
/// script sends.
/// </summary>
/// <remarks>
/// <para>
/// <b><see cref="Session"/></b> is the Session cookie, plus the anti-forgery header on a
/// state-changing request - exactly what the client sends (<c>TESTING-CONVENTIONS.md</c>'s "HTTP"
/// surface; <c>Surface.Browser</c> in the test design's Appendix A). Q3 (a): the real browser against
/// the real Host is a <c>Yello.Tests.Client</c> collection, not a third member here.
/// </para>
/// <para>
/// <b><see cref="ApiToken"/></b> is a bearer Token and nothing else: no cookie, no anti-forgery header.
/// </para>
/// <para>
/// <b>Adding a member is a loud change by design.</b> Story 7.3 adds <c>/sync</c> as a third member
/// (Q7); every case list's gap gate then demands an expectation for it on every row.
/// </para>
/// </remarks>
public enum IsolationSurface
{
    /// <summary>
    /// The Session cookie, as the browser sends it.
    /// </summary>
    Session,

    /// <summary>
    /// A bearer API Token, as a script sends it.
    /// </summary>
    ApiToken,
}
