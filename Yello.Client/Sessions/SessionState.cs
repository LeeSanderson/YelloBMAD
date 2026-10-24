namespace Yello.Client.Sessions;

/// <summary>
/// What the client knows about its own Session, and the seam every Space-scoped surface registers
/// its purge with. AC3, AC6, UX-DR30, UX-DR31.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE PURGE THIS CLASS PERFORMS IS STILL NEARLY EMPTY, AND THAT MUST BE READ AS A SEAM RATHER
/// THAN AS COVERAGE.</b> Since story 1.7 two things register with it: the active Space's state
/// (<c>ActiveSpaceState</c>, which holds the context bar's name and Role and is where epic 2's Board
/// will register), and the Space switcher's list (<c>SpaceDirectory</c>). There is still no replica,
/// no cached Board and no filter state. What is asserted is what this seam <i>does</i>: that
/// registrations run, that they run before the announcement, and that a failing one does not strand
/// the rest - for a Session that ends under the person and for one they end themselves.
/// </para>
/// <para>
/// <b>Purge before announce (UX-DR30), and the order is the point.</b> Reversing it renders a
/// queued frame one tick after the announcement, disclosing a Space the Account no longer belongs
/// to. The order is asserted rather than commented, because a comment is not a gate.
/// </para>
/// <para>
/// <b>Nothing is ever written to <c>localStorage</c>, <c>sessionStorage</c> or IndexedDB.</b> AC5
/// bans a credential in the first two; UX-DR31 extends the ban to Space-scoped <i>content</i> in
/// all three. There is no browser-storage interop anywhere in this client, and
/// <c>ClientStorageBanTests</c> keeps it that way. The anti-forgery token below lives in a field
/// for exactly that reason - a field dies with the tab, which is what it should do.
/// </para>
/// <para>
/// <b>This class takes no framework type, deliberately.</b> No <c>NavigationManager</c>, no
/// <c>HttpClient</c>, no <c>IJSRuntime</c>. Announcing a session's end is a callback the
/// composition root supplies, which is what lets the ordering above be asserted by an ordinary
/// test rather than by a browser - and what stops this becoming the place navigation decisions
/// accumulate.
/// </para>
/// <para>
/// <b>AC6, and the limit on what this story can hold.</b> "Locally-held unsent input is never
/// replayed under a new Session" is a rule about an editor and unsent input, neither of which
/// exists until epic 7. What this class can honestly provide is the rule and the absence of any
/// code path that would break it: nothing here stores a request, nothing re-sends one, and
/// <see cref="SessionEndedAsync"/> discards rather than queues. Stated as a limit rather than
/// implied as coverage.
/// </para>
/// </remarks>
public sealed class SessionState : IDisposable
{
    private readonly List<Registration> _purges = [];

    private Func<Task>? _announce;

    private Func<Task>? _announceSignOut;

    /// <summary>
    /// Cancelled when the Session ends, so that requests already on the wire do not deliver
    /// Space-scoped content <i>after</i> the purge that was supposed to discard it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>UX-DR30 arriving by the other door.</b> The purge-before-announce ordering closes the case
    /// where a queued frame renders one tick late. It does nothing about request R1, already in
    /// flight, whose continuation resolves normally against a component that has just been purged -
    /// same disclosure, different route. Found at code review on 2026-09-19.
    /// </para>
    /// <para>
    /// Replaced rather than reused after a cancellation, because a cancelled source stays cancelled:
    /// signing in again on the same client would otherwise start every request pre-cancelled.
    /// </para>
    /// </remarks>
    private CancellationTokenSource _inFlight = new();

    /// <summary>
    /// Whether this client believes it holds a Session.
    /// </summary>
    /// <remarks>
    /// <b>A belief, not a fact, and nothing is authorised by it.</b> The Session itself is an
    /// <c>HttpOnly</c> cookie this code cannot read; this flag only records that a sign-in
    /// succeeded, so that a 401 can be told apart from "was never signed in" when deciding whether
    /// a person should be told their Session <i>ended</i>. Every real decision is the server's, per
    /// request. That is the same rule UX-DR31 states as "optimistic rendering never precedes
    /// authorisation on the first read of a Space", and the reason Blazor's own guidance warns that
    /// <c>AuthorizeView</c> content is discoverable in a WebAssembly bundle without authentication.
    /// </remarks>
    public bool IsSignedIn { get; private set; }

    /// <summary>
    /// The anti-forgery request token this client sends with every state-changing request, held in
    /// memory and nowhere else.
    /// </summary>
    public string? AntiforgeryRequestToken { get; private set; }

    /// <summary>
    /// Wires what happens after a purge - in the composition root, once.
    /// </summary>
    /// <param name="announce">
    /// Returns the User to sign-in with the reason stated. Runs strictly after every purge.
    /// </param>
    public void AnnounceSessionEndWith(Func<Task> announce) => _announce = announce;

    /// <summary>
    /// Wires where a sign-out lands - in the composition root, once. Story 1.7, open question 4.
    /// </summary>
    /// <param name="announce">
    /// Returns the person to sign-in with no reason stated: they chose this, and nothing ended under
    /// them. Runs strictly after every purge, exactly as <see cref="AnnounceSessionEndWith"/>'s does.
    /// </param>
    public void AnnounceSignOutWith(Func<Task> announce) => _announceSignOut = announce;

    /// <summary>
    /// Records that a sign-in succeeded - and drops the anti-forgery token, deliberately.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>AN ANTI-FORGERY TOKEN IS BOUND TO THE IDENTITY IT WAS ISSUED TO.</b> ASP.NET Core embeds
    /// the authenticated user in the token pair, so a token minted while anonymous - which is what
    /// the sign-in request itself carried - is <i>refused</i> on the next request, now that there
    /// is a Session. Keeping it would make the first state-changing request after every sign-in
    /// fail with a 400 that says nothing about why.
    /// </para>
    /// <para>
    /// <b>The symptom, if this line is ever removed:</b> a sign-in that appears to work, followed
    /// by a <c>400</c> on the next thing the person does, with a problem <c>type</c> about an
    /// anti-forgery token and nothing on screen explaining why. It is not a theoretical failure
    /// mode - it is what this client did until
    /// <c>SessionSeamTests.Signing_in_drops_the_token_that_was_issued_to_an_anonymous_caller</c>
    /// was written.
    /// </para>
    /// <para>
    /// Dropping it rather than fetching a new one here keeps this class free of HTTP:
    /// <c>AntiforgeryTokenSource.EnsureTokenAsync</c> fetches on demand and is already called
    /// before every submission, so a null here simply means the next one fetches.
    /// </para>
    /// </remarks>
    public void SignedIn()
    {
        IsSignedIn = true;
        AntiforgeryRequestToken = null;
    }

    /// <summary>
    /// Holds an anti-forgery token without claiming a Session exists.
    /// </summary>
    /// <remarks>
    /// Registration needs a token and is unauthenticated, so the two facts are set separately.
    /// </remarks>
    /// <param name="antiforgeryRequestToken">The token from the anti-forgery endpoint.</param>
    public void HoldAntiforgeryToken(string? antiforgeryRequestToken) =>
        AntiforgeryRequestToken = antiforgeryRequestToken;

    /// <summary>
    /// Registers something to be discarded when a Session ends.
    /// </summary>
    /// <param name="purge">What to discard. Runs before anything is announced.</param>
    /// <returns>
    /// A handle that unregisters it. A component that registers on initialisation and disposes
    /// the handle cannot leave a callback behind that reaches into a component that is gone -
    /// which is the leak this shape exists to prevent, and the reason it is not a bare event.
    /// </returns>
    public IDisposable RegisterPurge(Func<Task> purge)
    {
        ArgumentNullException.ThrowIfNull(purge);

        var registration = new Registration(this, purge);

        _purges.Add(registration);

        return registration;
    }

    /// <summary>
    /// Ends the Session on this client: purge everything registered, then return the User to
    /// sign-in with the reason stated.
    /// </summary>
    /// <remarks>
    /// <b>The purge is awaited to completion before the announcement is reached</b>, and a purge
    /// that throws does not stop the others or the announcement. A half-purged client that stayed
    /// where it was would be strictly worse than either outcome: it is the state UX-DR30 describes,
    /// with Space-scoped content still on screen.
    /// </remarks>
    public Task SessionEndedAsync() => EndAsync(_announce);

    /// <summary>
    /// Ends the Session on this client because the person signed out: the same purge as
    /// <see cref="SessionEndedAsync"/>, differing only in where it lands. AC7, AC11.
    /// </summary>
    /// <remarks>
    /// Called after the server has been asked to end the Session (<c>DELETE /api/v1/sessions</c>), so
    /// a lifted copy of the cookie is refused too. One purge implementation for both, deliberately: a
    /// second would be a second place to forget a registration.
    /// </remarks>
    /// <returns>A task that completes once everything is purged and the landing is announced.</returns>
    public Task SignedOutAsync() => EndAsync(_announceSignOut);

    private async Task EndAsync(Func<Task>? announce)
    {
        IsSignedIn = false;
        AntiforgeryRequestToken = null;

        // FIRST, so that nothing already on the wire can deliver Space-scoped content after the
        // purge below has discarded it. Swapped before cancelling, so a request starting on another
        // continuation right now links to the fresh source rather than to one about to be cancelled.
        var cancelling = _inFlight;

        _inFlight = new CancellationTokenSource();

        await cancelling.CancelAsync().ConfigureAwait(false);

        cancelling.Dispose();

        try
        {
            // A copy, because a purge may unregister itself - which is the ordinary shape for a
            // component disposing during teardown, and would otherwise mutate the list being walked.
            foreach (var registration in _purges.ToArray())
            {
                try
                {
                    await registration.Purge().ConfigureAwait(false);
                }
                // S2221 disabled here, deliberately and narrowly, following the same reasoning as
                // StartupConnectivityCheck. An allow-list IS the defect this catch was widened to
                // fix: it listed one type, the test planted that same type, and every realistic
                // teardown failure fell outside it.
#pragma warning disable S2221 // "Catch a list of specific exception subtype or use exception filters"
                catch (Exception purgeFailure) when (purgeFailure is not OutOfMemoryException)
#pragma warning restore S2221
                {
                    // EVERY exception type, and the narrowing is the defect rather than the fix.
                    // This caught only InvalidOperationException until code review on 2026-09-19,
                    // while the remarks above promised - unconditionally - that "a purge that throws
                    // does not stop the others or the announcement". The failures a purge actually
                    // meets during teardown are JSDisconnectedException from an interop call,
                    // ObjectDisposedException, TaskCanceledException from a request this very method
                    // just cancelled, and HttpRequestException - none of them InvalidOperationException,
                    // and the test that proved the behaviour planted the one type that was caught.
                    //
                    // A purge is "discard what you are holding". There is no failure of one that
                    // should be allowed to strand the rest, because the state that leaves on screen
                    // is exactly what UX-DR30 forbids. The single exclusion above is a process-level
                    // failure that nothing here could meaningfully continue through anyway.
                    _ = purgeFailure;
                }
            }
        }
        finally
        {
            // IN A FINALLY, because the announcement is what takes the person off the Space-scoped
            // surface. Reaching it only when every purge returned normally made the announcement
            // conditional on the thing most likely to fail - so the one case that needed the
            // navigation most was the one that skipped it.
            if (announce is not null)
            {
                await announce().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Links a request's own cancellation to this Session's, so it is abandoned if the Session ends
    /// while it is in flight.
    /// </summary>
    /// <param name="caller">The cancellation token the caller supplied.</param>
    /// <returns>
    /// A linked source the caller must dispose. Linked rather than replaced, so a caller that
    /// cancels its own request still does.
    /// </returns>
    public CancellationTokenSource LinkToSession(CancellationToken caller) =>
        CancellationTokenSource.CreateLinkedTokenSource(caller, _inFlight.Token);

    /// <inheritdoc />
    public void Dispose() => _inFlight.Dispose();

    private sealed class Registration(SessionState owner, Func<Task> purge) : IDisposable
    {
        public Func<Task> Purge => purge;

        public void Dispose() => owner._purges.Remove(this);
    }
}
