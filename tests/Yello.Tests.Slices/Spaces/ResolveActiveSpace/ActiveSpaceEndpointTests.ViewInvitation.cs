using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Contracts.Invitations;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 4.3 over the wire: the Invitation view, <c>GET /api/v1/invitations/offer</c> with the token in the
/// <c>Yello-Invitation-Token</c> header (Lee's Q4 (a)) - a safe, side-effect-free read that answers the Space's name, the
/// Role offered and the issuer's captured name, and nothing else (AC1, AC2), the same for no Session, any Session and a
/// bearer Token (Lee's Q7 (a)), with the addressee indicator read from a Session only (Lee's Q5 (a), AC7). FR-11, AD-28,
/// UX-DR18, I-14.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 4.3 ATDD run (2026-11-01), and activated by its dev phase
/// (2026-11-02)</b>, which swapped <see cref="InvitationWire"/>'s temporary stand-ins for the contract's
/// <c>InvitationRoutes.Offer</c>, <c>.Acceptance</c>, <c>.TokenHeader</c> and <c>ProblemTypes.InvitationNotValid</c>,
/// <c>.InvitationForAnotherAccount</c>, and deleted them. Each case was also seen red under its plant (story Debug Log).
/// </para>
/// <para>
/// <b>The 4.2 trap, avoided.</b> An unmapped path under <c>/api</c> already answers the boundary <c>404 not-available</c>,
/// so any <c>404</c> or absence asserted on these routes would pass today. Every case here first gets a served answer on
/// the same route (the offer's <c>200</c>, the accept's <c>200</c>), and the uniform <c>404</c> is asserted to be a type of
/// its own, never <c>not-available</c>.
/// </para>
/// <para>
/// <b>Seeding.</b> Accounts are registered through the product; the Space is seeded by <see cref="SpaceSeeding"/> with the
/// issuer as its Owner (<c>FK_Membership_*</c>, <c>AK_Membership_SpaceId_AccountId</c>, <c>UX_Membership_SpaceId_Owner</c>
/// and the BLOCK AFTER INSERT predicate hold, as in 4.1's partials); every Invitation is issued through 4.1's route and its
/// token read from the email (<see cref="MailDrop"/>, <see cref="InvitationWire.TokenIn"/>), so the token index Lee's Q1
/// (a) adds is written by the product, never by the test.
/// </para>
/// <para>
/// <b>Not here:</b> the accept (<c>.AcceptInvitation.cs</c>), the races (<c>.AcceptInvitationRace.cs</c>), the bound
/// (<c>.AcceptInvitationBound.cs</c>), the cross-Space rows on both surfaces (<c>IsolationCases.InvitationAcceptance.cs</c>)
/// and the duration (the isolation timing row <c>ISO-TIMING-INVITATION-NOT-VALID-SAME-DURATION</c>).
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// AC2, UX-DR18, I-14, Q5 (a) - a live Invitation's view answers <c>200</c> with exactly the Space's name, the Role's
    /// stable code and the issuer's name as captured at issue (and, without a Session, no addressee indicator); never the
    /// invited address in any form, the Space's, Invitation's or issuer's id, the expiry or the state. Plant 8: the body
    /// gains <c>SpaceId</c> or the address.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-11")]
    [Trait("Requirement", "UX-DR18")]
    [Trait("Requirement", "AR-32")]
    public async Task The_view_answers_exactly_the_Space_the_Role_and_the_issuer_of_a_live_Invitation()
    {
        var database = Available();
        var wire = Wire();
        Invited? invited = null;
        WireResponse? offer = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            invited = await InviteAsync(client, database, wire, mail, "offered", "admin");
            offer = await ViewAnonymouslyAsync(client, invited.Token);
        });

        Assert.True(offer?.StatusCode == HttpStatusCode.OK, $"{offer?.StatusCode} {offer?.Body}");
        Assert.Subset(InvitationWire.OfferMembers.ToHashSet(StringComparer.Ordinal), InvitationWire.MemberNames(offer!.Body).ToHashSet(StringComparer.Ordinal));

        var parsed = InvitationWire.ParseOffer(offer.Body);

        Assert.Equal((invited!.SpaceName, "admin", invited.IssuerName), (parsed.SpaceName, parsed.Role, parsed.IssuerName));
        Assert.Null(parsed.AddressedToYou);

        foreach (var undisclosed in invited.Undisclosed())
        {
            Assert.DoesNotContain(undisclosed, offer.Body, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("@", offer.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Pending", offer.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires", offer.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// AC1, FR-11 <c>:277</c>, AD-28 - fetching the view three times (as a scanner, a prefetcher and a forwarded link would)
    /// writes nothing: the Invitation row is unchanged in every column, the Space's Membership count is unchanged, no outbox
    /// row and no idempotency record is added, and nothing is recorded as a refusal. Anchor: the same Invitation accepted
    /// afterwards does change the count - so the view's "nothing" is not a route that never ran. Plant 1: a view that sets
    /// <c>State = Accepted</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-11")]
    [Trait("Requirement", "AR-32")]
    [Trait("Requirement", "AD-28")]
    public async Task Viewing_an_Invitation_writes_nothing_and_only_the_accept_changes_anything()
    {
        var database = Available();
        var wire = Wire();
        Invited? invited = null;
        InvitationRow? before = null;
        InvitationRow? afterViews = null;
        var membershipsBefore = 0;
        var membershipsAfterViews = 0;
        var outboxBefore = 0;
        var recordsBefore = 0;
        var recordsAfterViews = 0;
        var views = new List<WireResponse>();
        WireResponse? accepted = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            invited = await InviteAsync(client, database, wire, mail, "safe-read", "member");

            var invitee = await RegisteredAndSignedInAsync(client, invited.Invitee, DisplayName());

            before = Assert.Single(await wire.InvitationsInAsync(invited.SpaceId));
            membershipsBefore = (await MembershipsInAsync(database, invited.SpaceId)).Count;
            outboxBefore = (await wire.OutboxRowsAsync()).Count;
            recordsBefore = await IdempotencyRecordCountAsync(database, invited.SpaceId);

            views.Add(await ViewAnonymouslyAsync(client, invited.Token));
            views.Add(await ViewAsync(invited.Owner, invited.Token));
            views.Add(await ViewAsync(invitee, invited.Token));

            membershipsAfterViews = (await MembershipsInAsync(database, invited.SpaceId)).Count;
            afterViews = Assert.Single(await wire.InvitationsInAsync(invited.SpaceId));
            recordsAfterViews = await IdempotencyRecordCountAsync(database, invited.SpaceId);

            await invitee.FetchAntiforgeryTokenAsync();
            accepted = await AcceptAsync(invitee, invited.Token);
        });

        Assert.All(views, view => Assert.True(view.StatusCode == HttpStatusCode.OK, $"{view.StatusCode} {view.Body}"));

        var after = Assert.Single(await wire.InvitationsInAsync(invited!.SpaceId));

        Assert.Equal("Accepted", after.State);
        Assert.Equal(before! with { State = "Accepted", TokenHash = after.TokenHash }, after);
        Assert.Equal(before.TokenHash, after.TokenHash);
        Assert.Equal(membershipsBefore, membershipsAfterViews);

        // The row as the three views left it, before the accept overwrites State (code review of story 4.3): every column,
        // State included, unchanged by a read.
        Assert.Equal(before with { TokenHash = afterViews!.TokenHash }, afterViews);
        Assert.Equal(before.TokenHash, afterViews.TokenHash);
        Assert.Equal(recordsBefore, recordsAfterViews);
        Assert.True(accepted?.StatusCode == HttpStatusCode.OK, $"{accepted?.StatusCode} {accepted?.Body}");
        Assert.Equal(membershipsBefore + 1, (await MembershipsInAsync(database, invited.SpaceId)).Count);
        Assert.Equal(outboxBefore, (await wire.OutboxRowsAsync()).Count);
        Assert.Empty(await RefusalsOfAsync(database, invited.OwnerId));
    }

    /// <summary>
    /// AC1, AC7, Lee's Q5 (a) and Q7 (a) - the view is not a credential check: no Session, the issuer's own Session, the
    /// addressee's Session and the issuer's bearer Token are all served the same Space, Role and issuer. Only the addressee
    /// indicator differs, and it reads a Session only: <c>null</c> with none and with a Token, <c>false</c> for a Session
    /// whose Account is not the invited one, <c>true</c> for the addressee's.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-11")]
    [Trait("Requirement", "UX-DR18")]
    [Trait("Requirement", "AD-4")]
    public async Task The_view_answers_the_same_offer_to_no_Session_any_Session_and_a_bearer_Token()
    {
        var database = Available();
        var wire = Wire();
        Invited? invited = null;
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            invited = await InviteAsync(client, database, wire, mail, "credentials", "viewer");

            var invitee = await RegisteredAndSignedInAsync(client, invited.Invitee, DisplayName());
            var bearer = await BearerForAsync(invited.Owner, invited.SpaceId);

            answers["none"] = await ViewAnonymouslyAsync(client, invited.Token);
            answers["issuer"] = await ViewAsync(invited.Owner, invited.Token);
            answers["addressee"] = await ViewAsync(invitee, invited.Token);
            answers["token"] = await invited.Owner.SendWithTokenAndHeadersAsync(HttpMethod.Get, InvitationRoutes.Offer, bearer, content: null, InvitationWire.TokenHeaders(invited.Token));
        });

        Assert.All(answers.Values, answer => Assert.True(answer.StatusCode == HttpStatusCode.OK, $"{answer.StatusCode} {answer.Body}"));

        var offers = answers.ToDictionary(pair => pair.Key, pair => InvitationWire.ParseOffer(pair.Value.Body), StringComparer.Ordinal);

        Assert.Single(offers.Values.Select(offer => (offer.SpaceName, offer.Role, offer.IssuerName)).Distinct());
        Assert.Equal((invited!.SpaceName, "viewer", invited.IssuerName), (offers["none"].SpaceName, offers["none"].Role, offers["none"].IssuerName));
        Assert.Null(offers["none"].AddressedToYou);
        Assert.Null(offers["token"].AddressedToYou);
        Assert.False(offers["issuer"].AddressedToYou);
        Assert.True(offers["addressee"].AddressedToYou);
    }

    /// <summary>
    /// AC7, UX-DR18, <c>EXPERIENCE.md:265</c>, Lee's Q5 (a) - a different Account signed in when the link is opened is told
    /// the Invitation is not for it, and neither address is echoed: the view's body holds no <c>@</c>, neither the invited
    /// address nor the signed-in one, and no fragment of either (their local parts). Anchor: the addressee's view says it is
    /// theirs.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-11")]
    [Trait("Requirement", "UX-DR18")]
    public async Task A_different_Account_viewing_is_told_it_is_not_theirs_without_either_address()
    {
        var database = Available();
        var wire = Wire();
        var strangerAddress = RandomAddress();
        Invited? invited = null;
        WireResponse? stranger = null;
        WireResponse? addressee = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            invited = await InviteAsync(client, database, wire, mail, "mismatch-view", "member");

            var other = await SignedInAsync(client, strangerAddress);
            var invitee = await RegisteredAndSignedInAsync(client, invited.Invitee, DisplayName());

            stranger = await ViewAsync(other, invited.Token);
            addressee = await ViewAsync(invitee, invited.Token);
        });

        Assert.True(addressee?.StatusCode == HttpStatusCode.OK, $"{addressee?.StatusCode} {addressee?.Body}");
        Assert.True(InvitationWire.ParseOffer(addressee!.Body).AddressedToYou);
        Assert.True(stranger?.StatusCode == HttpStatusCode.OK, $"{stranger?.StatusCode} {stranger?.Body}");
        Assert.False(InvitationWire.ParseOffer(stranger!.Body).AddressedToYou);

        foreach (var address in new[] { invited!.Invitee, strangerAddress })
        {
            Assert.DoesNotContain(address, stranger.Body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(address.Split('@')[0], stranger.Body, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("@", stranger.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// The view as the browser sends it with a Session: the cookie and the token header; a <c>GET</c>, so no anti-forgery
    /// header.
    /// </summary>
    private static Task<WireResponse> ViewAsync(WireSession session, string token) =>
        session.SendWithHeadersAsync(HttpMethod.Get, InvitationRoutes.Offer, content: null, InvitationWire.TokenHeaders(token), withAntiforgeryToken: false);

    /// <summary>
    /// The view as a link holder with no Session sends it - a mail scanner, a prefetcher, a forwarded link: a client of its
    /// own, holding no cookie.
    /// </summary>
    private static Task<WireResponse> ViewAnonymouslyAsync(HttpClient client, string token) =>
        new WireSession(client).SendWithHeadersAsync(HttpMethod.Get, InvitationRoutes.Offer, content: null, InvitationWire.TokenHeaders(token), withAntiforgeryToken: false);

    /// <summary>
    /// The accept as the browser sends it: the Session's cookie and anti-forgery header, the body
    /// <c>{ invitationToken }</c>, and an <c>Idempotency-Key</c> when one is given (Lee's Q4 (a); Q11 (a)).
    /// </summary>
    private static Task<WireResponse> AcceptAsync(WireSession session, string token, string? key = null) =>
        session.SendWithHeadersAsync(HttpMethod.Post, InvitationRoutes.Acceptance, InvitationWire.AcceptBody(token), KeyHeaders(key));

    /// <summary>
    /// A bearer Token the session's Account issues for <paramref name="spaceId"/> (story 1.8's route), as its plaintext.
    /// </summary>
    private static async Task<string> BearerForAsync(WireSession session, Guid spaceId)
    {
        var issued = await IssueAsync(session, spaceId, "invitation-" + Suffix());

        Assert.True(issued.StatusCode == HttpStatusCode.Created, $"{issued.StatusCode} {issued.Body}");

        using var body = JsonDocument.Parse(issued.Body);

        return body.RootElement.GetProperty("token").GetString() ?? throw new InvalidOperationException("The Token issue answered no plaintext.");
    }

    /// <summary>
    /// The issuer registered with a display name of its own and signed in, the Owner of a freshly seeded, named Space, who
    /// issues one Invitation to a fresh address (or <paramref name="invitee"/>) at <paramref name="role"/> through 4.1's
    /// route; the token is read from the email. The Session's anti-forgery token is fetched after sign-in.
    /// </summary>
    private static async Task<Invited> InviteAsync(
        HttpClient client,
        RegistrationDatabase database,
        WireDatabase wire,
        MailDrop mail,
        string label,
        string role,
        string? invitee = null)
    {
        var ownerAddress = RandomAddress();
        var issuerName = DisplayName();
        var owner = await RegisteredAndSignedInAsync(client, ownerAddress, issuerName);
        var ownerId = await AccountIdOfAsync(database, ownerAddress);
        var spaceId = Guid.NewGuid();
        var spaceName = $"Offered {label} {Suffix()}";

        await SeedNamedSpaceAsync(database, spaceId, spaceName, ownerId, Role.Owner);

        return await InviteIntoAsync(new Issuer(owner, ownerId, issuerName, spaceId, spaceName), wire, mail, invitee ?? InvitationWire.FreshAddress(label), role);
    }

    /// <summary>
    /// One more Invitation in an existing Space, issued by its Owner through 4.1's route.
    /// </summary>
    private static async Task<Invited> InviteIntoAsync(Issuer from, WireDatabase wire, MailDrop mail, string address, string role)
    {
        await from.Owner.FetchAntiforgeryTokenAsync();

        var issued = await IssueAsync(from.Owner, from.SpaceId, address, role);

        Assert.True(issued.StatusCode == HttpStatusCode.NoContent, $"Issuing through the product: {issued.StatusCode} {issued.Body}");

        var token = InvitationWire.TokenIn(await mail.WaitForEmailToAsync(address));
        var invitationId = Assert.Single(await wire.InvitationsInAsync(from.SpaceId), row => row.EmailAddress == address).Id;

        return new Invited(from, address, token, invitationId);
    }

    /// <summary>
    /// The idempotency records written under <paramref name="spaceId"/>'s key - the view must add none (AC1).
    /// </summary>
    private static async Task<int> IdempotencyRecordCountAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT COUNT(*) FROM dbo.[IdempotencyRecord]";

        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The Owner of a Space who issues its Invitations.
    /// </summary>
    /// <param name="Owner">The issuer's Session.</param>
    /// <param name="OwnerId">The issuer's Account.</param>
    /// <param name="IssuerName">The issuer's display name, as captured at issue.</param>
    /// <param name="SpaceId">The Space.</param>
    /// <param name="SpaceName">Its name.</param>
    private sealed record Issuer(WireSession Owner, Guid OwnerId, string IssuerName, Guid SpaceId, string SpaceName);

    /// <summary>
    /// One Invitation issued through the product, as a case knows it.
    /// </summary>
    /// <param name="From">Who issued it, and into which Space.</param>
    /// <param name="Invitee">The invited address, as issued.</param>
    /// <param name="Token">The token, from the email.</param>
    /// <param name="InvitationId">The row's id.</param>
    private sealed record Invited(Issuer From, string Invitee, string Token, Guid InvitationId)
    {
        /// <summary>
        /// The issuer's Session (the Space's Owner).
        /// </summary>
        public WireSession Owner => From.Owner;

        /// <summary>
        /// The issuer's Account.
        /// </summary>
        public Guid OwnerId => From.OwnerId;

        /// <summary>
        /// The issuer's display name, as captured at issue.
        /// </summary>
        public string IssuerName => From.IssuerName;

        /// <summary>
        /// The invited Space.
        /// </summary>
        public Guid SpaceId => From.SpaceId;

        /// <summary>
        /// The invited Space's name.
        /// </summary>
        public string SpaceName => From.SpaceName;

        /// <summary>
        /// What no answer to a link holder may contain (UX-DR18, AD-23): the address in every form, and every id.
        /// </summary>
        public IEnumerable<string> Undisclosed() =>
        [
            Invitee,
            EmailAddressNormalisation.Normalise(Invitee),
            Invitee.Split('@')[0],
            SpaceId.ToString("D"),
            SpaceId.ToString("N"),
            InvitationId.ToString("D"),
            OwnerId.ToString("D"),
        ];
    }
}
