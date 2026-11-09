using System.Net;
using Xunit;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// The recipient's read of the offer naming it: <c>GET /api/v1/spaces/{spaceId}/ownership-offers/mine</c>, declared under
/// <c>ReadProjectsAndTasks</c> (every Role), answering <c>200</c> with <c>OwnershipOfferForRecipientResponse(Id, OffererName,
/// ExpiresAt, ReadAt)</c> when an actionable offer names the caller's own Membership, and <c>204</c> otherwise. The Owner never
/// gets <c>200</c>: it cannot be named.
/// </summary>
/// <remarks>
/// <para>
/// <b>The offerer's name is the one captured when the offer was made</b>, never a live Account read: the case renames the
/// offering Owner after the offer and expects the captured name.
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// A Viewer recipient (any Role reads it) reads the offer naming it: <c>200</c>, exactly the four members, the offer's id,
    /// the offerer's name as captured on the row (not the name the Owner has since been given), the stored <c>ExpiresAt</c>,
    /// and a <c>ReadAt</c> from the database clock during the request; no <c>@</c> and no recipient id anywhere.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "UX-DR9")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AD-23")]
    [Trait("Requirement", "AD-27")]
    [Trait("Assumption", "PRD-12-3")]
    public async Task The_named_recipient_reads_its_offer_with_the_captured_offerer_name_and_the_database_time()
    {
        var database = Available();
        var spaceId = Guid.NewGuid();
        var recipientMembership = Guid.Empty;
        var (before, after) = (DateTimeOffset.MinValue, DateTimeOffset.MinValue);
        WireResponse? read = null;

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "viewer"));
            var recipient = space.Members["recipient"];

            recipientMembership = recipient.MembershipId;
            _ = await OfferedAsync(space.Owner, spaceId, recipient.MembershipId);
            await wire.SetDisplayNameAsync(space.OwnerAccountId, "Renamed " + Suffix());

            before = await wire.DatabaseNowAsync();
            read = await MineOverWireAsync(recipient.Session, spaceId);
            after = await wire.DatabaseNowAsync();
        });

        Assert.True(read?.StatusCode == HttpStatusCode.OK, $"The recipient's read: {Described(read)}");
        Assert.Equal(OwnershipOfferWire.RecipientResponseMembers, MembershipWire.ObjectMemberNames(read.Body));
        Assert.DoesNotContain("@", read.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(recipientMembership.ToString("D"), read.Body, StringComparison.OrdinalIgnoreCase);

        var answer = OwnershipOfferWire.ParseForRecipient(read.Body);
        var row = Assert.Single(await wire.OwnershipOffersInAsync(spaceId));

        Assert.Equal((row.Id, row.OffererName, row.ExpiresAt), (answer.Id, answer.OffererName, answer.ExpiresAt));
        Assert.InRange(answer.ReadAt, before, after);
    }

    /// <summary>
    /// The read answers <c>204</c> with no body to everyone the actionable offer does not name - another Member, an Admin,
    /// the Owner - and to the named recipient when its offer is past its expiry (a <c>Pending</c> row the shared predicate
    /// reads as none) or when the Space holds no offer. None of them is refused or recorded. Anchor: the named recipient's
    /// read of the live offer answers <c>200</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-42")]
    [Trait("Requirement", "UX-DR9")]
    [Trait("Requirement", "AD-26")]
    [Trait("Requirement", "AR-31")]
    [Trait("Assumption", "PRD-12-3")]
    public async Task The_recipients_read_answers_204_to_everyone_the_offer_does_not_name_and_for_an_expired_offer()
    {
        var database = Available();
        var (spaceId, expiredSpaceId, emptySpaceId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        var wire = await SoleDispatcherWireAsync();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            var space = await OfferableSpaceAsync(client, database, wire, mail, spaceId, DisplayName(), ("recipient", DisplayName(), "member"), ("member", DisplayName(), "member"), ("admin", DisplayName(), "admin"));

            _ = await OfferedAsync(space.Owner, spaceId, space.Members["recipient"].MembershipId);

            answers["anchor"] = await MineOverWireAsync(space.Members["recipient"].Session, spaceId);
            answers["another Member"] = await MineOverWireAsync(space.Members["member"].Session, spaceId);
            answers["an Admin"] = await MineOverWireAsync(space.Members["admin"].Session, spaceId);
            answers["the Owner"] = await MineOverWireAsync(space.Owner, spaceId);

            var expired = await OfferableSpaceAsync(client, database, wire, mail, expiredSpaceId, DisplayName(), ("recipient", DisplayName(), "member"));
            var now = await wire.DatabaseNowAsync();

            _ = await wire.AddOwnershipOfferAsync(expiredSpaceId, expired.Members["recipient"].MembershipId, expired.OwnerMembershipId, "Captured " + Suffix(), "Pending", now.AddMinutes(-1));
            answers["the recipient of an expired offer"] = await MineOverWireAsync(expired.Members["recipient"].Session, expiredSpaceId);

            var empty = await OfferableSpaceAsync(client, database, wire, mail, emptySpaceId, DisplayName(), ("member", DisplayName(), "member"));

            answers["a Member of a Space with no offer"] = await MineOverWireAsync(empty.Members["member"].Session, emptySpaceId);
        });

        Assert.True(answers["anchor"].StatusCode == HttpStatusCode.OK, $"The anchor: {Described(answers["anchor"])}");

        foreach (var (name, answer) in answers.Where(pair => pair.Key != "anchor"))
        {
            Assert.True(answer.StatusCode == HttpStatusCode.NoContent, $"The read by {name}: {Described(answer)}");
            Assert.Equal(string.Empty, answer.Body);
        }
    }

    /// <summary>
    /// The recipient's read: the Session's cookie, a <c>GET</c>.
    /// </summary>
    private static Task<WireResponse> MineOverWireAsync(WireSession session, Guid spaceId) =>
        session.SendWithHeadersAsync(HttpMethod.Get, OwnershipOfferRoutes.MineOf(spaceId), content: null, NoKey, withAntiforgeryToken: false);
}
