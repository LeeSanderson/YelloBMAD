using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Xunit;
using Yello.Contracts.Spaces;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 4.1's outbox and email over the wire: one email enqueued in the same transaction as the Invitation and sent
/// in-process after the commit; the recovery sweep at process start and on inbound authenticated traffic, never on a
/// timer; and the email's content - the Space, the Role, the issuer, an opaque link, and nothing else. AC6, AC7; FR-39,
/// AR-33/AD-10, I-14, AD-28, NFR-6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 4.1 ATDD run (2026-11-01).</b> The Host's sender is the file drop,
/// pointed at the case's own <see cref="MailDrop"/> folder (Q13 (a), assumed); the outbox rows are read and, for the
/// crash cases, written directly (<see cref="WireDatabase.OutboxRowsAsync"/>, <see cref="WireDatabase.AddOutboxRowAsync(string, string, string)"/>:
/// <c>OutboxMessage</c> carries no policy, no Space column and no foreign key, per Lee's Q6, so a row needs nothing but
/// its own columns).
/// </para>
/// <para>
/// <b>The forced rollback</b> is an <c>AFTER INSERT</c> trigger on <c>IdempotencyRecord</c> that throws: the pipeline
/// records the key after the endpoint has inserted the Invitation and enqueued the email, inside the same unit of work,
/// so the trigger fails the request AFTER the enqueue. The trigger fires only for the case's own key, and is dropped in a
/// <c>finally</c>.
/// </para>
/// <para>
/// <b>The piggyback case requires the throttle not to be consumed by the start-up kick</b> (or a Host started a moment
/// ago would not sweep on its first authenticated request for a minute). Recorded in the ATDD checklist for the dev
/// phase.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// AC6, AR-33, AD-10 - one issue enqueues exactly one email, the dispatcher sends it after the commit (one drop, to
    /// the invited address, from the configured sender), and the row is deleted once sent (Q7 (a)).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-39")]
    [Trait("Requirement", "AR-33")]
    [Trait("Requirement", "AD-10")]
    public async Task One_issue_sends_exactly_one_email_and_the_outbox_row_is_deleted_once_sent()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var invitee = InvitationWire.FreshAddress("sent");
        var spaceId = Guid.NewGuid();
        IReadOnlyList<DroppedEmail> emails = [];
        var outboxEmptied = false;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedNamedSpaceAsync(database, spaceId, "Sending " + Suffix(), await AccountIdOfAsync(database, address), Role.Owner);

            await session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(session, spaceId, invitee, "member")).StatusCode);

            _ = await mail.WaitForEmailToAsync(invitee);
            outboxEmptied = await OutboxEventuallyHoldsNoRowForAsync(wire, invitee);
            emails = await mail.ReadAllAsync();
        });

        var email = Assert.Single(emails, email => string.Equals(email.To, invitee, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(invitee, email.To);
        Assert.Equal(MailDrop.Sender, email.From);
        Assert.True(outboxEmptied, "The outbox row for a sent email was not deleted.");
    }

    /// <summary>
    /// AC6, AD-10 - the enqueue is in the issue's own transaction: a failure after the enqueue (the idempotency record's
    /// insert, forced) leaves no Invitation, no outbox row and no email. Anchor: once the plant is gone, an issue to
    /// another address in the same Space is sent, so "no email" is not a dispatcher that never ran.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-33")]
    [Trait("Requirement", "AD-10")]
    public async Task A_failure_after_the_enqueue_rolls_back_the_Invitation_and_the_outbox_row_and_sends_nothing()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var (rolledBack, anchor) = (InvitationWire.FreshAddress("rolled-back"), InvitationWire.FreshAddress("anchor"));
        var key = Guid.NewGuid().ToString("D");
        var spaceId = Guid.NewGuid();
        WireResponse? failed = null;
        IReadOnlyList<DroppedEmail> emails = [];
        var invitationsAfterFailure = -1;
        var outboxAfterFailure = -1;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedNamedSpaceAsync(database, spaceId, "Rollback " + Suffix(), await AccountIdOfAsync(database, address), Role.Owner);

            await session.FetchAntiforgeryTokenAsync();

            var trigger = await PlantFailingIdempotencyTriggerAsync(database, key);

            try
            {
                failed = await IssueAsync(session, spaceId, rolledBack, "member", key);
            }
            finally
            {
                await DropTriggerAsync(database, trigger);
            }

            invitationsAfterFailure = (await wire.InvitationsInAsync(spaceId)).Count;
            outboxAfterFailure = (await wire.OutboxRowsAsync()).Count(row => string.Equals(row.Recipient, rolledBack, StringComparison.OrdinalIgnoreCase));

            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(session, spaceId, anchor, "member")).StatusCode);

            _ = await mail.WaitForEmailToAsync(anchor);
            emails = await mail.ReadAllAsync();
        });

        Assert.True(failed?.StatusCode >= HttpStatusCode.InternalServerError, $"The forced failure answered {failed?.StatusCode} {failed?.Body}");
        Assert.Equal(0, invitationsAfterFailure);
        Assert.Equal(0, outboxAfterFailure);
        Assert.DoesNotContain(emails, email => string.Equals(email.To, rolledBack, StringComparison.OrdinalIgnoreCase));
        Assert.Single(emails, email => string.Equals(email.To, anchor, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// AC6, AD-10, A-9 - a row a crash left unsent (seeded before the Host starts) is sent by the process-start sweep of
    /// a fresh Host, with no request at all, and deleted. The email is the row's rendered content, unchanged: the
    /// dispatcher reads no Space data.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-33")]
    [Trait("Requirement", "AD-10")]
    public async Task The_process_start_sweep_sends_a_row_a_crash_left_unsent()
    {
        _ = Available();
        var wire = Wire();
        var recipient = InvitationWire.FreshAddress("crashed");
        var (subject, body) = ("Left behind " + Suffix(), "Rendered before the crash " + Suffix());
        DroppedEmail? email = null;
        var outboxEmptied = false;

        await wire.AddOutboxRowAsync(recipient, subject, body);

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async __ =>
        {
            email = await mail.WaitForEmailToAsync(recipient);
            outboxEmptied = await OutboxEventuallyHoldsNoRowForAsync(wire, recipient);
        });

        Assert.Equal((subject, body), (email!.Subject, email.PlainText));
        Assert.True(outboxEmptied, "The swept row was not deleted after sending.");
    }

    /// <summary>
    /// AC6, AD-10, A-9 - a row left unsent after the Host started is sent on the next inbound AUTHENTICATED request (the
    /// piggyback trigger), not by any timer: registration and sign-in (anonymous) come first, then the row is seeded,
    /// then one authenticated read carries the sweep.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-33")]
    [Trait("Requirement", "AD-10")]
    public async Task The_piggyback_sweep_sends_a_row_on_the_next_authenticated_request()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var recipient = InvitationWire.FreshAddress("piggyback");
        var (subject, body) = ("Waiting " + Suffix(), "Unsent until traffic " + Suffix());
        DroppedEmail? email = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            var spaceId = Guid.NewGuid();
            await SeedNamedSpaceAsync(database, spaceId, "Traffic " + Suffix(), await AccountIdOfAsync(database, address), Role.Owner);

            await wire.AddOutboxRowAsync(recipient, subject, body);

            Assert.Equal(HttpStatusCode.OK, (await session.GetAsync(SpaceRoutes.ActiveOf(spaceId))).StatusCode);

            email = await mail.WaitForEmailToAsync(recipient);
        });

        Assert.Equal((subject, body), (email!.Subject, email.PlainText));
    }

    /// <summary>
    /// AC6 - a claim is guarded by the database clock: a row claimed more than five minutes ago is claimed again and
    /// sent; one claimed a minute ago is left alone (its claimant is presumed alive). Both seeded before the Host starts,
    /// so one process-start sweep decides both; the sent row is the anchor for the unsent one.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-33")]
    [Trait("Requirement", "AD-10")]
    public async Task A_stale_claim_is_claimed_again_and_a_fresh_claim_is_left_alone()
    {
        _ = Available();
        var wire = Wire();
        var (stale, fresh) = (InvitationWire.FreshAddress("stale"), InvitationWire.FreshAddress("fresh"));
        IReadOnlyList<DroppedEmail> emails = [];
        IReadOnlyList<OutboxRow> left = [];

        await wire.AddOutboxRowAsync(stale, "Stale " + Suffix(), "Claimed long ago", claimedMinutesAgo: 6);
        var freshId = await wire.AddOutboxRowAsync(fresh, "Fresh " + Suffix(), "Claimed just now", claimedMinutesAgo: 1);

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async __ =>
        {
            _ = await mail.WaitForEmailToAsync(stale);
            _ = await OutboxEventuallyHoldsNoRowForAsync(wire, stale);

            emails = await mail.ReadAllAsync();
            left = await wire.OutboxRowsAsync();
        });

        Assert.DoesNotContain(emails, email => email.To == fresh);

        var unsent = Assert.Single(left, row => row.Id == freshId);

        Assert.Equal(1, unsent.Attempts);
    }

    /// <summary>
    /// AC7, FR-39, I-14, UJ-2, UX-DR34 - the delivered email names the Space, the Role (its display name) and the issuer's
    /// display name, and discloses nothing else: none of the Space's Project, Task or Label names, none of its other
    /// members' names or addresses, not the issuer's other Space or that Space's content, and not the issuer's own address.
    /// The issuer owns two populated Spaces. The plant (the template given a Project name, or the issuer's address) is
    /// AC7's anchor and is recorded in the Debug Log.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-39")]
    [Trait("Requirement", "NFR-1")]
    public async Task The_email_names_the_Space_the_Role_and_the_issuer_and_discloses_nothing_else()
    {
        var database = Available();
        var invitee = InvitationWire.FreshAddress("i14");
        var (spaceName, otherSpaceName, otherProject, taskTitle) =
            ("Shared " + Suffix(), "Private " + Suffix(), "Hidden project " + Suffix(), "Hidden task " + Suffix());
        DroppedEmail? email = null;
        var withheld = new List<string>();
        var issuerName = string.Empty;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var populated = await PopulateSpaceAsync(client, database, spaceName);
            var owner = populated.Owner with { SpaceId = populated.SpaceId };

            var ownerIdentity = await IdentityOfAsync(database, populated.Owner.AccountId);
            var memberIdentity = await IdentityOfAsync(database, populated.Member.AccountId);
            var adminIdentity = await IdentityOfAsync(database, populated.Admin.AccountId);

            var firstProject = (await new WireDatabase(fixture.ConnectionString!).ProjectsInAsync(populated.SpaceId))[0].Id;
            _ = Tasks.TaskWire.IdOf(await Tasks.TaskWire.CreateTaskAsync(owner, populated.SpaceId, firstProject, taskTitle));

            var otherSpace = CreatedSpace(await CreateSpaceAsync(populated.Owner.Session, otherSpaceName)).Id;
            _ = await Tasks.TaskWire.CreateProjectAsync(populated.Owner with { SpaceId = otherSpace }, otherProject, otherSpace);

            issuerName = ownerIdentity.DisplayName;
            withheld.AddRange(populated.Names);
            withheld.AddRange([taskTitle, otherSpaceName, otherProject, ownerIdentity.EmailAddress]);
            withheld.AddRange([memberIdentity.DisplayName, memberIdentity.EmailAddress, adminIdentity.DisplayName, adminIdentity.EmailAddress]);

            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(populated.Owner.Session, populated.SpaceId, invitee, "member")).StatusCode);

            email = await mail.WaitForEmailToAsync(invitee);
        });

        var everything = email!.Everything;

        Assert.Contains(spaceName, everything, StringComparison.Ordinal);
        Assert.Contains("Member", email.PlainText, StringComparison.Ordinal);
        Assert.Contains(issuerName, everything, StringComparison.Ordinal);

        Assert.True(withheld.Count >= 9, "The withheld names were not all collected, so the absences below would be vacuous.");

        foreach (var name in withheld)
        {
            Assert.DoesNotContain(name, everything, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// AC7, AD-28, UX-DR18, Task 3 (C8) - the link is <c>{ClientOrigin}/invitations/{token}</c> and carries nothing but
    /// the opaque token: no Space id or name. The token is 256 random bits behind a prefix that is not the API Token's,
    /// and the Invitation stores only its SHA-256. The email states the expiry as the stored <c>ExpiresAt</c>'s UTC date.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-39")]
    [Trait("Requirement", "NFR-6")]
    [Trait("Assumption", "PRD-12-9")]
    public async Task The_link_carries_only_an_opaque_token_whose_SHA_256_is_the_stored_hash()
    {
        var database = Available();
        var wire = Wire();
        var address = RandomAddress();
        var invitee = InvitationWire.FreshAddress("token");
        var spaceId = Guid.NewGuid();
        var spaceName = "Linked " + Suffix();
        DroppedEmail? email = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var session = await SignedInAsync(client, address);
            await SeedNamedSpaceAsync(database, spaceId, spaceName, await AccountIdOfAsync(database, address), Role.Owner);

            await session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(session, spaceId, invitee, "member")).StatusCode);

            email = await mail.WaitForEmailToAsync(invitee);
        });

        var token = TokenOf(email!);
        var row = Assert.Single(await wire.InvitationsInAsync(spaceId));

        Assert.False(token.StartsWith("yello_pat_", StringComparison.Ordinal), "The Invitation token shares the API Token's prefix.");
        Assert.DoesNotContain(spaceId.ToString("D"), token, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(spaceId.ToString("N"), token, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(spaceName, Uri.UnescapeDataString(token), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(SHA256.HashData(Encoding.UTF8.GetBytes(token)), row.TokenHash);
        Assert.Contains(DateOnly.FromDateTime(row.ExpiresAt.UtcDateTime).ToString("O", CultureInfo.InvariantCulture), email!.PlainText, StringComparison.Ordinal);
    }

    /// <summary>
    /// An <c>AFTER INSERT</c> trigger on <c>IdempotencyRecord</c> that throws for one key: the forced failure after the
    /// enqueue. Dropped by the caller in a <c>finally</c>; it fires for no other key.
    /// </summary>
    /// <returns>The trigger's name, for <see cref="DropTriggerAsync"/>.</returns>
    private static async Task<string> PlantFailingIdempotencyTriggerAsync(RegistrationDatabase database, string key)
    {
        var name = "TR_IdempotencyRecord_ForcedFailure_" + Guid.NewGuid().ToString("N");

        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        // The key is a GUID formatted here, so it is safe to inline; LIKE matches whichever column the record keys on.
        _ = Guid.Parse(key);

#pragma warning disable CA2100 // Review SQL queries - DDL takes no parameters; the name and key are GUIDs formatted here.
        command.CommandText =
            $"CREATE TRIGGER dbo.[{name}] ON dbo.[IdempotencyRecord] AFTER INSERT AS " +
            $"IF EXISTS (SELECT 1 FROM inserted WHERE CONVERT(nvarchar(400), [Key]) = N'{key}') THROW 50000, 'Forced failure after the enqueue.', 1;";
#pragma warning restore CA2100

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        return name;
    }

    /// <summary>
    /// Whether the outbox holds no row for <paramref name="recipient"/> within the window - the delete follows the send,
    /// so it is polled with a deadline rather than read once.
    /// </summary>
    private static async Task<bool> OutboxEventuallyHoldsNoRowForAsync(WireDatabase wire, string recipient)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (!(await wire.OutboxRowsAsync()).Any(row => string.Equals(row.Recipient, recipient, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// An Account's address and display name. <c>Account</c> carries no policy.
    /// </summary>
    private static async Task<(string EmailAddress, string DisplayName)> IdentityOfAsync(RegistrationDatabase database, Guid accountId)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT EmailAddress, DisplayName FROM dbo.[Account] WHERE Id = @account";
        command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@account", accountId));

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken), $"No Account {accountId}.");

        return (reader.GetString(0), reader.GetString(1));
    }
}
