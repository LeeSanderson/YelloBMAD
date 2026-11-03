using Xunit;
using Yello.Application.Memberships;
using Yello.Application.Memberships.ChangeMembershipRole;
using Yello.Application.Memberships.ListMemberships;
using Yello.Domain.Memberships;

namespace Yello.Tests.Slices.Memberships;

/// <summary>
/// Story 4.4's slices without a database: the list pages at its named size and issues a cursor only when a row follows
/// (AC1, Q8 (a)); the Role change hands the store the acting authority's Role sets and never a Role (AC4; Lee's Q1 (a));
/// the authority fails closed when the pipeline set none; the target rule judges a change the statement did not write
/// (AC4, AC5; Q10 (a), Q11 (a)); the cursor reads only what the route issued; and a request may name every Role but Owner.
/// </summary>
/// <remarks>
/// The wire cases in <c>ActiveSpaceEndpointTests.ListMemberships.cs</c>, <c>.ChangeMembershipRole.cs</c> and
/// <c>.ChangeMembershipRoleRace.cs</c> hold the store, the lock, the guarded statement and the pipeline's recorded
/// <c>403</c>.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "FR-13")]
public sealed class MembershipHandlerTests
{
    /// <summary>
    /// AC1, Q8 (a), NFR-8 - the list asks the store for one row more than a page, serves the page, and issues the cursor of
    /// the last served row only when a row follows; a short page has no cursor.
    /// </summary>
    /// <param name="stored">How many rows the store holds.</param>
    /// <param name="served">How many the page serves.</param>
    /// <param name="cursor">Whether a cursor is issued.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(10, 10, false)]
    [InlineData(50, 50, false)]
    [InlineData(51, 50, true)]
    [Trait("Requirement", "NFR-8")]
    public async Task The_list_serves_a_page_and_a_cursor_only_when_a_row_follows(int stored, int served, bool cursor)
    {
        var rows = Enumerable.Range(0, stored).Select(index => Row(index)).ToList();
        var store = new RecordingStore(rows);

        var (page, total, nextAfter) = await new ListMembershipsHandler(store).HandleAsync(Guid.NewGuid(), after: null, TestContext.Current.CancellationToken);

        Assert.Equal(ListMembershipsHandler.PageSize + 1, store.Taken);
        Assert.Equal(served, page.Count);
        Assert.Equal(stored, total);
        Assert.Equal(cursor, nextAfter is not null);

        if (nextAfter is not null)
        {
            Assert.True(MembershipCursorText.TryRead(nextAfter, out var read));
            Assert.Equal(new MembershipCursor(page[^1].CreatedAt, page[^1].Id), read);
        }
    }

    /// <summary>
    /// AC4, Lee's Q1 (a), AR-3 - the change hands the store exactly the Role sets the pipeline set, the actor and the target;
    /// the slice has no Role to read.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-3")]
    public async Task The_change_hands_the_store_the_acting_authoritys_sets()
    {
        var store = new RecordingStore([]);
        var authority = new ActingAuthority();
        var sets = RoleCapabilityMatrix.RoleChangeAuthorityOf(Role.Admin);
        var (actor, target) = (Guid.NewGuid(), Guid.NewGuid());

        authority.Set(sets);

        _ = await new ChangeMembershipRoleHandler(store, authority).HandleAsync(actor, target, Role.Viewer, TestContext.Current.CancellationToken);

        Assert.Equal((actor, target, Role.Viewer, sets), Assert.Single(store.Changes));
    }

    /// <summary>
    /// AC4 - an acting authority the pipeline never set fails closed: reading it throws, so no change runs on a default.
    /// </summary>
    [Fact]
    public void An_unset_acting_authority_fails_closed() =>
        Assert.Throws<InvalidOperationException>(() => new ActingAuthority().RoleChanges);

    /// <summary>
    /// AC4, AC5, Q10 (a), Q11 (a) - why a change the guarded statement did not write was not written, decided from the
    /// target's Role as it stands and whether the actor still holds its authority.
    /// </summary>
    /// <param name="actor">The acting Role the authority was computed from.</param>
    /// <param name="current">The target's Role now.</param>
    /// <param name="proposed">The Role asked for.</param>
    /// <param name="stillHolds">Whether the actor still holds its authority.</param>
    /// <param name="expected">The refusal.</param>
    [Theory]
    [InlineData(Role.Owner, Role.Owner, Role.Admin, true, RoleChangeRefusal.OwnerRoleFixed)]
    [InlineData(Role.Admin, Role.Owner, Role.Viewer, true, RoleChangeRefusal.BeyondAuthority)]
    [InlineData(Role.Admin, Role.Member, Role.Admin, true, RoleChangeRefusal.BeyondAuthority)]
    [InlineData(Role.Admin, Role.Admin, Role.Member, true, RoleChangeRefusal.BeyondAuthority)]
    [InlineData(Role.Admin, Role.Admin, Role.Admin, true, RoleChangeRefusal.BeyondAuthority)]
    [InlineData(Role.Admin, Role.Member, Role.Viewer, false, RoleChangeRefusal.BeyondAuthority)]
    [InlineData(Role.Owner, Role.Member, Role.Member, true, RoleChangeRefusal.Unchanged)]
    [InlineData(Role.Admin, Role.Viewer, Role.Viewer, true, RoleChangeRefusal.Unchanged)]
    [InlineData(Role.Owner, Role.Member, Role.Admin, false, RoleChangeRefusal.BeyondAuthority)]
    [Trait("Requirement", "AR-12")]
    [Trait("Assumption", "PRD-12-4")]
    public void The_target_rule_judges_a_change_that_was_not_written(Role actor, Role current, Role proposed, bool stillHolds, RoleChangeRefusal expected) =>
        Assert.Equal(expected, RoleCapabilityMatrix.RoleChangeAuthorityOf(actor).Judge(current, proposed, stillHolds));

    /// <summary>
    /// AC4, Lee's Q1 (a) - the Roles an actor must still hold: the Owner's authority is held by the Owner alone, the Admin's
    /// by an Owner or an Admin, and a lesser Role holds nothing.
    /// </summary>
    [Fact]
    public void Each_authority_names_the_Roles_that_still_hold_it()
    {
        Assert.Equal([Role.Owner], RoleCapabilityMatrix.RoleChangeAuthorityOf(Role.Owner).HeldBy.Order());
        Assert.Equal([Role.Owner, Role.Admin], RoleCapabilityMatrix.RoleChangeAuthorityOf(Role.Admin).HeldBy.Order());
        Assert.Empty(RoleCapabilityMatrix.RoleChangeAuthorityOf(Role.Member).HeldBy);
        Assert.Empty(RoleCapabilityMatrix.RoleChangeAuthorityOf(Role.Viewer).HeldBy);
    }

    /// <summary>
    /// AC1, Q8 (a) - the cursor round-trips the instant and the id exactly, and reads nothing the route did not issue.
    /// </summary>
    [Fact]
    [Trait("Requirement", "NFR-8")]
    public void The_cursor_round_trips_and_refuses_what_the_route_did_not_issue()
    {
        var issued = new MembershipCursor(new DateTimeOffset(2026, 11, 2, 9, 30, 1, TimeSpan.Zero).AddTicks(1_234_567), Guid.NewGuid());

        Assert.True(MembershipCursorText.TryRead(MembershipCursorText.Write(issued), out var read));
        Assert.Equal(issued, read);

        foreach (var sent in new[] { string.Empty, "not base64!", "bTE", new string('A', 300) })
        {
            Assert.False(MembershipCursorText.TryRead(sent, out _), $"Read a cursor the route did not issue: '{sent}'.");
        }
    }

    /// <summary>
    /// AC4, Q10 (a), FR-42 - a request may name Admin, Member or Viewer by its stable code, and never Owner, in any case.
    /// </summary>
    /// <param name="code">The code sent.</param>
    /// <param name="reads">Whether it reads.</param>
    [Theory]
    [InlineData("admin", true)]
    [InlineData("member", true)]
    [InlineData("viewer", true)]
    [InlineData("owner", false)]
    [InlineData("OWNER", false)]
    [InlineData("Admin", false)]
    [InlineData("superuser", false)]
    [InlineData(null, false)]
    [Trait("Requirement", "FR-42")]
    public void Every_Role_but_Owner_is_assignable(string? code, bool reads) =>
        Assert.Equal(reads, AssignableRoles.TryParse(code, out _));

    private static ListedMembership Row(int index) =>
        new(Guid.NewGuid(), $"Member {index}", $"member-{index}@example.test", "member", IsYou: false, DateTimeOffset.UnixEpoch.AddMinutes(index));

    private sealed class RecordingStore(List<ListedMembership> rows) : IMembershipStore
    {
        public int Taken { get; private set; }

        public List<(Guid Actor, Guid Target, Role Proposed, RoleChangeAuthority Authority)> Changes { get; } = [];

        public Task<(IReadOnlyList<ListedMembership> Rows, int Total)> ListAsync(Guid callerAccountId, MembershipCursor? after, int take, CancellationToken cancellationToken)
        {
            Taken = take;

            return Task.FromResult<(IReadOnlyList<ListedMembership>, int)>(([.. rows.Take(take)], rows.Count));
        }

        public Task<RoleChangeResult> ChangeRoleAsync(Guid actorAccountId, Guid membershipId, Role proposed, RoleChangeAuthority authority, CancellationToken cancellationToken)
        {
            Changes.Add((actorAccountId, membershipId, proposed, authority));

            return Task.FromResult(new RoleChangeResult(RoleChangeOutcome.NotFound, null));
        }
    }
}
