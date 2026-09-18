namespace Yello.Client.Sessions;

/// <summary>
/// What the client knows about its own Session, and the seam every Space-scoped surface registers
/// its purge with. AC3, AC6, UX-DR30, UX-DR31.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE PURGE THIS CLASS PERFORMS IS VERY NEARLY EMPTY TODAY, AND THAT MUST BE READ AS A SEAM
/// RATHER THAN AS COVERAGE.</b> There is no replica, no cached Board, no filter state, no Space
/// switcher and no Space-scoped client state of any kind - the client holds three fields in one
/// component. A test asserting "everything Space-scoped is purged" would pass against a system with
/// nothing to purge, which is the vacuous-gate defect this repository keeps rediscovering. What is
/// asserted instead is what this seam <i>does</i>: that registrations run, that they run before the
/// announcement, and that a failing one does not strand the rest. Stories 1.7 and epic 2 extend it
/// to Space switch, sign-out and the Board.
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
public sealed class SessionState
{
    private readonly List<Registration> _purges = [];

    private Func<Task>? _announce;

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
    public async Task SessionEndedAsync()
    {
        IsSignedIn = false;
        AntiforgeryRequestToken = null;

        // A copy, because a purge may unregister itself - which is the ordinary shape for a
        // component disposing during teardown, and would otherwise mutate the list being walked.
        foreach (var registration in _purges.ToArray())
        {
            try
            {
                await registration.Purge().ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                // A component torn down mid-purge. Deliberately swallowed rather than allowed to
                // abandon the remaining purges and the announcement: the failure mode of stopping
                // here is Space-scoped content left on screen, which is what UX-DR30 forbids.
            }
        }

        if (_announce is not null)
        {
            await _announce().ConfigureAwait(false);
        }
    }

    private sealed class Registration(SessionState owner, Func<Task> purge) : IDisposable
    {
        public Func<Task> Purge => purge;

        public void Dispose() => owner._purges.Remove(this);
    }
}
