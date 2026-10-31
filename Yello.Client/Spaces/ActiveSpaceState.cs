using Yello.Contracts.Spaces;

namespace Yello.Client.Spaces;

/// <summary>
/// The Space this tab is in, the server's answer about it, and the purge that runs - synchronously,
/// before any render - whenever the tab leaves it. AC4, AC7, AC10; UX-DR30, UX-DR31, UX-DR39.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE PURGE THIS CLASS PERFORMS IS NEARLY EMPTY TODAY, AND THAT MUST BE READ AS A SEAM RATHER
/// THAN AS COVERAGE</b> - the lesson <c>SessionState</c> records. What it holds is the context bar's
/// own state: the active Space's name and Role, and the requests in flight for it. Epic 2 registers
/// its Board here. So what is asserted is what this seam <i>does</i>: every registration runs, the
/// in-flight requests are cancelled first, one failing registration does not strand the rest, and
/// it runs on a change of Space and on leaving Space routes but not on a navigation within one
/// Space (<c>ActiveSpaceStateTests</c>), and before the destination renders (the browser cases).
/// </para>
/// <para>
/// <b>Synchronous, and called before the router renders.</b> The composition root hooks
/// <c>NavigationManager.RegisterLocationChangingHandler</c>, which runs before the new location is
/// rendered for a link, a <c>NavigateTo</c> and browser Back or Forward alike, and calls
/// <see cref="Navigating"/> from it. Purges are <see cref="Action"/>s rather than tasks for that
/// reason: nothing here may wait for anything before the departed Space is gone.
/// </para>
/// <para>
/// <b>Requests are linked to the Space as well as to the Session.</b> A Space-scoped request is
/// issued with <see cref="Token"/>, which <c>YelloApiHandler</c> links to the Session's, so an answer
/// about the departed Space cannot land after the purge (UX-DR30 by the other door).
/// </para>
/// <para>
/// <b>Every client holder of Space-scoped state, as surveyed by story 3.2 (AC11).</b> Each one either registers with
/// <see cref="RegisterPurge"/> or is a component keyed on the Space and rendered only inside an arrived
/// <c>SpaceArrival</c>, so a change of Space disposes it:
/// </para>
/// <list type="bullet">
/// <item>Registered here: <c>BoardView</c> (its replicas, Labels, Status options, open Task, pending deletes and moves,
/// held-out pages), <c>TaskListView</c> (its rows and <c>TaskListFilters</c>), <c>LabelSection</c>, <c>ProjectList</c>,
/// and <c>SpaceNarration</c> (a pending announcement, registered by <c>Program</c>).</item>
/// <item>Keyed and arrival-gated: <c>BoardView</c> and <c>TaskListView</c> (<c>@key</c> on the Space and Project), and
/// <c>ProjectList</c>, <c>SpaceNameSection</c> and <c>LabelSection</c> (<c>@key</c> on the Space); their children
/// (<c>TaskDetail</c>, <c>TaskCreate</c>, <c>MovePicker</c>, cards and rows) go with them.</item>
/// <item>Cleared through <see cref="Changed"/>, which <see cref="Purge"/> raises: <c>ContextBar</c>'s displayed Space name,
/// Role chip, Project name and pending switch announcement (<c>_announcement</c>).</item>
/// <item>Page-level fields of a page reused across Spaces reset on a change of <c>SpaceId</c>: <c>SpaceArrival</c>'s
/// arrival state, <c>SpaceSettings</c>' permission state (story 2.4) and <c>SpaceHome</c>'s narration (story 3.2).</item>
/// <item>Not Space-scoped, kept across a switch by design: <c>SpaceDirectory</c> and <c>ContextBar</c>'s copy of it
/// (Account-scoped, dropped on Session end), and <c>PageHeadingFocus</c> (an element reference, no content).</item>
/// <item>Session-scoped, purged on sign-out and Session end through <c>SessionState.RegisterPurge</c>: this seam and
/// <c>SpaceDirectory</c> (registered by <c>Program</c>), <c>AccountSettings</c>' Tokens and <c>CreateSpace</c>'s form.</item>
/// </list>
/// <para>
/// <b>Epic 7 registers the rest of UX-DR31 with this seam</b> (stories 7.3 and 7.7): the replica, its projections, the
/// queued inbound frames and lease invalidation (<c>MembershipChanged</c>) as a purge trigger. The FR-33 durable buffer
/// lives in memory only. No second purge mechanism is added for any of them.
/// </para>
/// <para>
/// <b>Never in browser storage, and shared with no other tab</b> (<c>EXPERIENCE.md:93</c>): scoped
/// services are per tab in WebAssembly, and nothing here reaches a <c>BroadcastChannel</c> or a
/// storage event. This class takes no framework type, so the ordering above is testable without a
/// browser.
/// </para>
/// </remarks>
public sealed class ActiveSpaceState : IDisposable
{
    private readonly List<Registration> _purges = [];

    private CancellationTokenSource _inFlight = new();

    private bool _switching;

    /// <summary>
    /// Raised after every purge and every arrival, so the context bar can redraw.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// The Space this tab is in or on its way into, or <c>null</c> on a route outside any Space.
    /// </summary>
    public Guid? SpaceId { get; private set; }

    /// <summary>
    /// The server's answer about <see cref="SpaceId"/> on this load, or <c>null</c> until it arrives.
    /// The context bar renders a Space name from this and from nothing else (AC10).
    /// </summary>
    public ActiveSpaceResponse? Current { get; private set; }

    /// <summary>
    /// The name of the Project whose Board the tab is showing, from that Board's own answer on this load,
    /// or <c>null</c> off a Board (story 2.2; DESIGN.md:454). The context bar renders it after the Space
    /// name, and from nothing else - never from Space home's list.
    /// </summary>
    public string? ProjectName { get; private set; }

    /// <summary>
    /// How many answers have arrived - so a listener can tell a new arrival from a redraw.
    /// </summary>
    public int Arrivals { get; private set; }

    /// <summary>
    /// Whether the last arrival ended a switch from another Space, rather than a first arrival in
    /// this tab - the case UX-DR39 moves focus to the context bar for.
    /// </summary>
    public bool LastArrivalWasSwitch { get; private set; }

    /// <summary>
    /// The cancellation token every request about <see cref="SpaceId"/> is issued with. Cancelled by
    /// the next purge.
    /// </summary>
    public CancellationToken Token => _inFlight.Token;

    /// <summary>
    /// Registers something Space-scoped to be discarded when the tab leaves its Space.
    /// </summary>
    /// <param name="purge">What to discard. Synchronous, and run before the destination renders.</param>
    /// <returns>A handle that unregisters it.</returns>
    public IDisposable RegisterPurge(Action purge)
    {
        ArgumentNullException.ThrowIfNull(purge);

        var registration = new Registration(this, purge);

        _purges.Add(registration);

        return registration;
    }

    /// <summary>
    /// The tab is about to show <paramref name="target"/> - a Space, or <c>null</c> for a route
    /// outside any Space. Purges the departed Space unless the target is the same one.
    /// </summary>
    /// <param name="target">The destination's Space, read from its route.</param>
    public void Navigating(Guid? target)
    {
        if (target == SpaceId)
        {
            return;
        }

        var switching = SpaceId is not null && target is not null;

        Purge();

        SpaceId = target;
        _switching = switching;
    }

    /// <summary>
    /// Discards everything about the current Space, synchronously: cancels its requests first, then
    /// runs every registration, then forgets the answer. On a change of Space, on leaving Space
    /// routes, on sign-out and when the Session ends.
    /// </summary>
    public void Purge()
    {
        // FIRST, so that nothing already on the wire can deliver the departed Space's answer after
        // it is gone - SessionState's order, for the same reason.
        var cancelling = _inFlight;

        _inFlight = new CancellationTokenSource();

        cancelling.Cancel();
        cancelling.Dispose();

        Current = null;
        ProjectName = null;
        SpaceId = null;
        _switching = false;

        foreach (var registration in _purges.ToArray())
        {
            try
            {
                registration.Purge();
            }
            // Every exception type, for the reason SessionState gives: a purge is "discard what you
            // are holding", and no failure of one may strand the rest on screen.
#pragma warning disable S2221 // "Catch a list of specific exception subtype or use exception filters"
            catch (Exception purgeFailure) when (purgeFailure is not OutOfMemoryException)
#pragma warning restore S2221
            {
                _ = purgeFailure;
            }
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Records the server's answer about the Space the tab is in.
    /// </summary>
    /// <param name="answer">The answer.</param>
    /// <returns>
    /// <c>true</c> when it was accepted - it is about the Space the tab is still in - and
    /// <c>false</c> for an answer that arrived after the tab moved on, which is dropped.
    /// </returns>
    public bool Arrive(ActiveSpaceResponse answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        if (answer.Id != SpaceId)
        {
            return false;
        }

        Current = answer;
        LastArrivalWasSwitch = _switching;
        _switching = false;
        Arrivals++;

        Changed?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>
    /// Records the server's answer to a rename of the Space the tab is in (story 3.1, AC14, open question 7 (a)): the
    /// context bar redraws with the new name, and nothing else happens - it is not an arrival, so it does not count as a
    /// switch and moves no focus.
    /// </summary>
    /// <param name="answer">The rename's answer: the Space's id, its new name and the caller's Role.</param>
    /// <returns><c>false</c>, and nothing recorded, for an answer about a Space the tab has left.</returns>
    public bool Renamed(ActiveSpaceResponse answer)
    {
        ArgumentNullException.ThrowIfNull(answer);

        if (answer.Id != SpaceId || Current is null)
        {
            return false;
        }

        Current = answer;
        Changed?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>
    /// The tab is about to land in a Space it has just created (story 3.1, AC13): the arrival counts as a switch, so focus
    /// moves to the context bar and the new Space and Role are announced (UX-DR39) - although the create page it leaves
    /// is in no Space. Call before navigating there; the location-changing hook then finds the tab already in it.
    /// </summary>
    /// <param name="spaceId">The created Space.</param>
    public void ArrivingInCreated(Guid spaceId)
    {
        Navigating(spaceId);
        _switching = true;
    }

    /// <summary>
    /// Records the name of the Project whose Board has just answered, for the context bar.
    /// </summary>
    /// <param name="spaceId">The Space the Board answer is about.</param>
    /// <param name="name">The Project's name, as that answer states it.</param>
    /// <returns><c>false</c>, and nothing recorded, for an answer about a Space the tab has left.</returns>
    public bool ShowProject(Guid spaceId, string name)
    {
        if (spaceId != SpaceId)
        {
            return false;
        }

        ProjectName = name;
        Changed?.Invoke(this, EventArgs.Empty);

        return true;
    }

    /// <summary>
    /// The tab has left the Board: the context bar names no Project.
    /// </summary>
    public void ClearProject()
    {
        if (ProjectName is null)
        {
            return;
        }

        ProjectName = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Dispose() => _inFlight.Dispose();

    private sealed class Registration(ActiveSpaceState owner, Action purge) : IDisposable
    {
        public Action Purge => purge;

        public void Dispose() => owner._purges.Remove(this);
    }
}
