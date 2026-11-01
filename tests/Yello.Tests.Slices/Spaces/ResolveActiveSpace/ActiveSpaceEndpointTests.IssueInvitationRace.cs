using System.Data.Common;
using System.Net;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 4.1's lock race: under RCSI the reads that decide an issue (AC4's Membership, AC13's actionable pending
/// Invitation) are taken only after the Space-named application lock <c>Yello/Invitations/{spaceId:D}</c>, so a
/// concurrent issue of the same address cannot slip past them. AC13; the story's Locking section;
/// <c>TESTING-CONVENTIONS.md</c> "When the race is about a read, the test does the other write".
/// </summary>
/// <remarks>
/// <b>Written as a red-phase scaffold by the story 4.1 ATDD run (2026-11-01).</b> The test holds the lock itself on a
/// connection opened under the Space's key, fires the issue, waits until it is blocked on any <c>LCK_M_%</c>
/// (<see cref="TaskWire.WaitUntilBlockedOnALockAsync"/>), then - in the held transaction - inserts what a competing issue
/// would have (a pending Invitation for the same address) and commits. A request that read before the lock sees no
/// pending Invitation and inserts a second row; one that read after it answers <c>409 invitation-pending</c>. The inserted
/// row satisfies the schema Task 4 declares: the Space exists (<c>FK_Invitation_Space_SpaceId</c>), the issuer is a
/// registered Account (the <c>IssuedByAccountId</c> foreign key), a random 32-byte <c>TokenHash</c> (its unique index),
/// <c>Id</c> supplied, and the connection carries the Space's key (<c>BLOCK AFTER INSERT</c>). The plant is "skip the
/// lock": two rows. No <c>HOLDLOCK</c> anywhere (3.4).
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// AC13, AD-27 - an issue racing a competing issue of the same address reads the pending Invitations only under the
    /// Space's application lock: it waits on the held lock, sees the committed competitor, and answers
    /// <c>409 invitation-pending</c>; exactly one row exists.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC13 - ApplicationLocks.InvitationsOf and the store's lock-then-read order arrive with story 4.1 Task 4")]
    [Trait("Requirement", "FR-10")]
    [Trait("Requirement", "AD-27")]
    public async Task An_issue_racing_a_pending_Invitation_for_the_same_address_reads_it_only_under_the_Space_lock()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var invitee = InvitationScaffold.FreshAddress("raced");
        var spaceId = Guid.NewGuid();
        WireResponse? raced = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            var ownerId = await AccountIdOfAsync(database, address);
            await SeedNamedSpaceAsync(database, spaceId, "Raced " + Suffix(), ownerId, Role.Owner);

            await session.FetchAntiforgeryTokenAsync();

            // Presence first: the owner's first request in the Space inserts its SpacePresence row, which must not be what
            // the race waits on.
            Assert.Equal(HttpStatusCode.OK, (await session.GetAsync(Yello.Contracts.Spaces.SpaceRoutes.ActiveOf(spaceId))).StatusCode);

            var cancellationToken = TestContext.Current.CancellationToken;

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await TakeApplicationLockAsync(holder, held, InvitationsLockOf(spaceId), cancellationToken);

            var racing = IssueAsync(session, spaceId, invitee, "member");

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);

            await InsertPendingInvitationAsync(holder, held, spaceId, ownerId, invitee, cancellationToken);
            await held.CommitAsync(cancellationToken);

            raced = await racing;
        });

        Assert.True(raced?.StatusCode == HttpStatusCode.Conflict, $"{raced?.StatusCode} {raced?.Body}");
        Assert.Equal(InvitationScaffold.InvitationPending, ProblemType(raced!));
        Assert.Single(await wire.InvitationsInAsync(spaceId));
    }

    /// <summary>
    /// The product's application lock for a Space's Invitations, as story 4.1 Task 4 names it.
    /// </summary>
    private static string InvitationsLockOf(Guid spaceId) => $"Yello/Invitations/{spaceId:D}";

    /// <summary>
    /// A pending Invitation for <paramref name="emailAddress"/>, written in the held transaction - what a competing issue
    /// would insert. <c>ExpiresAt</c> is left to the column default (Q10 (a)), so the row is actionable.
    /// </summary>
    private static async Task InsertPendingInvitationAsync(
        SqlConnection holder,
        DbTransaction held,
        Guid spaceId,
        Guid issuerId,
        string emailAddress,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = holder.CreateCommand();

        command.Transaction = held;
        command.CommandText = """
            INSERT dbo.[Invitation] (Id, SpaceId, EmailAddress, NormalizedEmailAddress, Role, State, TokenHash, IssuedByAccountId, IssuerName, CreatedAt)
            VALUES (@id, @space, @address, @normalized, N'Member', N'Pending', @hash, @issuer, N'Competitor', SYSDATETIMEOFFSET())
            """;
        command.Parameters.Add(new SqlParameter("@id", Guid.NewGuid()));
        command.Parameters.Add(new SqlParameter("@space", spaceId));
        command.Parameters.Add(new SqlParameter("@address", emailAddress));
        command.Parameters.Add(new SqlParameter("@normalized", EmailAddressNormalisation.Normalise(emailAddress)));
        command.Parameters.Add(new SqlParameter("@hash", System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));
        command.Parameters.Add(new SqlParameter("@issuer", issuerId));

        Assert.Equal(1, await command.ExecuteNonQueryAsync(cancellationToken));
    }
}
