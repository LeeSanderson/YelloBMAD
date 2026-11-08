using Xunit;
using Yello.Client.Spaces;
using Yello.Contracts.Spaces;

namespace Yello.Tests.Architecture;

/// <summary>
/// The client's one Role entry point, <see cref="ActiveSpaceState.ReconcileAsync(Guid, string, CancellationToken)"/>,
/// asserted for what it does: a drop is raised and narrated before the lower Role is applied, a rise is applied at once,
/// and a Role for any Space but the active one - or after the request was cancelled - is dropped unseen.
/// </summary>
/// <remarks>
/// In this suite because it is the one that references <c>Yello.Client</c>, as <c>ActiveSpaceStateTests</c> is. The
/// browser cases in <c>RoleDropTests</c> prove the same order on the page; these prove the filter, which a browser case
/// cannot reach once a switch has cancelled the late request.
/// </remarks>
[Trait("Suite", "Architecture")]
[Trait("Priority", "P0")]
[Trait("Requirement", "UX-DR29")]
[Trait("Requirement", "UX-DR38")]
public sealed class RoleReconcileTests
{
    private static readonly Guid Northwind = Guid.NewGuid();
    private static readonly Guid Harbour = Guid.NewGuid();

    /// <summary>
    /// A drop raises <see cref="ActiveSpaceState.RoleDropping"/>, then narrates, and only then applies the lower Role: the
    /// narration sees the old Role still held, and <see cref="ActiveSpaceState.Changed"/> follows it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_drop_is_raised_and_narrated_before_the_lower_Role_is_applied()
    {
        using var state = InSpace(Northwind, "member");
        var events = new List<string>();

        state.RoleDropping += (_, drop) => events.Add($"dropping {drop.From}->{drop.To}");
        state.Changed += (_, _) => events.Add("changed " + state.Current?.Role);

        using var narrator = state.RegisterNarrator(drop =>
        {
            events.Add("narrated while " + state.Current?.Role);

            return Task.CompletedTask;
        });

        await state.ReconcileAsync(Northwind, "viewer", CancellationToken.None);

        Assert.Equal(["dropping member->viewer", "narrated while member", "changed viewer"], events);
    }

    /// <summary>
    /// A rise is applied at once and announced through <see cref="ActiveSpaceState.RoleRose"/>; nothing is narrated.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_rise_is_applied_at_once_and_never_narrated()
    {
        using var state = InSpace(Northwind, "viewer");
        var narrated = false;
        var rose = false;

        state.RoleRose += (_, _) => rose = true;

        using var narrator = state.RegisterNarrator(_ =>
        {
            narrated = true;

            return Task.CompletedTask;
        });

        await state.ReconcileAsync(Northwind, "member", CancellationToken.None);

        Assert.Equal("member", state.Current?.Role);
        Assert.True(rose);
        Assert.False(narrated);
    }

    /// <summary>
    /// A Role carried by an answer about another Space, an unknown Role code, the same Role, or a cancelled request is
    /// dropped: nothing is raised or narrated and the Role held is unchanged. Plant: remove the Space check - the first
    /// call narrates in the wrong Space.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_Role_for_any_Space_but_the_active_one_is_dropped_unseen()
    {
        using var state = InSpace(Harbour, "member");
        using var cancelled = new CancellationTokenSource();
        var raised = new List<string>();

        await cancelled.CancelAsync();

        state.RoleDropping += (_, drop) => raised.Add("dropping " + drop.To);
        state.RoleRose += (_, _) => raised.Add("rose");

        using var narrator = state.RegisterNarrator(drop =>
        {
            raised.Add("narrated " + drop.To);

            return Task.CompletedTask;
        });

        await state.ReconcileAsync(Northwind, "viewer", CancellationToken.None);
        await state.ReconcileAsync(Harbour, "superuser", CancellationToken.None);
        await state.ReconcileAsync(Harbour, "member", CancellationToken.None);
        await state.ReconcileAsync(Harbour, "viewer", cancelled.Token);

        Assert.Empty(raised);
        Assert.Equal("member", state.Current?.Role);
    }

    /// <summary>
    /// A switch made while a drop's narration renders leaves the departed Space's lower Role unapplied: the Space now
    /// active keeps its own Role.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_switch_during_the_narration_leaves_the_lower_Role_unapplied()
    {
        using var state = InSpace(Northwind, "member");
        var rendered = new TaskCompletionSource();

        using var narrator = state.RegisterNarrator(_ => rendered.Task);

        var reconciling = state.ReconcileAsync(Northwind, "viewer", CancellationToken.None);

        state.Navigating(Harbour);
        state.Arrive(new ActiveSpaceResponse(Harbour, "Harbour", "member"));
        rendered.SetResult();

        await reconciling;

        Assert.Equal(Harbour, state.Current?.Id);
        Assert.Equal("member", state.Current?.Role);
    }

    /// <summary>
    /// A rename's answer keeps the Role held and hands its own Role to the entry point, so a drop it carries is narrated
    /// like any other rather than applied in place.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_renames_Role_goes_through_the_entry_point()
    {
        using var state = InSpace(Northwind, "admin");
        var narrated = new List<string>();

        using var narrator = state.RegisterNarrator(drop =>
        {
            narrated.Add(drop.To);

            return Task.CompletedTask;
        });

        Assert.True(state.Renamed(new ActiveSpaceResponse(Northwind, "Renamed", "member")));
        await state.ReconcileAsync(Northwind, "member", CancellationToken.None);

        Assert.Equal(["member"], narrated);
        Assert.Equal("Renamed", state.Current?.Name);
        Assert.Equal("member", state.Current?.Role);
    }

    /// <summary>
    /// A rise that lands while a drop is narrating is judged once the drop is applied, so the tab ends on the risen Role
    /// rather than on the older drop applied over it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_rise_during_a_drops_narration_is_applied_after_it_and_kept()
    {
        using var state = InSpace(Northwind, "admin");
        var rendered = new TaskCompletionSource();

        using var narrator = state.RegisterNarrator(_ => rendered.Task);

        var dropping = state.ReconcileAsync(Northwind, "viewer", CancellationToken.None);
        var rising = state.ReconcileAsync(Northwind, "owner", CancellationToken.None);

        Assert.Equal("admin", state.Current?.Role);

        rendered.SetResult();
        await Task.WhenAll(dropping, rising);

        Assert.Equal("owner", state.Current?.Role);
    }

    /// <summary>
    /// A second, different drop waits for the first one's narration and Role: each is narrated in turn, from the Role the
    /// one before it left, and neither Role is applied before its own narration has rendered.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_second_drop_waits_for_the_first_ones_narration()
    {
        using var state = InSpace(Northwind, "admin");
        var first = new TaskCompletionSource();
        var events = new List<string>();

        using var narrator = state.RegisterNarrator(drop =>
        {
            events.Add($"narrating {drop.From}->{drop.To} while {state.Current?.Role}");

            return events.Count == 1 ? first.Task : Task.CompletedTask;
        });

        var toMember = state.ReconcileAsync(Northwind, "member", CancellationToken.None);
        var toViewer = state.ReconcileAsync(Northwind, "viewer", CancellationToken.None);

        Assert.Equal(["narrating admin->member while admin"], events);

        first.SetResult();
        await Task.WhenAll(toMember, toViewer);

        Assert.Equal(["narrating admin->member while admin", "narrating member->viewer while member"], events);
        Assert.Equal("viewer", state.Current?.Role);
    }

    private static ActiveSpaceState InSpace(Guid spaceId, string role)
    {
        var state = new ActiveSpaceState();

        state.Navigating(spaceId);
        state.Arrive(new ActiveSpaceResponse(spaceId, "A Space", role));

        return state;
    }
}
