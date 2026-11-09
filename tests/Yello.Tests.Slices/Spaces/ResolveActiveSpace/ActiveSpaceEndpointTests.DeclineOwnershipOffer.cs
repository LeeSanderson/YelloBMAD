using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// The recipient's decline over the wire (Lee's Q3 (a): built with the acceptance):
/// <c>POST /api/v1/spaces/{spaceId}/ownership-offers/{offerId}/decline</c>, no body, <c>204</c>. The same identity guard and
/// the same disambiguation as the acceptance, writing <c>Declined</c>: no Role moves, nothing is published and nothing is
/// emailed. Telling the offering Owner, and the proofs across every settled state, stay with the later story.
/// </summary>
/// <remarks>
/// <b>Red-phase scaffolds.</b> Every case is skipped until the decline exists; the route is
/// <see cref="OwnershipOfferWire.DeclineOf"/>'s stand-in. Seeding is as in <c>.AcceptOwnershipOffer.cs</c>.
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// The named recipient declines: <c>204</c> with no body; the row is retained with <c>State = Declined</c> and every other
    /// column unchanged; every Membership and Role is exactly as before; no publish names either party (the Owner is never
    /// joined, and the recipient is named as often as a bystander who joined the same way); the recipient's read then answers
    /// <c>204</c>; and a declined offer cannot afterwards be accepted (<c>409 ownership-offer-not-pending</c>).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 (Q3 a) - the decline route does not exist yet")]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AR-12")]
    public async Task The_named_recipient_declines_no_Role_moves_and_the_offer_cannot_then_be_accepted()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var (ownerAccount, recipientAccount, bystanderAccount) = (Guid.Empty, Guid.Empty, Guid.Empty);
        IReadOnlyList<MembershipRow> before = [];
        OwnershipOfferRow? pending = null;
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("bystander", DisplayName(), "member"));
            var recipient = space.Members["recipient"];
            var offer = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            (ownerAccount, recipientAccount, bystanderAccount) = (space.OwnerAccountId, recipient.AccountId, space.Members["bystander"].AccountId);
            before = await wire.MembershipRowsInAsync(spaceId);
            pending = Assert.Single(await wire.OwnershipOffersInAsync(spaceId));

            await recipient.Session.FetchAntiforgeryTokenAsync();
            answers["decline"] = await DeclineOverWireAsync(recipient.Session, spaceId, offer.Id);
            answers["read"] = await MineOverWireAsync(recipient.Session, spaceId);
            answers["accept"] = await AcceptOverWireAsync(recipient.Session, spaceId, offer.Id);
        });

        Assert.True(answers["decline"].StatusCode == HttpStatusCode.NoContent, $"The decline: {Described(answers["decline"])}");
        Assert.Equal(string.Empty, answers["decline"].Body);
        Assert.Equal(pending! with { State = "Declined" }, Assert.Single(await wire.OwnershipOffersInAsync(spaceId)));
        Assert.Equal(before, await wire.MembershipRowsInAsync(spaceId));
        Assert.Equal(0, PublishesNaming(log, ownerAccount, spaceId));
        Assert.Equal(PublishesNaming(log, bystanderAccount, spaceId), PublishesNaming(log, recipientAccount, spaceId));
        Assert.True(answers["read"].StatusCode == HttpStatusCode.NoContent, $"The read after the decline: {Described(answers["read"])}");
        Assert.True(answers["accept"].StatusCode == HttpStatusCode.Conflict, $"Accepting the declined offer: {Described(answers["accept"])}");
        Assert.Equal(ProblemTypes.OwnershipOfferNotPending, ProblemType(answers["accept"]));
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// Nobody but the named Membership can decline: another Member, an Admin and the Owner each get the in-Space not-found on
    /// the offer's real id, byte for byte the answer for a random id and for a Space that does not exist, unrecorded; the
    /// offer stays <c>Pending</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 (Q3 a), AC4 - the decline route and its identity clause do not exist yet")]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AD-3")]
    public async Task A_decline_by_anyone_the_offer_does_not_name_is_the_in_Space_404_and_nothing_changes()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var actors = new Dictionary<string, Guid>(StringComparer.Ordinal);
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("member", DisplayName(), "member"), ("admin", DisplayName(), "admin"));
            var offer = await OfferedAsync(space.Owner, spaceId, space.Members["recipient"].MembershipId);

            actors["owner"] = space.OwnerAccountId;
            actors["member"] = space.Members["member"].AccountId;
            actors["admin"] = space.Members["admin"].AccountId;

            await space.Members["member"].Session.FetchAntiforgeryTokenAsync();
            await space.Members["admin"].Session.FetchAntiforgeryTokenAsync();

            answers["absent"] = await DeclineOverWireAsync(space.Owner, Guid.NewGuid(), Guid.NewGuid());
            answers["random"] = await DeclineOverWireAsync(space.Members["member"].Session, spaceId, Guid.NewGuid());
            answers["member"] = await DeclineOverWireAsync(space.Members["member"].Session, spaceId, offer.Id);
            answers["admin"] = await DeclineOverWireAsync(space.Members["admin"].Session, spaceId, offer.Id);
            answers["owner"] = await DeclineOverWireAsync(space.Owner, spaceId, offer.Id);
        });

        Assert.Equal(HttpStatusCode.NotFound, answers["absent"].StatusCode);

        foreach (var name in new[] { "random", "member", "admin", "owner" })
        {
            Assert.True(answers[name].StatusCode == HttpStatusCode.NotFound, $"The {name}'s decline: {Described(answers[name])}");
            Assert.Equal(ProblemTypes.NotAvailable, ProblemType(answers[name]));
            Assert.Equal(answers["absent"].Body, answers[name].Body);
            Assert.Equal(answers["absent"].HeaderNames, answers[name].HeaderNames);
        }

        foreach (var (name, accountId) in actors)
        {
            Assert.True(
                !(await RefusalsOfAsync(database, accountId)).Any(row => row.TargetSpaceId == spaceId),
                $"The {name}'s refused decline was recorded against the Space.");
        }

        Assert.Equal("Pending", Assert.Single(await wire.OwnershipOffersInAsync(spaceId)).State);
        await AssertOneOwnerEachAsync(wire, spaceId);
    }

    /// <summary>
    /// The decline under its <c>Idempotency-Key</c>: a replay answers the original <c>204</c>; a second decline under a new
    /// key is <c>409 ownership-offer-not-pending</c>; a malformed key is <c>400 invalid-idempotency-key</c>. One row, <c>Declined</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 (Q3 a), AC10 - the decline route does not exist yet")]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "AD-26")]
    public async Task A_decline_replays_under_its_key_and_a_new_key_is_refused_409_ownership_offer_not_pending()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString("D");
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var recipient = space.Members["recipient"];
            var offer = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);

            await recipient.Session.FetchAntiforgeryTokenAsync();
            answers["malformed"] = await DeclineOverWireAsync(recipient.Session, spaceId, offer.Id, new string('k', Idempotency.MaxKeyLength + 1));
            answers["original"] = await DeclineOverWireAsync(recipient.Session, spaceId, offer.Id, key);
            answers["replay"] = await DeclineOverWireAsync(recipient.Session, spaceId, offer.Id, key);
            answers["fresh"] = await DeclineOverWireAsync(recipient.Session, spaceId, offer.Id, Guid.NewGuid().ToString("D"));
        });

        Assert.True(answers["malformed"].StatusCode == HttpStatusCode.BadRequest, $"The malformed key: {Described(answers["malformed"])}");
        Assert.Equal(ProblemTypes.InvalidIdempotencyKey, ProblemType(answers["malformed"]));
        Assert.True(answers["original"].StatusCode == HttpStatusCode.NoContent, $"The original: {Described(answers["original"])}");
        Assert.Equal(answers["original"].StatusCode, answers["replay"].StatusCode);
        Assert.Equal(answers["original"].Body, answers["replay"].Body);
        Assert.True(answers["fresh"].StatusCode == HttpStatusCode.Conflict, $"A second decline: {Described(answers["fresh"])}");
        Assert.Equal(ProblemTypes.OwnershipOfferNotPending, ProblemType(answers["fresh"]));
        Assert.Equal("Declined", Assert.Single(await wire.OwnershipOffersInAsync(spaceId)).State);
    }

    /// <summary>
    /// The decline as the browser sends it: the Session's cookie and anti-forgery header, no body, and an
    /// <c>Idempotency-Key</c> when one is given.
    /// </summary>
    private static Task<WireResponse> DeclineOverWireAsync(WireSession session, Guid spaceId, Guid offerId, string? key = null) =>
        session.SendWithHeadersAsync(HttpMethod.Post, OwnershipOfferWire.DeclineOf(spaceId, offerId), content: null, KeyHeaders(key));
}
