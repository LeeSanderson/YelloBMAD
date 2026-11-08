using System.Net;
using System.Net.Http.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Projects;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using Yello.Contracts.Spaces;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Offering ownership of a Space over the wire: <c>POST /api/v1/spaces/{spaceId}/ownership-offers</c> under
/// <c>Capability.OfferOwnershipTransfer</c>, body <c>{ membershipId }</c>, answered <c>201</c> with the offer and a
/// <c>Location</c> of the pending read. The offer names an existing Membership of the Space at any Role and moves nothing:
/// the offering Owner keeps every capability, at most one offer is pending per Space, a time-expired pending row is lapsed by
/// the next offer rather than blocking it, the expiry is the database's clock plus seven days, and an offer is not an exit.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every refusal is anchored</b> on a <c>201</c> from the same route in the same case: an unmapped path answers the
/// boundary <c>404</c>, so a refusal asserted alone would pass against nothing.
/// </para>
/// <para>
/// <b>Status codes follow the comparable endpoints.</b> The offer is a creation that answers with what it made, as a Project,
/// Label, Task or Space creation does (<c>201</c>, <c>Location</c> set on the response so the idempotency record replays it);
/// it is not the Invitation issue's <c>204</c>, which answers alike whether or not an address has an Account. A body naming
/// no Membership is <c>400 invalid-offer-recipient</c>, as the assignment's is <c>400 invalid-assignee</c>.
/// </para>
/// <para>
/// <b>Seeding.</b> Every recipient joins through the product (an Invitation at its Role, accepted), so its Membership is the
/// product's. The Space and its Owner are seeded (<see cref="SpaceSeeding"/>) under an Account registered through the
/// product. Time-expired and terminal offers are seeded directly (<see cref="WireDatabase.AddOwnershipOfferAsync"/>): expiry
/// is reached only by seeding the past. Every case reads mail (the join's Invitation token), so it stops the class's pooled
/// Hosts first (<see cref="SoleDispatcherWireAsync"/>) and runs a Host of its own.
/// </para>
/// <para>
/// <b>Seed constraints checked.</b> <c>FK_Membership_Account_AccountId</c> (Restrict): every Membership names an Account
/// registered through the product first. <c>AK_Membership_SpaceId_AccountId</c> and <c>UX_Membership_SpaceId_Owner</c>: one
/// Membership per Account per Space, one Owner per seeded Space; the other Memberships are written by the accept. The offer
/// table's only key is <c>FK_OwnershipOffer_Space_SpaceId</c> (no key to <c>Membership</c> or <c>Account</c>), and a
/// seeded row is written under its own Space's key so <c>BLOCK AFTER INSERT</c> admits it; at most one seeded row per Space is
/// <c>Pending</c> (<c>UX_OwnershipOffer_SpaceId_Pending</c>).
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// The Roles an offer may name: any but the Owner's.
    /// </summary>
    public static TheoryData<string> OwnershipRecipientRoles => new() { "admin", "member", "viewer" };

    /// <summary>
    /// Bodies that name no Membership: an empty object, an array, a string and JSON <c>null</c>.
    /// </summary>
    public static TheoryData<string> BodiesNamingNoMembership => new() { "{}", "[]", "\"someone\"", "null" };

    /// <summary>
    /// The Owner offers ownership to a Membership of each lesser Role: <c>201</c>, the body is exactly the offer's five members
    /// naming that Membership by id and display name, <c>Location</c> is the pending read, and one <c>Pending</c> row names the
    /// Space, the recipient, the Owner's Membership and the Owner's name as it was when the offer was made.
    /// </summary>
    /// <param name="recipientRole">The recipient's Role.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(OwnershipRecipientRoles))]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "AD-26")]
    public async Task The_Owner_offers_ownership_to_a_Membership_of_any_Role_and_one_offer_is_pending(string recipientRole)
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var (ownerName, recipientName) = (DisplayName(), DisplayName());
        OfferSpace? space = null;
        WireResponse? offered = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, ownerName, ("recipient", recipientName, recipientRole));
            offered = await OfferOverWireAsync(space.Owner, spaceId, space.Members["recipient"].MembershipId);
        });

        var recipient = space!.Members["recipient"];

        Assert.True(offered?.StatusCode == HttpStatusCode.Created, $"The offer to a {recipientRole}: {Described(offered)}");
        Assert.Equal(OwnershipOfferWire.ResponseMembers, MembershipWire.ObjectMemberNames(offered.Body));

        var answer = OwnershipOfferWire.Parse(offered.Body);

        Assert.Equal(recipient.MembershipId, answer.RecipientMembershipId);
        Assert.Equal(recipientName, answer.RecipientName);
        Assert.True(offered.Headers.TryGetValue("Location", out var location), "The 201 carries no Location.");
        Assert.EndsWith(OwnershipOfferRoutes.PendingOf(spaceId), Assert.Single(location), StringComparison.Ordinal);

        var row = Assert.Single(await wire.OwnershipOffersInAsync(spaceId));

        Assert.Equal(new OwnershipOfferRow(answer.Id, spaceId, recipient.MembershipId, space.OwnerMembershipId, ownerName, "Pending", row.ExpiresAt, row.CreatedAt), row);
        Assert.Equal(answer.ExpiresAt, row.ExpiresAt);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// Making the offer moves nothing: every Membership's Role is as before, the offering Owner still renames the Space and
    /// reads the pending offer (both Owner-only), the Viewer recipient is still refused a write (<c>403</c> naming
    /// <c>manage-projects</c>), and the offer publishes no Membership change: none names the Owner, and the Viewer recipient
    /// is named exactly as often as the Admin who joined the same way and was not offered anything.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "AR-12")]
    [Trait("Requirement", "AD-5")]
    public async Task An_offer_moves_no_Role_and_the_Owner_keeps_every_Owner_capability()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        OfferSpace? space = null;
        IReadOnlyList<MembershipRow> before = [];
        WireResponse? offered = null;
        WireResponse? renamed = null;
        WireResponse? read = null;
        WireResponse? viewerWrite = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("viewer", DisplayName(), "viewer"), ("admin", DisplayName(), "admin"));
            var viewer = space.Members["viewer"];

            before = await wire.MembershipRowsInAsync(spaceId);
            offered = await OfferOverWireAsync(space.Owner, spaceId, viewer.MembershipId);
            renamed = await RenameSpaceAsync(space.Owner, spaceId, "Still mine " + Suffix());
            read = await PendingOfferOverWireAsync(space.Owner, spaceId);

            await viewer.Session.FetchAntiforgeryTokenAsync();
            viewerWrite = await viewer.Session.SendWithHeadersAsync(HttpMethod.Post, ProjectRoutes.ListIn(spaceId), JsonContent.Create(new { name = "Not yours " + Suffix() }, options: JsonOptions), NoKey);
        });

        Assert.True(offered?.StatusCode == HttpStatusCode.Created, $"The offer: {Described(offered)}");
        Assert.Equal(before, await wire.MembershipRowsInAsync(spaceId));
        Assert.True(renamed?.StatusCode == HttpStatusCode.OK, $"The offering Owner's rename: {Described(renamed)}");
        Assert.True(read?.StatusCode == HttpStatusCode.OK, $"The offering Owner's read of its offer: {Described(read)}");
        Assert.True(viewerWrite?.StatusCode == HttpStatusCode.Forbidden, $"The Viewer recipient's write: {Described(viewerWrite)}");
        Assert.Equal("manage-projects", CapabilityOf(viewerWrite));
        Assert.Equal(0, PublishesNaming(log, space!.OwnerAccountId, spaceId));
        Assert.Equal(PublishesNaming(log, space.Members["admin"].AccountId, spaceId), PublishesNaming(log, space.Members["viewer"].AccountId, spaceId));
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// An offer names a Membership and nothing else: a body carrying an address instead, one naming an Invitation's id, one
    /// naming a registered Account with no Membership here, and one naming the recipient's own Account id (not its Membership
    /// id) are each <c>400 invalid-offer-recipient</c>, byte for byte the answer for a random id, and no offer is stored.
    /// Anchor: the recipient's Membership id is then offered <c>201</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "AD-23")]
    public async Task An_offer_to_an_address_an_Invitation_or_an_Account_is_the_400_a_random_id_gets()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var (invitee, outsiderAddress) = (InvitationWire.FreshAddress("offered-invitee"), RandomAddress());
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);
        var storedBeforeAnchor = -1;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var recipient = space.Members["recipient"];

            _ = await SignedInAsync(client, outsiderAddress);
            var outsider = await AccountIdOfAsync(database, outsiderAddress);

            await space.Owner.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await IssueAsync(space.Owner, spaceId, invitee, "member")).StatusCode);
            var invitation = Assert.Single(await wire.InvitationsInAsync(spaceId), row => row.EmailAddress == invitee).Id;

            answers["random"] = await OfferOverWireAsync(space.Owner, spaceId, Guid.NewGuid());
            answers["address"] = await RawOfferOverWireAsync(space.Owner, spaceId, $$"""{"emailAddress":"{{recipient.Address}}"}""");
            answers["invitation"] = await OfferOverWireAsync(space.Owner, spaceId, invitation);
            answers["outsider"] = await OfferOverWireAsync(space.Owner, spaceId, outsider);
            answers["account"] = await OfferOverWireAsync(space.Owner, spaceId, recipient.AccountId);

            storedBeforeAnchor = (await wire.OwnershipOffersInAsync(spaceId)).Count;
            answers["anchor"] = await OfferOverWireAsync(space.Owner, spaceId, recipient.MembershipId);
        });

        Assert.True(answers["anchor"].StatusCode == HttpStatusCode.Created, $"The anchor: {Described(answers["anchor"])}");

        foreach (var refused in new[] { "random", "address", "invitation", "outsider", "account" })
        {
            Assert.True(answers[refused].StatusCode == HttpStatusCode.BadRequest, $"The {refused} body: {Described(answers[refused])}");
            Assert.Equal(ProblemTypes.InvalidOfferRecipient, ProblemType(answers[refused]));
            Assert.Equal(answers["random"].Body, answers[refused].Body);
        }

        Assert.Equal(0, storedBeforeAnchor);
    }

    /// <summary>
    /// A body that is not an object, or an object with no <c>membershipId</c>, is <c>400 invalid-offer-recipient</c> and
    /// stores nothing. Anchor: a real recipient is then offered <c>201</c>.
    /// </summary>
    /// <param name="body">The refused body.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(BodiesNamingNoMembership))]
    [Trait("Requirement", "FR-8")]
    public async Task A_body_that_names_no_Membership_is_400_invalid_offer_recipient(string body)
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        WireResponse? refused = null;
        WireResponse? anchor = null;
        var storedBeforeAnchor = -1;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));

            refused = await RawOfferOverWireAsync(space.Owner, spaceId, body);
            storedBeforeAnchor = (await wire.OwnershipOffersInAsync(spaceId)).Count;
            anchor = await OfferOverWireAsync(space.Owner, spaceId, space.Members["recipient"].MembershipId);
        });

        Assert.True(refused?.StatusCode == HttpStatusCode.BadRequest, $"The body {body}: {Described(refused)}");
        Assert.Equal(ProblemTypes.InvalidOfferRecipient, ProblemType(refused));
        Assert.Equal(0, storedBeforeAnchor);
        Assert.True(anchor?.StatusCode == HttpStatusCode.Created, $"The anchor: {Described(anchor)}");
    }

    /// <summary>
    /// The Owner's own Membership, a random id and another Space's real Membership id are each <c>400
    /// invalid-offer-recipient</c> with one body, nothing is recorded as a refusal, and no offer is stored in either Space: the
    /// guarded insert proves "another Membership of this Space" in its own <c>WHERE</c>, and the answer says nothing about
    /// whether the id exists elsewhere. Anchor: a real recipient is then offered <c>201</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AD-20")]
    public async Task Offering_to_oneself_to_a_random_id_or_to_another_Spaces_Membership_is_one_400_and_unrecorded()
    {
        var database = Available();
        var (spaceId, otherSpaceId) = (Guid.NewGuid(), Guid.NewGuid());
        var otherOwnerAddress = RandomAddress();
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);
        var ownerId = Guid.Empty;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));

            ownerId = space.OwnerAccountId;
            _ = await SignedInAsync(client, otherOwnerAddress);
            await SeedNamedSpaceAsync(database, otherSpaceId, "Elsewhere " + Suffix(), await AccountIdOfAsync(database, otherOwnerAddress), Role.Owner);
            var foreign = await wire.MembershipOfAsync(otherSpaceId, await AccountIdOfAsync(database, otherOwnerAddress));

            answers["random"] = await OfferOverWireAsync(space.Owner, spaceId, Guid.NewGuid());
            answers["self"] = await OfferOverWireAsync(space.Owner, spaceId, space.OwnerMembershipId);
            answers["foreign"] = await OfferOverWireAsync(space.Owner, spaceId, foreign.Id);
            answers["anchor"] = await OfferOverWireAsync(space.Owner, spaceId, space.Members["recipient"].MembershipId);
        });

        Assert.True(answers["anchor"].StatusCode == HttpStatusCode.Created, $"The anchor: {Described(answers["anchor"])}");

        foreach (var refused in new[] { "random", "self", "foreign" })
        {
            Assert.True(answers[refused].StatusCode == HttpStatusCode.BadRequest, $"The {refused} recipient: {Described(answers[refused])}");
            Assert.Equal(ProblemTypes.InvalidOfferRecipient, ProblemType(answers[refused]));
            Assert.Equal(answers["random"].Body, answers[refused].Body);
        }

        Assert.DoesNotContain(await RefusalsOfAsync(database, ownerId), row => row.TargetSpaceId == spaceId);
        Assert.Single(await wire.OwnershipOffersInAsync(spaceId));
        Assert.Empty(await wire.OwnershipOffersInAsync(otherSpaceId));
    }

    /// <summary>
    /// At most one offer is pending per Space: with one pending, an offer to another recipient under another key is
    /// <c>409 ownership-offer-pending</c>, and the Space still holds exactly one <c>Pending</c> row, naming the first
    /// recipient.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AR-12")]
    public async Task A_second_offer_while_one_is_pending_is_409_and_one_offer_stays_pending()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        OfferSpace? space = null;
        WireResponse? first = null;
        WireResponse? second = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("first", DisplayName(), "member"), ("second", DisplayName(), "admin"));
            first = await OfferOverWireAsync(space.Owner, spaceId, space.Members["first"].MembershipId, Guid.NewGuid().ToString("D"));
            second = await OfferOverWireAsync(space.Owner, spaceId, space.Members["second"].MembershipId, Guid.NewGuid().ToString("D"));
        });

        Assert.True(first?.StatusCode == HttpStatusCode.Created, $"The first offer: {Described(first)}");
        Assert.True(second?.StatusCode == HttpStatusCode.Conflict, $"The second offer: {Described(second)}");
        Assert.Equal(ProblemTypes.OwnershipOfferPending, ProblemType(second));
        Assert.DoesNotContain("@", second.Body, StringComparison.Ordinal);

        var pending = Assert.Single(await wire.OwnershipOffersInAsync(spaceId));

        Assert.Equal("Pending", pending.State);
        Assert.Equal(space!.Members["first"].MembershipId, pending.RecipientMembershipId);
    }

    /// <summary>
    /// A pending row past its <c>ExpiresAt</c> does not hold the Space's one pending place: the next offer, under the
    /// Memberships lock and in its own transaction, writes <c>Lapsed</c> on it before inserting, answers <c>201</c>, and the
    /// Space holds the lapsed row (retained) and exactly one <c>Pending</c> row. Without that write the filtered index refuses
    /// the offer <c>409</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "AD-27")]
    [Trait("Requirement", "AR-31")]
    [Trait("Assumption", "PRD-12-3")]
    public async Task A_time_expired_pending_offer_is_lapsed_by_the_next_offer_and_does_not_block_it()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var expired = Guid.Empty;
        WireResponse? offered = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("stale", DisplayName(), "member"), ("fresh", DisplayName(), "member"));
            var now = await wire.DatabaseNowAsync();

            expired = await wire.AddOwnershipOfferAsync(spaceId, space.Members["stale"].MembershipId, space.OwnerMembershipId, "Captured " + Suffix(), "Pending", now.AddDays(-1));
            offered = await OfferOverWireAsync(space.Owner, spaceId, space.Members["fresh"].MembershipId);
        });

        Assert.True(offered?.StatusCode == HttpStatusCode.Created, $"The offer over a time-expired one: {Described(offered)}");

        var rows = await wire.OwnershipOffersInAsync(spaceId);

        Assert.Equal("Lapsed", Assert.Single(rows, row => row.Id == expired).State);
        Assert.Single(rows, row => row.State == "Pending");
        Assert.Equal(2, rows.Count);
    }

    /// <summary>
    /// The expiry is the database's: a fresh offer's <c>ExpiresAt</c> lies between seven days after the database clock read
    /// just before the request and seven days after the one read just after it, the answer carries the stored value, and
    /// its <c>ReadAt</c> lies between the two reads. No C# clock stamps either.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-27")]
    [Trait("Requirement", "AR-31")]
    [Trait("Assumption", "PRD-12-3")]
    public async Task A_fresh_offer_expires_seven_days_after_the_database_clock()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var (before, after) = (DateTimeOffset.MinValue, DateTimeOffset.MinValue);
        WireResponse? offered = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "viewer"));

            before = await wire.DatabaseNowAsync();
            offered = await OfferOverWireAsync(space.Owner, spaceId, space.Members["recipient"].MembershipId);
            after = await wire.DatabaseNowAsync();
        });

        Assert.True(offered?.StatusCode == HttpStatusCode.Created, $"The offer: {Described(offered)}");

        var answer = OwnershipOfferWire.Parse(offered.Body);
        var row = Assert.Single(await wire.OwnershipOffersInAsync(spaceId));

        Assert.InRange(row.ExpiresAt, before.AddDays(OwnershipOfferWire.ExpiryDays), after.AddDays(OwnershipOfferWire.ExpiryDays));
        Assert.Equal(row.ExpiresAt, answer.ExpiresAt);
        Assert.InRange(answer.ReadAt, before, after);
    }

    /// <summary>
    /// Making an offer is not an exit: with an offer pending, the offering Owner's leave and its removal of its own Membership
    /// are each still <c>409 ownership-held</c>, the offer stays <c>Pending</c>, and the Space keeps its one Owner.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-8")]
    [Trait("Requirement", "FR-14")]
    [Trait("Requirement", "AD-5")]
    public async Task The_offering_Owner_still_cannot_leave_or_be_removed_while_an_offer_is_pending()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        WireResponse? offered = null;
        WireResponse? left = null;
        WireResponse? removed = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "admin"));

            offered = await OfferOverWireAsync(space.Owner, spaceId, space.Members["recipient"].MembershipId);
            left = await LeaveOverWireAsync(space.Owner, spaceId);
            removed = await RemoveOverWireAsync(space.Owner, spaceId, space.OwnerMembershipId);
        });

        Assert.True(offered?.StatusCode == HttpStatusCode.Created, $"The offer: {Described(offered)}");

        foreach (var (name, answer) in new[] { ("leave", left), ("removal", removed) })
        {
            Assert.True(answer?.StatusCode == HttpStatusCode.Conflict, $"The offering Owner's {name}: {Described(answer)}");
            Assert.Equal(ProblemTypes.OwnershipHeld, ProblemType(answer));
        }

        Assert.Equal("Pending", Assert.Single(await wire.OwnershipOffersInAsync(spaceId)).State);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// The same <c>Idempotency-Key</c> after a committed offer answers the original <c>201</c> byte for byte and stores and
    /// records nothing more; the key on another recipient is <c>422 idempotency-key-reused</c>; a malformed key is <c>400
    /// invalid-idempotency-key</c>. One <c>Pending</c> row and one <c>ownership-offered</c> send record remain.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "FR-43")]
    public async Task A_replayed_offer_answers_its_original_201_and_stores_and_records_nothing_more()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString("D");
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("other", DisplayName(), "member"));

            answers["original"] = await OfferOverWireAsync(space.Owner, spaceId, space.Members["recipient"].MembershipId, key);
            answers["replay"] = await OfferOverWireAsync(space.Owner, spaceId, space.Members["recipient"].MembershipId, key);
            answers["reused"] = await OfferOverWireAsync(space.Owner, spaceId, space.Members["other"].MembershipId, key);
            answers["malformed"] = await OfferOverWireAsync(space.Owner, spaceId, space.Members["other"].MembershipId, new string('k', Idempotency.MaxKeyLength + 1));
        });

        Assert.True(answers["original"].StatusCode == HttpStatusCode.Created, $"The original: {Described(answers["original"])}");
        Assert.Equal(answers["original"].StatusCode, answers["replay"].StatusCode);
        Assert.Equal(answers["original"].Body, answers["replay"].Body);
        Assert.Equal(answers["original"].Headers["Location"], answers["replay"].Headers["Location"]);
        Assert.True(answers["reused"].StatusCode == HttpStatusCode.UnprocessableContent, $"The reused key: {Described(answers["reused"])}");
        Assert.Equal(ProblemTypes.IdempotencyKeyReused, ProblemType(answers["reused"]));
        Assert.True(answers["malformed"].StatusCode == HttpStatusCode.BadRequest, $"The malformed key: {Described(answers["malformed"])}");
        Assert.Equal(ProblemTypes.InvalidIdempotencyKey, ProblemType(answers["malformed"]));
        Assert.Single(await wire.OwnershipOffersInAsync(spaceId));
        Assert.Equal(1, (await wire.NotificationSendsInAsync(spaceId)).Count(record => record.Kind == OwnershipOfferWire.OwnershipOfferedKind));
    }

    /// <summary>
    /// A seeded Space owned by a fresh Account registered through the product under <paramref name="ownerName"/>, and one
    /// fresh Account per joiner, each joined at its Role through an Invitation the Owner issues and the joiner accepts.
    /// </summary>
    private static Task<OfferSpace> OfferableSpaceAsync(
        HttpClient client,
        RegistrationDatabase database,
        WireDatabase wire,
        MailDrop mail,
        Guid spaceId,
        string ownerName,
        params (string Key, string DisplayName, string Role)[] joiners) =>
        NamedOfferableSpaceAsync(client, database, wire, mail, (spaceId, "Offered " + Suffix()), ownerName, joiners);

    /// <summary>
    /// As <see cref="OfferableSpaceAsync"/>, with the Space's name chosen by the case. The Space carries one Status, so a Task
    /// can be created in it (a seeded Space carries none).
    /// </summary>
    private static async Task<OfferSpace> NamedOfferableSpaceAsync(
        HttpClient client,
        RegistrationDatabase database,
        WireDatabase wire,
        MailDrop mail,
        (Guid Id, string Name) seeded,
        string ownerName,
        params (string Key, string DisplayName, string Role)[] joiners)
    {
        var spaceId = seeded.Id;
        var ownerAddress = RandomAddress();
        var owner = await RegisteredAndSignedInAsync(client, ownerAddress, ownerName);
        var ownerId = await AccountIdOfAsync(database, ownerAddress);

        await SeedAssignableSpaceAsync(database, spaceId, seeded.Name, ownerId);

        var members = new Dictionary<string, OfferMember>(StringComparer.Ordinal);

        foreach (var (key, displayName, role) in joiners)
        {
            var address = RandomAddress();
            var session = await JoinedThroughTheProductAsync(client, wire, mail, (owner, spaceId), address, displayName, role);
            var accountId = await AccountIdOfAsync(database, address);

            members[key] = new OfferMember(session, address, displayName, accountId, (await wire.MembershipOfAsync(spaceId, accountId)).Id);
        }

        await owner.FetchAntiforgeryTokenAsync();

        return new OfferSpace(owner, ownerAddress, ownerId, (await wire.MembershipOfAsync(spaceId, ownerId)).Id, members);
    }

    /// <summary>
    /// The offer as the browser sends it: the Session's cookie and anti-forgery header, <c>{ membershipId }</c>, and an
    /// <c>Idempotency-Key</c> when one is given.
    /// </summary>
    private static Task<WireResponse> OfferOverWireAsync(WireSession session, Guid spaceId, Guid? membershipId, string? key = null) =>
        session.SendWithHeadersAsync(HttpMethod.Post, OwnershipOfferRoutes.OffersOf(spaceId), OwnershipOfferWire.Body(membershipId), KeyHeaders(key));

    /// <summary>
    /// The offer with a body of the case's own text.
    /// </summary>
    private static Task<WireResponse> RawOfferOverWireAsync(WireSession session, Guid spaceId, string json) =>
        session.SendWithHeadersAsync(HttpMethod.Post, OwnershipOfferRoutes.OffersOf(spaceId), OwnershipOfferWire.RawBody(json), NoKey);

    /// <summary>
    /// The Owner's read of the pending offer: the Session's cookie, a <c>GET</c>.
    /// </summary>
    private static Task<WireResponse> PendingOfferOverWireAsync(WireSession session, Guid spaceId) =>
        session.SendWithHeadersAsync(HttpMethod.Get, OwnershipOfferRoutes.PendingOf(spaceId), content: null, NoKey, withAntiforgeryToken: false);

    /// <summary>
    /// The revoke as the browser sends it: the Session's cookie and anti-forgery header, no body, and an
    /// <c>Idempotency-Key</c> when one is given.
    /// </summary>
    private static Task<WireResponse> RevokeOfferOverWireAsync(WireSession session, Guid spaceId, Guid offerId, string? key = null) =>
        session.SendWithHeadersAsync(HttpMethod.Delete, OwnershipOfferRoutes.OfferOf(spaceId, offerId), content: null, KeyHeaders(key));

    /// <summary>
    /// An offer made through the product, asserted <c>201</c>.
    /// </summary>
    private static async Task<OwnershipOfferAnswer> OfferedAsync(WireSession owner, Guid spaceId, Guid membershipId)
    {
        var offered = await OfferOverWireAsync(owner, spaceId, membershipId);

        Assert.True(offered.StatusCode == HttpStatusCode.Created, $"Offering through the product: {Described(offered)}");

        return OwnershipOfferWire.Parse(offered.Body);
    }

    /// <summary>
    /// No <c>Pending</c> offer in <paramref name="spaceId"/> names a Membership that no longer exists there.
    /// </summary>
    private static async Task AssertNoPendingOfferNamesAMissingMembershipAsync(WireDatabase wire, Guid spaceId)
    {
        var memberships = (await wire.MembershipRowsInAsync(spaceId)).Select(row => row.Id).ToHashSet();
        var orphaned = (await wire.OwnershipOffersInAsync(spaceId)).Where(row => row.State == "Pending" && !memberships.Contains(row.RecipientMembershipId)).ToList();

        Assert.True(orphaned.Count == 0, $"{orphaned.Count} Pending offer(s) name a Membership that no longer exists.");
    }

    /// <summary>
    /// A Space ready for an offer: its Owner (signed in, anti-forgery token fetched) and the joined Memberships by key.
    /// </summary>
    private sealed record OfferSpace(WireSession Owner, string OwnerAddress, Guid OwnerAccountId, Guid OwnerMembershipId, IReadOnlyDictionary<string, OfferMember> Members);

    /// <summary>
    /// One joined Membership: its Session, address, display name, Account and Membership.
    /// </summary>
    private sealed record OfferMember(WireSession Session, string Address, string Name, Guid AccountId, Guid MembershipId);
}
