using System.Globalization;
using System.Resources;
using Mono.Cecil;
using Xunit;
using Yello.Client.Localisation;
using Yello.Client.Pages;
using Yello.Client.Sessions;
using Yello.Client.Spaces;
using Yello.Contracts.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// Story 1.7's client seams, asserted for what they DO: the active Space's synchronous purge, the
/// sign-out entry that shares the Session's purge, and the context bar's accessible name. AC4, AC6,
/// AC7, AC11; UX-DR28, UX-DR30, UX-DR31.
/// </summary>
/// <remarks>
/// <para>
/// <b>In this suite because it is the only one that references <c>Yello.Client</c></b>, as
/// <c>SessionSeamTests</c> and <c>RefusalCopyTests</c> are. Neither seam takes a framework type, so
/// the ordering is assertable without a browser; the browser cases in <c>SpaceSwitchingTests</c>
/// assert that the purge runs before the destination renders.
/// </para>
/// <para>
/// <b>The seam is nearly empty today</b> - it holds the context bar's own state, and epic 2 registers
/// its Board - so nothing here asserts "everything Space-scoped is purged" over a system with almost
/// nothing to purge. It asserts that registrations run, in which order relative to cancellation, and
/// on which navigations.
/// </para>
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-9")]
[Trait("Requirement", "UX-DR31")]
public sealed class ActiveSpaceStateTests
{
    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();

    private static readonly ResourceManager Copy = new(ClientCopy.ResourceName, typeof(ClientCopy).Assembly);

    /// <summary>
    /// AC7 - a purge runs every registration, and cancels the departed Space's requests before any
    /// of them runs.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR30")]
    public void A_purge_cancels_requests_first_and_then_runs_every_registration()
    {
        using var state = InSpace(Northwind);

        var inFlight = state.Token;
        var seen = new List<string>();

        using var first = state.RegisterPurge(() => seen.Add("first:" + inFlight.IsCancellationRequested));
        using var second = state.RegisterPurge(() => seen.Add("second:" + inFlight.IsCancellationRequested));

        state.Purge();

        Assert.Equal(["first:True", "second:True"], seen);
        Assert.Null(state.Current);
        Assert.Null(state.SpaceId);
        Assert.False(state.Token.IsCancellationRequested);
    }

    /// <summary>
    /// AC7 - a registration that throws does not stop the rest, or the answer being forgotten.
    /// </summary>
    [Fact]
    public void A_purge_that_fails_does_not_strand_the_rest()
    {
        using var state = InSpace(Northwind);

        var ran = false;

        using var failing = state.RegisterPurge(() => throw new ObjectDisposedException("board"));
        using var after = state.RegisterPurge(() => ran = true);

        state.Purge();

        Assert.True(ran);
        Assert.Null(state.Current);
    }

    /// <summary>
    /// AC7 - moving to another Space purges the departed one; staying within one Space purges
    /// nothing; leaving Space routes altogether purges too.
    /// </summary>
    [Fact]
    public void Only_a_change_of_Space_or_leaving_Space_routes_purges()
    {
        using var state = InSpace(Northwind);

        var purges = 0;

        using var registration = state.RegisterPurge(() => purges++);

        // Anchor: arriving in a Space from outside one is a navigation, and purges what little there is.
        state.Navigating(Northwind);
        Assert.Equal(0, purges);
        Assert.NotNull(state.Current);

        state.Navigating(Harbour);
        Assert.Equal(1, purges);
        Assert.Null(state.Current);
        Assert.Equal(Harbour, state.SpaceId);

        state.Navigating(null);
        Assert.Equal(2, purges);
        Assert.Null(state.SpaceId);
    }

    /// <summary>
    /// AC10 / UX-DR30 - an answer about a Space the tab has already left is dropped, never rendered.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR30")]
    public void An_answer_about_a_departed_Space_is_dropped()
    {
        using var state = new ActiveSpaceState();

        state.Navigating(Northwind);
        state.Navigating(Harbour);

        Assert.False(state.Arrive(Answer(Northwind, "owner")));
        Assert.Null(state.Current);

        Assert.True(state.Arrive(Answer(Harbour, "viewer")));
        Assert.Equal("viewer", state.Current?.Role);
    }

    /// <summary>
    /// AC4 / UX-DR39 - an arrival that ends a switch from another Space is told apart from a first
    /// arrival, which is what decides whether focus goes to the context bar or to the page's heading.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR39")]
    public void A_switch_is_told_apart_from_a_first_arrival()
    {
        using var state = new ActiveSpaceState();

        state.Navigating(Northwind);
        state.Arrive(Answer(Northwind, "owner"));

        Assert.False(state.LastArrivalWasSwitch);

        state.Navigating(Harbour);
        state.Arrive(Answer(Harbour, "viewer"));

        Assert.True(state.LastArrivalWasSwitch);
        Assert.Equal(2, state.Arrivals);
    }

    /// <summary>
    /// AC7 / AC11 - signing out runs the same purge an ended Session does, and lands where sign-out
    /// lands - not where an ended Session does.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-2")]
    public async Task Signing_out_runs_the_Sessions_purge_and_lands_without_a_reason()
    {
        using var session = new SessionState();

        var events = new List<string>();

        session.AnnounceSessionEndWith(() => RecordAsync(events, "session-ended"));
        session.AnnounceSignOutWith(() => RecordAsync(events, "signed-out"));

        using var purge = session.RegisterPurge(() => RecordAsync(events, "purged"));

        session.SignedIn();

        await session.SignedOutAsync();

        Assert.Equal(["purged", "signed-out"], events);
        Assert.False(session.IsSignedIn);
    }

    /// <summary>
    /// AC6 / UX-DR28 - the context bar's accessible name states each Role and what it permits, with
    /// the Role in sentence case.
    /// </summary>
    [Theory]
    [InlineData("owner", "Northwind Redesign — Owner, can manage everything in this Space, including ownership")]
    [InlineData("admin", "Northwind Redesign — Admin, can manage Members and settings")]
    [InlineData("member", "Northwind Redesign — Member, can create and edit Projects and Tasks")]
    [InlineData("viewer", "Northwind Redesign — Viewer, read only in this Space")]
    [Trait("Requirement", "UX-DR28")]
    [Trait("Requirement", "NFR-9")]
    public void The_accessible_name_states_the_Role_and_what_it_permits(string role, string expected)
    {
        var composed = RoleCopy.Compose(
            key => Copy.GetString(key, CultureInfo.InvariantCulture) ?? key,
            RoleCopy.AccessibleNameKey,
            "Northwind Redesign",
            role);

        Assert.Equal(expected, composed);
    }

    /// <summary>
    /// AC7 - the purge hook reads the destination's Space from its route, and only from
    /// <c>spaces/{id}</c>.
    /// </summary>
    [Fact]
    public void A_route_names_a_Space_only_when_it_is_a_Space_route()
    {
        Assert.Equal(Northwind, ClientRoutes.SpaceIn($"spaces/{Northwind:D}"));
        Assert.Equal(Northwind, ClientRoutes.SpaceIn($"spaces/{Northwind:D}?tab=1#x"));
        Assert.Null(ClientRoutes.SpaceIn(string.Empty));
        Assert.Null(ClientRoutes.SpaceIn("sign-in"));
        Assert.Null(ClientRoutes.SpaceIn("spaces/not-a-guid"));
        Assert.Null(ClientRoutes.SpaceIn($"projects/{Northwind:D}"));
    }

    /// <summary>
    /// AC7 - the client's composition root purges the departed Space from a location-CHANGING
    /// handler, which runs before the router renders the destination, and wires where a sign-out
    /// lands.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why a wiring gate as well as the browser cases.</b> In this client every Space-scoped
    /// component also resets itself when its parameters change, so a hook moved to
    /// <c>LocationChanged</c> - after the navigation - or removed altogether leaves nothing stale on
    /// screen that a browser can catch today. Epic 2's Board will not be so forgiving. So the hook
    /// itself is held: <c>Program</c> must call <c>RegisterLocationChangingHandler</c>, and
    /// <see cref="ActiveSpaceState.Navigating"/> must be called from <c>Program</c>'s code.
    /// Read from compiled IL, as <c>SessionSeamTests.The_client_wires_what_happens_when_a_session_ends</c>
    /// does, so a comment naming either method satisfies nothing.
    /// </para>
    /// <para>
    /// A hook moved to <c>LocationChanged</c> is caught here and by nothing in the browser suite.
    /// </para>
    /// </remarks>
    [Fact]
    [Trait("Requirement", "UX-DR30")]
    public void The_client_purges_the_departed_Space_before_the_router_renders()
    {
        var clientAssembly = ProductionAssemblies.All.Single(assembly => assembly.GetName().Name == "Yello.Client");

        using var module = ModuleDefinition.ReadModule(clientAssembly.Location);

        List<string> CallersOf(string declaringType, string method) =>
            [.. (from type in module.GetTypes()
                 from candidate in type.Methods
                 where candidate.HasBody
                 where candidate.Body.Instructions.Any(instruction =>
                     instruction.Operand is MethodReference called
                     && called.Name == method
                     && called.DeclaringType.FullName == declaringType)
                 select Outermost(type).FullName).Distinct(StringComparer.Ordinal)];

        Assert.Contains("Program", CallersOf("Microsoft.AspNetCore.Components.NavigationManager", "RegisterLocationChangingHandler"));
        Assert.Contains("Program", CallersOf(typeof(ActiveSpaceState).FullName!, nameof(ActiveSpaceState.Navigating)));
        Assert.Contains("Program", CallersOf(typeof(SessionState).FullName!, nameof(SessionState.AnnounceSignOutWith)));
    }

    /// <summary>
    /// Story 3.2 AC11 (UX-DR31, UX-DR38) - a sentence held in <see cref="SpaceNarration"/> is a pending announcement
    /// bound to the Space it was said in: it survives a navigation within that Space and is gone after a change of Space.
    /// </summary>
    /// <remarks>
    /// <b>Written by the story 3.2 ATDD run.</b> Wired here the way Task 2 recommends <c>Program</c> wires it
    /// (<c>RegisterPurge(narration.Forget)</c>). The ATDD scaffold found <c>Forget</c> by reflection so it compiled before
    /// the method existed; the dev phase retargeted it to a direct call on activation. The wiring itself is held by
    /// <c>SpaceSwitcherGateTests.The_composition_root_registers_the_pending_narration_with_the_Space_purge</c>.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "UX-DR38")]
    public void A_pending_narration_is_forgotten_on_a_Space_change_and_kept_within_one_Space()
    {
        using var state = InSpace(Northwind);

        var narration = new SpaceNarration();

        using var registration = state.RegisterPurge(narration.Forget);

        // Anchor: a navigation within one Space purges nothing, so the sentence is still there to take.
        narration.Say("You're now a Member. Space settings is no longer available.");
        state.Navigating(Northwind);

        Assert.Equal("You're now a Member. Space settings is no longer available.", narration.Take());

        narration.Say("You're now a Member. Space settings is no longer available.");
        state.Navigating(Harbour);

        Assert.Null(narration.Take());
    }

    private static TypeDefinition Outermost(TypeDefinition type) =>
        type.DeclaringType is null ? type : Outermost(type.DeclaringType);

    private static ActiveSpaceState InSpace(Guid spaceId)
    {
        var state = new ActiveSpaceState();

        state.Navigating(spaceId);
        state.Arrive(Answer(spaceId, "owner"));

        return state;
    }

    private static ActiveSpaceResponse Answer(Guid spaceId, string role) => new(spaceId, "A Space", role);

    private static Task RecordAsync(List<string> events, string what)
    {
        events.Add(what);

        return Task.CompletedTask;
    }
}
