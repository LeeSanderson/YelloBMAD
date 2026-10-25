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
