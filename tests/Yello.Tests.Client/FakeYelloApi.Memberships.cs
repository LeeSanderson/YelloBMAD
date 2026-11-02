using System.Collections.Concurrent;
using System.Text.Json;
using Yello.Contracts;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// Story 4.4's routes on the fake (ATDD run, 2026-11-02): the Membership list, <c>GET /api/v1/spaces/{id}/memberships</c>
/// with an optional <c>after</c> cursor, and the Role change, <c>PATCH .../memberships/{membershipId}</c> with
/// <c>{ role }</c>. No case written before 4.4 sends either, so every earlier case is served the bytes it always was (both fell
/// to the unmatched <c>404</c> before).
/// </summary>
/// <remarks>
/// <para>
/// <b>The list as the Host answers it under Q8 (a) and Q9 (a).</b> <see cref="SpaceMemberships"/> for the Space, in join
/// order, <see cref="MembershipWire.PageSize"/> rows a page, as <see cref="MembershipPage"/>: the true <c>total</c>, the
/// page's rows (<see cref="ListedMembership"/>, <c>isYou</c> as seeded) and an opaque <c>nextAfter</c> until the last page. A
/// read can be held (<see cref="HoldMembershipListAsync"/>), to show what renders before the server answers.
/// </para>
/// <para>
/// <b>The Role change as the Host answers it under Lee's Q1 (a) and Q10 (a), Q11 (a).</b> The pipeline first: an unlisted
/// Space is the boundary <c>404</c>, a Role other than Owner or Admin the <c>403 insufficient-role</c> naming
/// <c>change-membership-role</c>. Then validation: <c>owner</c> or an unknown code is <c>400 role-not-assignable</c>. Then the
/// store: an id the Space does not list is the boundary <c>404</c>; the Owner's row is <c>409 owner-role-fixed</c> for the
/// Owner and <c>403</c> for an Admin; an Admin touching an Admin row or sending <c>admin</c> is the Qualified <c>403</c>;
/// the same Role is a <c>200</c> no-op; otherwise the row changes and the answer is the updated row. A key already applied to
/// the same path and body replays the <c>200</c>; on another body it is <c>422 idempotency-key-reused</c>. A held change
/// waits for its release (<see cref="HoldRoleChangeAsync"/>); <see cref="NextRoleChangeRefusal"/> refuses the next one.
/// </para>
/// <para>
/// <b>Contract constants, never literals</b> (4.1 review P13): the paths and the two new problem types are
/// <see cref="MembershipWire"/>'s ATDD stand-ins until Task 3 declares <c>MembershipRoutes</c> and the <c>ProblemTypes</c>
/// members; the dev phase swaps them here as everywhere.
/// </para>
/// </remarks>
internal sealed partial class FakeYelloApi
{
    private readonly ConcurrentDictionary<string, (string Path, string Body, string Answer)> _roleChangedByKey = new(StringComparer.Ordinal);
    private readonly Lock _membershipsLock = new();

    private DeleteHold? _roleChangeHold;
    private DeleteHold? _lastRoleChangeHold;
    private DeleteHold? _membershipListHold;
    private DeleteHold? _lastMembershipListHold;

    /// <summary>
    /// Each Space's Memberships as the list answers them, in join order. Story 4.4. Seed it before the page opens.
    /// </summary>
    public ConcurrentDictionary<Guid, List<ListedMembership>> SpaceMemberships { get; } = new();

    /// <summary>
    /// When set, the next Role change is refused with this instead of applied: a status, a problem type and the capability a
    /// <c>403</c> names (or <c>null</c>). Story 4.4.
    /// </summary>
    public (int Status, string ProblemType, string? Capability)? NextRoleChangeRefusal { get; set; }

    /// <summary>
    /// The list's path for one Space, as the contract names it. Story 4.4.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string MembershipsPathOf(Guid spaceId) => MembershipWire.MembershipsOf(spaceId);

    /// <summary>
    /// One Membership's path - the Role change's - as the contract names it. Story 4.4.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="membershipId">The Membership.</param>
    /// <returns>The path.</returns>
    public static string MembershipPathOf(Guid spaceId, Guid membershipId) => MembershipWire.MembershipOf(spaceId, membershipId);

    /// <summary>
    /// Every read of <paramref name="spaceId"/>'s Membership list the page sent, in order, each with its query. Story 4.4.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The requests.</returns>
    public IReadOnlyList<FakeRequest> MembershipListReads(Guid spaceId) =>
        [.. _requests.Where(request => request.Method == "GET" && request.Path == MembershipsPathOf(spaceId))];

    /// <summary>
    /// Every Role change in <paramref name="spaceId"/> the page sent, in order, each with its body and headers. Story 4.4.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The requests.</returns>
    public IReadOnlyList<FakeRequest> RoleChanges(Guid spaceId) =>
        [.. _requests.Where(request => request.Method == "PATCH" && TryMembership(request.Path, out var changedIn, out _) && changedIn == spaceId)];

    /// <summary>
    /// Holds the <b>next</b> Role change until it is released. Story 4.4.
    /// </summary>
    /// <returns>A task that completes when the held change has arrived and is waiting.</returns>
    public Task HoldRoleChangeAsync()
    {
        var hold = new DeleteHold();

        _roleChangeHold = hold;
        _lastRoleChangeHold = hold;

        return hold.Arrived.Task;
    }

    /// <summary>
    /// Lets the held Role change go and applies it. Story 4.4.
    /// </summary>
    public void ReleaseRoleChange() => _lastRoleChangeHold?.Released.TrySetResult(TaskEditOutcome.Admitted);

    /// <summary>
    /// Lets the held Role change go with a refusal or a server failure instead: nothing changes. Story 4.4.
    /// </summary>
    /// <param name="status">The status code.</param>
    /// <param name="problemType">The problem type.</param>
    /// <param name="capability">The capability a <c>403</c> names, or <c>null</c>.</param>
    public void ReleaseRoleChangeRefused(int status, string problemType, string? capability = null) =>
        _lastRoleChangeHold?.Released.TrySetResult(new TaskEditOutcome(status, problemType, capability, Abort: false));

    /// <summary>
    /// Lets the held Role change go with no answer - the connection reset. Nothing changes, so a resend under the same key is
    /// applied. Story 4.4.
    /// </summary>
    public void ReleaseRoleChangeUnanswered() =>
        _lastRoleChangeHold?.Released.TrySetResult(new TaskEditOutcome(0, string.Empty, Capability: null, Abort: true));

    /// <summary>
    /// Holds the <b>next</b> Membership list read until it is released. Story 4.4.
    /// </summary>
    /// <returns>A task that completes when the held read has arrived and is waiting.</returns>
    public Task HoldMembershipListAsync()
    {
        var hold = new DeleteHold();

        _membershipListHold = hold;
        _lastMembershipListHold = hold;

        return hold.Arrived.Task;
    }

    /// <summary>
    /// Lets the held list read go, answered as the list stands then. Story 4.4.
    /// </summary>
    public void ReleaseMembershipList() => _lastMembershipListHold?.Released.TrySetResult(TaskEditOutcome.Admitted);

    /// <summary>
    /// Story 4.4's answers, consulted before the main routes: <c>null</c> when the request is neither Membership route.
    /// </summary>
    private async Task<(int Status, string Body)?> MembershipAsync(FakeRequest request)
    {
        if (request.Method == "GET" && TryMemberships(request.Path, out var spaceId))
        {
            return await ListMembershipsAsync(spaceId, request.Query).ConfigureAwait(false);
        }

        return request.Method == "PATCH" && TryMembership(request.Path, out var changedIn, out var membershipId)
            ? await ChangeRoleAsync(changedIn, membershipId, request).ConfigureAwait(false)
            : null;
    }

    private async Task<(int Status, string Body)> ListMembershipsAsync(Guid spaceId, string query)
    {
        if (Interlocked.Exchange(ref _membershipListHold, null) is { } hold)
        {
            hold.Arrived.TrySetResult();
            _ = await hold.Released.Task.ConfigureAwait(false);
        }

        if (MembershipRefusal(spaceId) is { } refused)
        {
            return refused;
        }

        ListedMembership[] all = [];

        if (SpaceMemberships.TryGetValue(spaceId, out var listed))
        {
            lock (_membershipsLock)
            {
                all = [.. listed];
            }
        }

        var after = System.Web.HttpUtility.ParseQueryString(query).Get("after");
        var start = after is not null && int.TryParse(after.TrimStart('p'), System.Globalization.CultureInfo.InvariantCulture, out var index) ? index : 0;
        var page = all.Skip(start).Take(MembershipWire.PageSize).ToList();
        var next = start + page.Count < all.Length ? "p" + (start + page.Count).ToString(System.Globalization.CultureInfo.InvariantCulture) : null;

        return (200, JsonSerializer.Serialize(new MembershipPage(all.Length, page, next), JsonOptions));
    }

    private async Task<(int Status, string Body)> ChangeRoleAsync(Guid spaceId, Guid membershipId, FakeRequest request)
    {
        var key = request.Header(Idempotency.HeaderName);
        var body = request.Body ?? string.Empty;

        if (key is not null && _roleChangedByKey.TryGetValue(key, out var applied))
        {
            return string.Equals(applied.Path, request.Path, StringComparison.Ordinal) && string.Equals(applied.Body, body, StringComparison.Ordinal)
                ? (200, applied.Answer)
                : (422, Problem(ProblemTypes.IdempotencyKeyReused));
        }

        var outcome = await DeleteOutcomeAsync(() => Interlocked.Exchange(ref _roleChangeHold, null), () =>
        {
            var refusal = NextRoleChangeRefusal;

            NextRoleChangeRefusal = null;
            return refusal;
        }).ConfigureAwait(false);

        if (outcome.Abort)
        {
            return (AbortConnection, string.Empty);
        }

        if (outcome.Status is not (200 or 204))
        {
            return (outcome.Status, JsonSerializer.Serialize(new ProblemResponse(outcome.ProblemType, Errors: null, Capability: outcome.Capability), JsonOptions));
        }

        var answer = ApplyRoleChange(spaceId, membershipId, MemberOf(body, "role"));

        if (answer.Status == 200 && key is not null)
        {
            _roleChangedByKey[key] = (request.Path, body, answer.Body);
        }

        return answer;
    }

    private (int Status, string Body) ApplyRoleChange(Guid spaceId, Guid membershipId, string? role)
    {
        if (MembershipRefusal(spaceId) is { } refused)
        {
            return refused;
        }

        if (role is not ("admin" or "member" or "viewer"))
        {
            return (400, Problem(MembershipWire.RoleNotAssignableProblemType));
        }

        var viewer = Roles[spaceId];

        if (!SpaceMemberships.TryGetValue(spaceId, out var listed))
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        lock (_membershipsLock)
        {
            var at = listed.FindIndex(row => row.Id == membershipId);

            if (at < 0)
            {
                return (404, Problem(ProblemTypes.NotAvailable));
            }

            var target = listed[at];
            var insufficient = (403, JsonSerializer.Serialize(new ProblemResponse(ProblemTypes.InsufficientRole, Errors: null, Capability: MembershipWire.CapabilityCode), JsonOptions));

            if (target.Role == "owner")
            {
                return viewer == "owner" ? (409, Problem(MembershipWire.OwnerRoleFixedProblemType)) : insufficient;
            }

            if (viewer == "admin" && (target.Role == "admin" || role == "admin"))
            {
                return insufficient;
            }

            var changed = target with { Role = role };

            listed[at] = changed;

            return (200, JsonSerializer.Serialize(changed, JsonOptions));
        }
    }

    /// <summary>
    /// The pipeline's answer before either Membership route runs: the boundary <c>404</c> for a Space not listed, and the
    /// <c>403</c> naming <c>change-membership-role</c> for a Role other than Owner or Admin; <c>null</c> when the request
    /// passes.
    /// </summary>
    private (int Status, string Body)? MembershipRefusal(Guid spaceId)
    {
        if (Switcher.All(space => space.Id != spaceId) || !Roles.TryGetValue(spaceId, out var role))
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        return role is "owner" or "admin"
            ? null
            : (403, JsonSerializer.Serialize(new ProblemResponse(ProblemTypes.InsufficientRole, Errors: null, Capability: MembershipWire.CapabilityCode), JsonOptions));
    }

    /// <summary>
    /// Whether <paramref name="path"/> is <see cref="MembershipsPathOf"/> for some Space.
    /// </summary>
    private static bool TryMemberships(string path, out Guid spaceId)
    {
        const string leaf = "/memberships";

        spaceId = Guid.Empty;

        return path.EndsWith(leaf, StringComparison.Ordinal)
            && TryActiveSpace(path[..^leaf.Length], out spaceId)
            && path == MembershipsPathOf(spaceId);
    }

    /// <summary>
    /// Whether <paramref name="path"/> is <see cref="MembershipPathOf"/> for some Space and Membership.
    /// </summary>
    private static bool TryMembership(string path, out Guid spaceId, out Guid membershipId)
    {
        var slash = path.LastIndexOf('/');

        spaceId = Guid.Empty;
        membershipId = Guid.Empty;

        return slash > 0
            && Guid.TryParse(path[(slash + 1)..], out membershipId)
            && TryMemberships(path[..slash], out spaceId)
            && path == MembershipPathOf(spaceId, membershipId);
    }
}
