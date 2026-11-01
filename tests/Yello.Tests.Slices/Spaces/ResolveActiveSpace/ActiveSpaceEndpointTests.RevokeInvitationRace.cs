using System.Data.Common;
using System.Net;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Spaces;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 4.2's race: a revoke must never overwrite an acceptance that commits under it. The revoke is one guarded
/// statement, <c>UPDATE Invitation SET State = 'Revoked' WHERE Id = @id AND State = Pending AND ExpiresAt &gt; now</c>, so
/// under RCSI it waits on an uncommitted writer of the row and re-evaluates the guard against what that writer committed.
/// AC4, Q10 (a); the story's "Locking and the race"; <c>TESTING-CONVENTIONS.md</c> "When the race is about a read, the
/// test does the other write".
/// </summary>
/// <remarks>
/// <b>Written as a red-phase scaffold by the story 4.2 ATDD run (2026-11-01); activated by its dev phase.</b> The test plays 4.3's acceptance itself
/// (no product path exists yet): on a connection opened under the Space's key it begins a transaction and sets the row
/// <c>Accepted</c> without committing, fires the revoke, waits until the revoke is blocked on any <c>LCK_M_%</c>
/// (<see cref="TaskWire.WaitUntilBlockedOnALockAsync"/>), then commits. A guarded revoke then matches no row and answers
/// <c>409 invitation-not-pending</c>; an unguarded one (plant 2) or a load-check-save (plant 3, which read the committed
/// <c>Pending</c> before the holder committed) overwrites the acceptance. The update keeps <c>SpaceId</c>, so the
/// <c>BLOCK AFTER UPDATE</c> predicate admits it; the connection carries the Space's key, so the FILTER predicate shows the
/// row. No application lock and no <c>HOLDLOCK</c> (Q10 (a); 3.4).
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// AC4, Q10 (a), AD-26, AD-27 - a revoke racing an acceptance that commits under it waits on the row, re-evaluates its
    /// guard against the committed <c>Accepted</c>, and answers <c>409 invitation-not-pending</c>; the row stays
    /// <c>Accepted</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-12")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AD-27")]
    public async Task A_revoke_racing_an_acceptance_that_commits_under_it_answers_409_and_leaves_it_accepted()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var invitee = InvitationWire.FreshAddress("raced-revoke");
        var spaceId = Guid.NewGuid();
        WireResponse? raced = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedNamedSpaceAsync(database, spaceId, "Raced " + Suffix(), await AccountIdOfAsync(database, address), Role.Owner);

            await session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(session, spaceId, invitee, "member")).StatusCode);

            // Presence first: the owner's first request in the Space inserts its SpacePresence row - the issue above made it.
            Assert.Equal(HttpStatusCode.OK, (await session.GetAsync(SpaceRoutes.ActiveOf(spaceId))).StatusCode);

            var invitationId = await InvitationIdOfAsync(wire, spaceId, invitee);
            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await AcceptUncommittedAsync(holder, held, invitationId, cancellationToken);

            var racing = RevokeAsync(session, spaceId, invitationId);

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            raced = await racing;
        });

        Assert.True(raced?.StatusCode == HttpStatusCode.Conflict, $"{raced?.StatusCode} {raced?.Body}");
        Assert.Equal(ProblemTypes.InvitationNotPending, ProblemType(raced!));
        Assert.Equal("Accepted", Assert.Single(await wire.InvitationsInAsync(spaceId)).State);
    }

    /// <summary>
    /// What 4.3's acceptance will write, held uncommitted in <paramref name="held"/>: the row set <c>Accepted</c>.
    /// </summary>
    private static async Task AcceptUncommittedAsync(SqlConnection holder, DbTransaction held, Guid invitationId, CancellationToken cancellationToken)
    {
        await using DbCommand command = holder.CreateCommand();

        command.Transaction = held;
        command.CommandText = "UPDATE dbo.[Invitation] SET State = N'Accepted' WHERE Id = @id AND State = N'Pending'";
        command.Parameters.Add(new SqlParameter("@id", invitationId));

        Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
    }
}
