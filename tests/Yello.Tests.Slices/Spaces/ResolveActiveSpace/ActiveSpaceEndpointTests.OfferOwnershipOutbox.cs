using System.Net;
using Xunit;
using Yello.Contracts.Labels;
using Yello.Domain.Accounts;
using Yello.Tests.Shared;
using LabelWire = Yello.Tests.Slices.Tasks.LabelWire;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// The offer's email over the wire: exactly one, to the named recipient, enqueued through the outbox in the offer's own
/// transaction with its Space-scoped send record, and dispatched in process. It names the Space and the offerer, says the
/// recipient is being offered ownership, carries the expiry, and discloses nothing else: no content of the Space, no other
/// Membership, no other Space, no address, no id and no means of accepting. Nothing is sent on revoke or lapse, and neither
/// an address nor a display name reaches the Host's log.
/// </summary>
/// <remarks>
/// <para>
/// <b>Mail is read from a drop of the case's own</b>, through a Host of its own: the class's pooled Hosts each run a
/// dispatcher that would claim this case's outbox rows and send them elsewhere, so every case stops them first
/// (<see cref="SoleDispatcherWireAsync"/>) and never uses the pool afterwards. Every recipient is a fresh address, and every
/// wait is for the case's own email by recipient, never "any N".
/// </para>
/// <para>
/// <b>An absence of email is fenced</b>: after the act that must send nothing, a later email that must arrive (an Invitation
/// to a fresh address) is awaited first, so a late dispatch cannot hide behind a quick read.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// The acts after an offer that must send nothing: its revoke, the removal of its recipient, and the recipient leaving.
    /// </summary>
    public static TheoryData<string> SilentAfterTheOffer => new() { "revoke", "removal", "leave" };

    /// <summary>
    /// One offer sends exactly one offer email, to the recipient, and none to the offerer; a replay of the offer under its
    /// key sends no second one. The send record holds one <c>ownership-offered</c> row.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC7 - the offer route, OutboxKinds.OwnershipOffered and OwnershipOfferEmail do not exist yet")]
    [Trait("Requirement", "FR-43")]
    [Trait("Requirement", "AR-33")]
    [Trait("Requirement", "AD-10")]
    public async Task One_offer_sends_one_email_to_the_recipient_and_none_to_the_offerer()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString("D");
        OfferSpace? space = null;
        WireResponse? replay = null;
        IReadOnlyList<DroppedEmail> dropped = [];

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var recipient = space.Members["recipient"];
            var fence = InvitationWire.FreshAddress("offer-fence");

            Assert.Equal(HttpStatusCode.Created, (await OfferOverWireAsync(space.Owner, spaceId, recipient.MembershipId, key)).StatusCode);
            _ = await mail.WaitForEmailsToAsync(recipient.Address, OwnershipOfferWire.IsOwnershipOfferEmail);

            replay = await OfferOverWireAsync(space.Owner, spaceId, recipient.MembershipId, key);

            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(space.Owner, spaceId, fence, "member")).StatusCode);
            _ = await mail.WaitForEmailToAsync(fence);

            dropped = await mail.ReadAllAsync();
        });

        Assert.True(replay?.StatusCode == HttpStatusCode.Created, $"The replay: {Described(replay)}");
        Assert.Single(OwnershipOfferWire.OfferEmailsTo(dropped, space!.Members["recipient"].Address));
        Assert.Empty(OwnershipOfferWire.OfferEmailsTo(dropped, space.OwnerAddress));
        Assert.Equal(1, (await wire.NotificationSendsInAsync(spaceId)).Count(record => record.Kind == OwnershipOfferWire.OwnershipOfferedKind));
    }

    /// <summary>
    /// What the email says, and does not. The Space holds a Project, a Task, a Label, a third Member and a pending Invitation;
    /// the recipient also belongs to another Space with names of its own. The one offer email names the Space and the
    /// offerer in its subject and body and carries the expiry; it holds none of the Project, Task or Label names, the third
    /// Member's name or address, the invited address, the other Space's names, the recipient's or the offerer's address, the
    /// password, a Token prefix or any GUID (read from the subject and both bodies; the envelope's To is the recipient's own); and no link in it carries an id or names the offer routes or an accept route.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC8 - the offer route and OwnershipOfferEmail.Render do not exist yet")]
    [Trait("Requirement", "FR-43")]
    [Trait("Requirement", "NFR-1")]
    [Trait("Requirement", "NFR-6")]
    [Trait("Requirement", "AD-23")]
    [Trait("Assumption", "PRD-12-3")]
    public async Task The_offer_email_names_the_Space_and_the_offerer_and_discloses_nothing_else()
    {
        var database = Available();
        var (spaceId, otherSpaceId) = (Guid.NewGuid(), Guid.NewGuid());
        var (spaceName, ownerName, thirdName) = ("Harbour " + Suffix(), DisplayName(), DisplayName());
        var (projectName, title, labelName) = ("Quayside " + Suffix(), "Paint the bollards " + Suffix(), "Urgent " + Suffix());
        var (otherSpaceName, otherProjectName) = ("Secret works " + Suffix(), "Hidden project " + Suffix());
        var invitee = InvitationWire.FreshAddress("offer-disclosure");
        var otherOwnerAddress = RandomAddress();
        OfferSpace? space = null;
        IReadOnlyList<DroppedEmail> received = [];

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            space = await NamedOfferableSpaceAsync(client, database, wire, mail, (spaceId, spaceName), ownerName, ("recipient", DisplayName(), "member"), ("third", thirdName, "admin"));
            var recipient = space.Members["recipient"];

            var otherOwner = await SignedInAsync(client, otherOwnerAddress);
            await SeedAssignableSpaceAsync(database, otherSpaceId, otherSpaceName, await AccountIdOfAsync(database, otherOwnerAddress));
            await wire.AddMembershipAsync(otherSpaceId, recipient.AccountId, "member");
            _ = await NamedProjectAsync(otherOwner, otherSpaceId, otherProjectName);

            var project = await NamedProjectAsync(space.Owner, spaceId, projectName);
            _ = await TaskInProjectAsync(space.Owner, spaceId, project, title);
            Assert.Equal(HttpStatusCode.Created, (await space.Owner.SendWithHeadersAsync(HttpMethod.Post, LabelRoutes.LabelsOf(spaceId), LabelWire.DefinitionBody(labelName, LabelWire.Rust), NoKey)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(space.Owner, spaceId, invitee, "viewer")).StatusCode);

            Assert.Equal(HttpStatusCode.Created, (await OfferOverWireAsync(space.Owner, spaceId, recipient.MembershipId)).StatusCode);
            received = await mail.WaitForEmailsToAsync(recipient.Address, OwnershipOfferWire.IsOwnershipOfferEmail);
        });

        var email = Assert.Single(received);
        var content = string.Join('\n', email.Subject, email.PlainText, email.Html);
        var offerId = Assert.Single(await wire.OwnershipOffersInAsync(spaceId)).Id;
        var third = space!.Members["third"];

        Assert.Contains(ownerName, email.Subject, StringComparison.Ordinal);
        Assert.Contains(spaceName, email.Subject, StringComparison.Ordinal);
        Assert.Contains("offered you ownership of", email.PlainText, StringComparison.Ordinal);
        Assert.All(new[] { spaceName, ownerName }, name => Assert.Contains(name, email.PlainText, StringComparison.Ordinal));
        Assert.Contains(" UTC", email.PlainText, StringComparison.Ordinal);

        Assert.All(
            new[] { projectName, title, labelName, thirdName, otherSpaceName, otherProjectName, Password, "yello_pat_" },
            secret => Assert.False(content.Contains(secret, StringComparison.Ordinal), "The offer email holds something it must not disclose."));
        Assert.All(
            new[] { invitee, third.Address, space.Members["recipient"].Address, space.OwnerAddress, otherOwnerAddress, EmailAddressNormalisation.Normalise(invitee) },
            address => Assert.False(content.Contains(address, StringComparison.OrdinalIgnoreCase), "The offer email holds an address."));
        Assert.All(
            new[] { spaceId, otherSpaceId, offerId, space.Members["recipient"].MembershipId, space.OwnerMembershipId },
            id => Assert.DoesNotContain(id.ToString("D"), content, StringComparison.OrdinalIgnoreCase));
        Assert.False(OwnershipOfferWire.HoldsAGuid(content), "The offer email carries a GUID.");

        var links = OwnershipOfferWire.LinksIn(email.PlainText + "\n" + email.Html);

        Assert.NotEmpty(links);
        Assert.All(links, link => Assert.DoesNotContain("ownership-offers", link, StringComparison.OrdinalIgnoreCase));
        Assert.All(links, link => Assert.DoesNotContain("accept", link, StringComparison.OrdinalIgnoreCase));
        Assert.All(links, link => Assert.DoesNotContain("token", link, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The enqueue and the send record are in the offer's own transaction: a failure after them (the idempotency record's
    /// insert, forced) leaves no offer, no outbox row for the recipient, no <c>ownership-offered</c> send record and no email.
    /// Anchor: once the plant is gone, an offer under another key is stored, recorded and sent.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC7 - the offer route and its enqueue do not exist yet")]
    [Trait("Requirement", "AR-33")]
    [Trait("Requirement", "AD-10")]
    [Trait("Requirement", "FR-43")]
    public async Task A_failure_after_the_enqueue_rolls_back_the_offer_its_email_and_its_send_record()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString("D");
        var (offersAfterFailure, outboxAfterFailure, recordsAfterFailure) = (-1, -1, -1);
        WireResponse? failed = null;
        WireResponse? anchor = null;
        IReadOnlyList<DroppedEmail> received = [];

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var recipient = space.Members["recipient"];
            var trigger = await PlantFailingIdempotencyTriggerAsync(database, key);

            try
            {
                failed = await OfferOverWireAsync(space.Owner, spaceId, recipient.MembershipId, key);
            }
            finally
            {
                await DropTriggerAsync(database, trigger);
            }

            offersAfterFailure = (await wire.OwnershipOffersInAsync(spaceId)).Count;
            outboxAfterFailure = (await wire.OutboxRowsAsync()).Count(row => string.Equals(row.Recipient, recipient.Address, StringComparison.OrdinalIgnoreCase) && row.Kind == OwnershipOfferWire.OwnershipOfferedKind);
            recordsAfterFailure = (await wire.NotificationSendsInAsync(spaceId)).Count(record => record.Kind == OwnershipOfferWire.OwnershipOfferedKind);

            anchor = await OfferOverWireAsync(space.Owner, spaceId, recipient.MembershipId, Guid.NewGuid().ToString("D"));
            received = await mail.WaitForEmailsToAsync(recipient.Address, OwnershipOfferWire.IsOwnershipOfferEmail);
        });

        Assert.True(failed?.StatusCode >= HttpStatusCode.InternalServerError, $"The forced failure: {Described(failed)}");
        Assert.Equal((0, 0, 0), (offersAfterFailure, outboxAfterFailure, recordsAfterFailure));
        Assert.True(anchor?.StatusCode == HttpStatusCode.Created, $"The anchor: {Described(anchor)}");
        Assert.Single(received);
        Assert.Single(await wire.OwnershipOffersInAsync(spaceId));
        Assert.Equal(1, (await wire.NotificationSendsInAsync(spaceId)).Count(record => record.Kind == OwnershipOfferWire.OwnershipOfferedKind));
    }

    /// <summary>
    /// Nothing is sent on revoke or lapse: after the offer's own email has arrived, the revoke, the recipient's removal or the
    /// recipient's leave is followed by a fence email that must arrive; the recipient then still has exactly one offer email
    /// and the Space exactly one <c>ownership-offered</c> send record.
    /// </summary>
    /// <param name="act">What follows the offer.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC7 - the offer and revoke routes and the lapse do not exist yet")]
    [MemberData(nameof(SilentAfterTheOffer))]
    [Trait("Requirement", "FR-43")]
    [Trait("Requirement", "AD-26")]
    public async Task Revoking_removing_the_recipient_or_its_leaving_sends_nothing(string act)
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        OfferSpace? space = null;
        WireResponse? acted = null;
        IReadOnlyList<DroppedEmail> dropped = [];

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var recipient = space.Members["recipient"];
            var fence = InvitationWire.FreshAddress("silent-fence");
            var offer = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            _ = await mail.WaitForEmailsToAsync(recipient.Address, OwnershipOfferWire.IsOwnershipOfferEmail);

            if (act == "leave")
            {
                await recipient.Session.FetchAntiforgeryTokenAsync();
            }

            acted = act switch
            {
                "revoke" => await RevokeOfferOverWireAsync(space.Owner, spaceId, offer.Id),
                "removal" => await RemoveOverWireAsync(space.Owner, spaceId, recipient.MembershipId),
                _ => await LeaveOverWireAsync(recipient.Session, spaceId),
            };

            await space.Owner.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(space.Owner, spaceId, fence, "member")).StatusCode);
            _ = await mail.WaitForEmailToAsync(fence);

            dropped = await mail.ReadAllAsync();
        });

        Assert.True(acted?.StatusCode == HttpStatusCode.NoContent, $"The {act}: {Described(acted)}");
        Assert.Single(OwnershipOfferWire.OfferEmailsTo(dropped, space!.Members["recipient"].Address));
        Assert.Equal(1, (await wire.NotificationSendsInAsync(spaceId)).Count(record => record.Kind == OwnershipOfferWire.OwnershipOfferedKind));
    }

    /// <summary>
    /// Neither an address nor a display name - the Owner's, the recipient's, as written or normalised - reaches the Host's log
    /// across an offer, a read, a refused second offer and a revoke. Anchor: the log is the Host's and each request was
    /// served as asserted.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC8, AC12 - the offer, read and revoke routes do not exist yet")]
    [Trait("Requirement", "NFR-6")]
    [Trait("Requirement", "AD-23")]
    public async Task Neither_an_address_nor_a_display_name_reaches_the_Host_log_on_offer_read_or_revoke()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var (ownerName, recipientName, otherName) = (DisplayName(), DisplayName(), DisplayName());
        OfferSpace? space = null;
        var answers = new List<(HttpStatusCode Expected, WireResponse Answer)>();

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, ownerName, ("recipient", recipientName, "member"), ("other", otherName, "member"));

            var offered = await OfferOverWireAsync(space.Owner, spaceId, space.Members["recipient"].MembershipId);

            answers.Add((HttpStatusCode.Created, offered));
            answers.Add((HttpStatusCode.OK, await PendingOfferOverWireAsync(space.Owner, spaceId)));
            answers.Add((HttpStatusCode.Conflict, await OfferOverWireAsync(space.Owner, spaceId, space.Members["other"].MembershipId)));

            if (offered.StatusCode == HttpStatusCode.Created)
            {
                answers.Add((HttpStatusCode.NoContent, await RevokeOfferOverWireAsync(space.Owner, spaceId, OwnershipOfferWire.Parse(offered.Body).Id)));
            }

            _ = await mail.WaitForEmailsToAsync(space.Members["recipient"].Address, OwnershipOfferWire.IsOwnershipOfferEmail);
        });

        Assert.Contains(HostProcess.ListeningMarker, log, StringComparison.Ordinal);
        Assert.Equal(4, answers.Count);
        Assert.All(answers, pair => Assert.True(pair.Answer.StatusCode == pair.Expected, Described(pair.Answer)));

        var recipientAddress = space!.Members["recipient"].Address;

        foreach (var secret in new[] { space.OwnerAddress, recipientAddress, EmailAddressNormalisation.Normalise(recipientAddress), ownerName, recipientName, otherName })
        {
            // Counted, never printed: a failure message that quoted the secret would itself be the leak.
            Assert.False(log.Contains(secret, StringComparison.OrdinalIgnoreCase), "The Host log holds an address or a display name.");
        }
    }
}
