using System.Net;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Yello.Contracts.Accounts;

namespace Yello.Client.Sessions;

/// <summary>
/// Everything every call to the Host needs and no page should have to remember: the cookie on the
/// wire, the anti-forgery token in the header, and what to do about a 401. AC3, AC5, AD-7 / AR-14.
/// </summary>
/// <remarks>
/// <para>
/// <b>A cross-origin cookie is not sent unless the request asks for it.</b> <c>fetch</c> defaults
/// to <c>credentials: "same-origin"</c>, and the client and the Host are distinct origins - so
/// without <see cref="BrowserRequestCredentials.Include"/> the Session cookie is simply never sent
/// and every authenticated request is anonymous. Story 1.3 registered a bare
/// <c>HttpClient</c> with no handler at all, which was correct for a story whose only call was
/// unauthenticated; this is the handler that story's comment predicted.
/// </para>
/// <para>
/// <b>Set on every request rather than on the authenticated ones.</b> The alternative - deciding
/// per route - is a list that a later story adds a route to and forgets, and the cost of including
/// credentials on a request that needs none is nothing. The <i>server</i> decides what a cookie
/// grants, per request, which is AD-1.
/// </para>
/// <para>
/// <b>The 401 handling deliberately excludes sign-in itself.</b>
/// <c>POST /api/v1/sessions</c> answers 401 for a wrong password, and treating that as "your
/// Session ended" would purge and navigate in the middle of somebody typing their password wrongly
/// - announcing an event that did not happen. A 401 from anywhere else, to a client that believed
/// it held a Session, is the event AC3 describes.
/// </para>
/// <para>
/// <b>What is honest to claim about that path.</b> Since story 1.7 the client makes authenticated
/// requests - the Space switcher and a Space's own answer - so a Session that ends under a person
/// reaches this branch from a real surface. The seam tests drive it directly, and the browser
/// suite drives it twice: against a fake at the network edge, and against the real Host
/// (<c>RealHostBrowserTests</c>), whose real 401 lands the running client on sign-in with the
/// reason stated.
/// </para>
/// </remarks>
/// <param name="session">The client's Session state, and the purge seam.</param>
public sealed class YelloApiHandler(SessionState session) : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Split from the body below because S4457 requires an argument check to run eagerly rather
        // than on the first await of a state machine.
        return SendCoreAsync(request, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // The browser attaches and stores the cookie; nothing in this client ever sees it, which is
        // what HttpOnly buys and what keeps AC5's web-storage ban from depending on restraint.
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        if (RequiresAntiforgeryToken(request) && session.AntiforgeryRequestToken is { } token)
        {
            request.Headers.TryAddWithoutValidation(Antiforgery.HeaderName, token);
        }

        // LINKED TO THE SESSION, so that a request still on the wire when the Session ends is
        // abandoned rather than allowed to resolve against a client that has just been purged.
        // Without this the purge-before-announce ordering closes only the queued-frame case, and a
        // response arriving a moment later delivers Space-scoped content anyway - the same
        // disclosure UX-DR30 forbids, reached by the other door. Found at code review on 2026-09-19.
        using var linked = session.LinkToSession(cancellationToken);

        var response = await base.SendAsync(request, linked.Token).ConfigureAwait(false);

        if (IsSessionEnded(request, response))
        {
            await session.SessionEndedAsync().ConfigureAwait(false);
        }

        return response;
    }

    /// <summary>
    /// Whether a request changes state, and so must carry an anti-forgery token (AR-14).
    /// </summary>
    /// <remarks>
    /// Phrased as "everything except the safe methods" rather than as a list of the unsafe ones, so
    /// a verb a later story reaches for is covered by default. The safe set is RFC 9110's.
    /// </remarks>
    private static bool RequiresAntiforgeryToken(HttpRequestMessage request) =>
        request.Method != HttpMethod.Get
        && request.Method != HttpMethod.Head
        && request.Method != HttpMethod.Options
        && request.Method != HttpMethod.Trace;

    private bool IsSessionEnded(HttpRequestMessage request, HttpResponseMessage response) =>
        response.StatusCode == HttpStatusCode.Unauthorized
        && session.IsSignedIn
        && !IsAuthenticationAttempt(request);

    private static bool IsAuthenticationAttempt(HttpRequestMessage request) =>
        request.RequestUri is { } uri
        && uri.AbsolutePath.EndsWith(SessionRoutes.Authenticate, StringComparison.Ordinal);
}
