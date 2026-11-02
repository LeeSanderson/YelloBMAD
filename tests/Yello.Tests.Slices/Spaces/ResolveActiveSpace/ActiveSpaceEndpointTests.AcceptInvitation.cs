using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Accounts;
using Yello.Contracts.Invitations;
using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 4.3 over the wire: accepting an Invitation, <c>POST /api/v1/invitations/acceptance</c> with
/// <c>{ invitationToken }</c> (Lee's Q4 (a)) on a Session only (Lee's Q7 (a)) - one guarded statement under the Space's
/// Invitations lock, authorised by the caller's own normalised address, never by the token (AC4, AD-28); exactly one
/// Membership at the Invitation's Role (FR-11 <c>:276</c>); refused with one uniform answer for five states on both routes
/// (AC5, AC6, Lee's Q6 (a)); a mismatch stated without an address (AC7, Q13 (a)); registration from the Invitation through
/// the one registration slice (AC3, Lee's Q3 (a)); idempotent under retry (AC11, Q11 (a)). FR-11, AR-27/AD-22, AR-28/AD-23,
/// AR-31/AD-27, AR-32/AD-28, NFR-6, I-17.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 4.3 ATDD run (2026-11-01); activated by its dev phase (2026-11-02).</b> See <c>.ViewInvitation.cs</c> for the
/// stand-ins, the seeding and the 4.2 trap: every refusal here is anchored on a served accept of the same route, and the
/// uniform <c>404</c> is asserted to differ in type and body from the unmatched path's <c>not-available</c>.
/// </para>
/// <para>
/// <b>The expired arm</b> is the one row written directly: an Invitation issued through the product, its <c>ExpiresAt</c>
/// then moved into the past by <see cref="WireDatabase.SetInvitationExpiresAtAsync"/> (R12; <c>Assumption = PRD-12-9</c>).
/// The revoked arm goes through 4.2's route, the accepted arm through this story's, the Space-deleted arm through 3.3's
/// deletion (whose one statement must still cascade to the token index, Lee's Q1 (a)).
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// AC4, FR-11 <c>:276</c>, <c>:280</c>, AD-28, Q10 (a) - the invited Account, signed in, accepts: <c>200</c> with exactly
    /// <c>{ spaceId }</c> naming the invited Space; the Invitation is <c>Accepted</c>; the Account now holds exactly two
    /// Memberships - its Personal Space as Owner and the invited Space at exactly the invited Role - and still one Account.
    /// Anchor: the view showed the offer before the accept (a deliberate act after the Space and Role were shown).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-11")]
    [Trait("Requirement", "AR-32")]
    [Trait("Requirement", "AD-28")]
    public async Task The_invited_Account_accepts_and_joins_exactly_the_invited_Space_at_exactly_the_invited_Role()
    {
        var database = Available();
        var wire = Wire();
        var displayName = DisplayName();
        Invited? invited = null;
        WireResponse? shown = null;
        WireResponse? accepted = null;
        var inviteeId = Guid.Empty;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            invited = await InviteAsync(client, database, wire, mail, "joins", "viewer");

            var invitee = await RegisteredAndSignedInAsync(client, invited.Invitee, displayName);

            inviteeId = await AccountIdOfAsync(database, invited.Invitee);
            shown = await ViewAsync(invitee, invited.Token);

            await invitee.FetchAntiforgeryTokenAsync();
            accepted = await AcceptAsync(invitee, invited.Token, Guid.NewGuid().ToString("D"));
        });

        Assert.True(shown?.StatusCode == HttpStatusCode.OK, $"{shown?.StatusCode} {shown?.Body}");
        Assert.True(accepted?.StatusCode == HttpStatusCode.OK, $"{accepted?.StatusCode} {accepted?.Body}");
        Assert.Equal(InvitationWire.AcceptedMembers, InvitationWire.MemberNames(accepted!.Body));
        Assert.Equal(invited!.SpaceId, InvitationWire.AcceptedSpaceId(accepted.Body));
        Assert.Equal("Accepted", Assert.Single(await wire.InvitationsInAsync(invited.SpaceId)).State);
        Assert.Equal(1, await wire.AccountCountAsync(invited.Invitee));

        var personal = await PersonalSpaceOfAsync(database, displayName);

        Assert.Equal(
            [(invited.SpaceId, "Viewer"), (personal, "Owner")],
            (await wire.MembershipsOfAccountAsync(inviteeId)).OrderBy(row => row.SpaceId == personal));
    }

    /// <summary>
    /// AC5, FR-11 <c>:281</c>, AD-28, C5 - a second acceptance with the same Invitation (a new <c>Idempotency-Key</c>) is
    /// refused by the guarded <c>WHERE State = Pending</c>: the uniform <c>404 invitation-not-valid</c>, never AD-26's
    /// <c>409</c>; still exactly one Membership in the Space for the Account. Plant 2 or 3 (the guard dropped, or
    /// load-check-save): a second Membership, or a 2601/2627 answered as <c>500</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-11")]
    [Trait("Requirement", "AR-32")]
    [Trait("Requirement", "AD-26")]
    public async Task A_second_acceptance_is_refused_with_the_uniform_answer_and_no_second_Membership()
    {
        var database = Available();
        var wire = Wire();
        Invited? invited = null;
        WireResponse? first = null;
        WireResponse? second = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            invited = await InviteAsync(client, database, wire, mail, "twice", "member");

            var invitee = await RegisteredAndSignedInAsync(client, invited.Invitee, DisplayName());

            await invitee.FetchAntiforgeryTokenAsync();
            first = await AcceptAsync(invitee, invited.Token, Guid.NewGuid().ToString("D"));
            second = await AcceptAsync(invitee, invited.Token, Guid.NewGuid().ToString("D"));
        });

        Assert.True(first?.StatusCode == HttpStatusCode.OK, $"{first?.StatusCode} {first?.Body}");
        Assert.True(second?.StatusCode == HttpStatusCode.NotFound, $"{second?.StatusCode} {second?.Body}");
        Assert.Equal(ProblemTypes.InvitationNotValid, ProblemType(second!));
        Assert.Single(await MembershipsInAsync(database, invited!.SpaceId), row => row.AccountId != invited.OwnerId);
    }

    /// <summary>
    /// AC11, AD-18, AR-25, Q11 (a) - a malformed key is <c>400 invalid-idempotency-key</c> and accepts nothing; then a replay
    /// with the same key after a committed accept answers the original <c>200</c> and body from the record (a lost response
    /// is not mistaken for a revoked Invitation); a new key on the accepted Invitation answers the uniform <c>404</c>; one
    /// Membership, the row <c>Accepted</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-18")]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "FR-11")]
    public async Task An_accept_replays_its_200_under_the_same_key_and_a_new_key_meets_the_uniform_answer()
    {
        var database = Available();
        var wire = Wire();
        var key = Guid.NewGuid().ToString("D");
        Invited? invited = null;
        WireResponse? malformed = null;
        var pendingAfterMalformed = string.Empty;
        WireResponse? original = null;
        WireResponse? replay = null;
        WireResponse? fresh = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            invited = await InviteAsync(client, database, wire, mail, "replayed", "member");

            var invitee = await RegisteredAndSignedInAsync(client, invited.Invitee, DisplayName());

            await invitee.FetchAntiforgeryTokenAsync();
            malformed = await AcceptAsync(invitee, invited.Token, new string('k', Idempotency.MaxKeyLength + 1));
            pendingAfterMalformed = Assert.Single(await wire.InvitationsInAsync(invited.SpaceId)).State;

            original = await AcceptAsync(invitee, invited.Token, key);
            replay = await AcceptAsync(invitee, invited.Token, key);
            fresh = await AcceptAsync(invitee, invited.Token, Guid.NewGuid().ToString("D"));
        });

        Assert.Equal(HttpStatusCode.BadRequest, malformed?.StatusCode);
        Assert.Equal(ProblemTypes.InvalidIdempotencyKey, ProblemType(malformed!));
        Assert.Equal("Pending", pendingAfterMalformed);
        Assert.True(original?.StatusCode == HttpStatusCode.OK, $"{original?.StatusCode} {original?.Body}");
        Assert.Equal(original!.StatusCode, replay?.StatusCode);
        Assert.Equal(original.Body, replay!.Body);
        Assert.True(fresh?.StatusCode == HttpStatusCode.NotFound, $"{fresh?.StatusCode} {fresh?.Body}");
        Assert.Equal(ProblemTypes.InvitationNotValid, ProblemType(fresh!));
        Assert.Equal("Accepted", Assert.Single(await wire.InvitationsInAsync(invited!.SpaceId)).State);
        Assert.Single(await MembershipsInAsync(database, invited.SpaceId), row => row.AccountId != invited.OwnerId);
    }

    /// <summary>
    /// AC6, FR-11 <c>:282</c>, AD-28, UX-DR18, I-13, Lee's Q6 (a), ledger <c>:736</c> and <c>:722</c> - five states, one
    /// response, on both routes: a revoked (4.2's route), an accepted (this story's), an expired (<c>ExpiresAt</c> past,
    /// still stored <c>Pending</c>), a Space-deleted (the cascade took it) and an unrecognised token - well-formed but never
    /// issued, and each malformed shape - are each answered the same <c>404</c> with the same body, content type and header
    /// names, on the view (no Session) and on the accept (a signed-in Account the Invitation does not name, and the revoked
    /// Invitation's own addressee: AC6 is decided before AC7's mismatch, Q5 (a)). The type is <c>invitation-not-valid</c>,
    /// never the boundary's <c>not-available</c>, and the body differs from the unmatched path's; nothing names a Space or an
    /// address, and nothing is recorded as a refusal. Anchor: a live Invitation's view is the <c>200</c> offer. Plant 7: an
    /// unrecognised token answered with a distinct body.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-11")]
    [Trait("Requirement", "AR-32")]
    [Trait("Requirement", "UX-DR18")]
    [Trait("Requirement", "AD-27")]
    [Trait("Assumption", "PRD-12-9")]
    public async Task Five_states_are_one_answer_on_the_view_and_on_the_accept()
    {
        var database = Available();
        var wire = Wire();
        var proberAddress = RandomAddress();
        var proberId = Guid.Empty;
        Invited? live = null;
        var spaceNames = new List<string>();
        var addresses = new List<string>();
        var views = new List<(string State, WireResponse Answer)>();
        var accepts = new List<(string State, WireResponse Answer)>();
        WireResponse? offer = null;
        WireResponse? unmatched = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            live = await InviteAsync(client, database, wire, mail, "live", "member");

            var revoked = await InviteIntoAsync(live.From, wire, mail, InvitationWire.FreshAddress("revoked"), "member");
            var accepted = await InviteIntoAsync(live.From, wire, mail, InvitationWire.FreshAddress("accepted"), "member");
            var expired = await InviteIntoAsync(live.From, wire, mail, InvitationWire.FreshAddress("expired"), "member");

            var doomedSpace = Guid.NewGuid();
            var doomedName = "Doomed " + Suffix();

            await SeedNamedSpaceAsync(database, doomedSpace, doomedName, live.OwnerId, Role.Owner);

            var deleted = await InviteIntoAsync(live.From with { SpaceId = doomedSpace, SpaceName = doomedName }, wire, mail, InvitationWire.FreshAddress("deleted"), "member");

            spaceNames.AddRange([live.SpaceName, doomedName]);
            addresses.AddRange([live.Invitee, revoked.Invitee, accepted.Invitee, expired.Invitee, deleted.Invitee]);

            // Revoked through 4.2's route; accepted through this story's; expired by moving ExpiresAt (R12); deleted with
            // its Space through 3.3's one statement.
            await live.Owner.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await live.Owner.SendWithHeadersAsync(HttpMethod.Delete, InvitationRoutes.InvitationOf(live.SpaceId, revoked.InvitationId), content: null, NoKey)).StatusCode);

            var acceptor = await RegisteredAndSignedInAsync(client, accepted.Invitee, DisplayName());

            await acceptor.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(acceptor, accepted.Token)).StatusCode);

            await wire.SetInvitationExpiresAtAsync(live.SpaceId, expired.InvitationId, (await wire.DatabaseNowAsync()).AddMinutes(-1));

            await live.Owner.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await DeleteSpaceOverWireAsync(live.Owner, doomedSpace, key: null)).StatusCode);

            var prober = await SignedInAsync(client, proberAddress);
            var revokedAddressee = await RegisteredAndSignedInAsync(client, revoked.Invitee, DisplayName());

            proberId = await AccountIdOfAsync(database, proberAddress);
            await prober.FetchAntiforgeryTokenAsync();
            await revokedAddressee.FetchAntiforgeryTokenAsync();

            var states = new List<(string State, string Token)>
            {
                ("revoked", revoked.Token),
                ("accepted", accepted.Token),
                ("expired", expired.Token),
                ("space-deleted", deleted.Token),
                ("unrecognised", InvitationWire.UnrecognisedToken()),
            };

            states.AddRange(InvitationWire.MalformedTokens().Select((token, index) => ($"malformed-{index}", token)));

            foreach (var (state, token) in states)
            {
                views.Add((state, await ViewAnonymouslyAsync(client, token)));
                accepts.Add((state, await AcceptAsync(prober, token)));
            }

            accepts.Add(("revoked, by its addressee", await AcceptAsync(revokedAddressee, revoked.Token)));

            offer = await ViewAnonymouslyAsync(client, live.Token);
            unmatched = await new WireSession(client).GetAnonymouslyAsync(InvitationRoutes.Offer + "/nothing-here");
        });

        Assert.True(offer?.StatusCode == HttpStatusCode.OK, $"The live anchor: {offer?.StatusCode} {offer?.Body}");
        Assert.Equal(HttpStatusCode.NotFound, unmatched?.StatusCode);

        foreach (var route in new[] { views, accepts })
        {
            var (_, first) = route[0];

            Assert.True(first.StatusCode == HttpStatusCode.NotFound, $"{first.StatusCode} {first.Body}");
            Assert.Equal(ProblemTypes.InvitationNotValid, ProblemType(first));
            Assert.NotEqual(ProblemTypes.NotAvailable, ProblemType(first));
            Assert.NotEqual(unmatched!.Body, first.Body);

            foreach (var (state, answer) in route)
            {
                Assert.True(answer.StatusCode == first.StatusCode, $"{state}: {answer.StatusCode} {answer.Body}");
                Assert.True(answer.Body == first.Body, $"{state}: {answer.Body}");
                Assert.Equal(first.ContentType, answer.ContentType);
                Assert.Equal(first.HeaderNames, answer.HeaderNames);
            }

            foreach (var undisclosed in spaceNames.Concat(addresses))
            {
                Assert.DoesNotContain(undisclosed, first.Body, StringComparison.OrdinalIgnoreCase);
            }
        }

        Assert.Equal(views[0].Answer.Body, accepts[0].Answer.Body);
        Assert.Empty(await RefusalsOfAsync(database, proberId));
        Assert.Equal("Revoked", Assert.Single(await wire.InvitationsInAsync(live!.SpaceId), row => row.EmailAddress == addresses[1]).State);
        Assert.Equal("Pending", Assert.Single(await wire.InvitationsInAsync(live.SpaceId), row => row.EmailAddress == addresses[3]).State);
    }

    /// <summary>
    /// AC7, AD-28, UX-DR18, Q13 (a) - a signed-in Account the Invitation does not name accepting it is refused <c>403
    /// invitation-for-another-account</c>: no <c>@</c>, neither address, nothing written (the Invitation <c>Pending</c>, no
    /// Membership for the caller) and nothing recorded as an <c>AccessRefusal</c> (neither of AD-20's kinds). Anchor: the
    /// addressee then accepts the same token <c>200</c> - the token was never the authority. Plant 4: the accept without the
    /// address clause.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-11")]
    [Trait("Requirement", "AR-32")]
    [Trait("Requirement", "AD-28")]
    [Trait("Requirement", "UX-DR18")]
    public async Task A_different_Account_accepting_is_refused_403_without_an_address_and_nothing_is_written()
    {
        var database = Available();
        var wire = Wire();
        var strangerAddress = RandomAddress();
        var strangerId = Guid.Empty;
        Invited? invited = null;
        WireResponse? refused = null;
        string? stateAfterRefusal = null;
        WireResponse? accepted = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            invited = await InviteAsync(client, database, wire, mail, "not-yours", "admin");

            var stranger = await SignedInAsync(client, strangerAddress);

            strangerId = await AccountIdOfAsync(database, strangerAddress);
            await stranger.FetchAntiforgeryTokenAsync();
            refused = await AcceptAsync(stranger, invited.Token);
            stateAfterRefusal = Assert.Single(await wire.InvitationsInAsync(invited.SpaceId)).State;

            var invitee = await RegisteredAndSignedInAsync(client, invited.Invitee, DisplayName());

            await invitee.FetchAntiforgeryTokenAsync();
            accepted = await AcceptAsync(invitee, invited.Token);
        });

        Assert.True(refused?.StatusCode == HttpStatusCode.Forbidden, $"{refused?.StatusCode} {refused?.Body}");
        Assert.Equal(ProblemTypes.InvitationForAnotherAccount, ProblemType(refused!));
        Assert.DoesNotContain("@", refused!.Body, StringComparison.Ordinal);

        foreach (var address in new[] { invited!.Invitee, strangerAddress })
        {
            Assert.DoesNotContain(address.Split('@')[0], refused.Body, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal("Pending", stateAfterRefusal);
        Assert.DoesNotContain(await wire.MembershipsOfAccountAsync(strangerId), row => row.SpaceId == invited.SpaceId);
        Assert.Empty(await RefusalsOfAsync(database, strangerId));
        Assert.True(accepted?.StatusCode == HttpStatusCode.OK, $"The addressee's anchor: {accepted?.StatusCode} {accepted?.Body}");
    }

    /// <summary>
    /// AC3, FR-11 <c>:278-279</c>, AD-22, AD-23, Lee's Q3 (a) - an invitee with no Account registers through the unchanged
    /// <c>POST /api/v1/accounts</c>, signs in, and sends the accept: one deliberate act, three requests. Afterwards there is
    /// exactly one Account for the address, holding exactly two Memberships - Owner of its own Personal Space (named from the
    /// display name, provisioned by registration) and the invited Role in the invited Space - and the Invitation is
    /// <c>Accepted</c>. Plant 17 is the gate's (the acceptance slice referencing <c>NewSpace</c>), not this case's.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-11")]
    [Trait("Requirement", "FR-4")]
    [Trait("Requirement", "AR-27")]
    [Trait("Requirement", "AD-22")]
    public async Task Registering_from_the_Invitation_then_signing_in_and_accepting_joins_through_the_one_registration_slice()
    {
        var database = Available();
        var wire = Wire();
        var displayName = DisplayName();
        Invited? invited = null;
        WireResponse? offer = null;
        WireResponse? registered = null;
        WireResponse? signedIn = null;
        WireResponse? accepted = null;
        var inviteeId = Guid.Empty;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            invited = await InviteAsync(client, database, wire, mail, "registers", "member");

            var newcomer = new WireSession(client);

            offer = await ViewAsync(newcomer, invited.Token);

            await newcomer.FetchAntiforgeryTokenAsync();
            registered = await newcomer.RegisterAsync(new RegisterAccountRequest(displayName, invited.Invitee, Password));
            signedIn = await newcomer.SignInAsync(invited.Invitee, Password);

            // A token minted while anonymous is refused once a Session exists (TESTING-CONVENTIONS.md:450-456).
            await newcomer.FetchAntiforgeryTokenAsync();
            accepted = await AcceptAsync(newcomer, invited.Token, Guid.NewGuid().ToString("D"));
            inviteeId = await AccountIdOfAsync(database, invited.Invitee);
        });

        Assert.True(offer?.StatusCode == HttpStatusCode.OK, $"{offer?.StatusCode} {offer?.Body}");
        Assert.Equal(HttpStatusCode.NoContent, registered?.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, signedIn?.StatusCode);
        Assert.True(accepted?.StatusCode == HttpStatusCode.OK, $"{accepted?.StatusCode} {accepted?.Body}");
        Assert.Equal(1, await wire.AccountCountAsync(invited!.Invitee));

        var personal = await PersonalSpaceOfAsync(database, displayName);

        Assert.NotEqual(invited.SpaceId, personal);
        Assert.Equal(
            [(invited.SpaceId, "Member"), (personal, "Owner")],
            (await wire.MembershipsOfAccountAsync(inviteeId)).OrderBy(row => row.SpaceId == personal));
        Assert.Equal("Accepted", Assert.Single(await wire.InvitationsInAsync(invited.SpaceId)).State);
    }

    /// <summary>
    /// AC4, AD-7, Lee's Q7 (a), C6 - the accept needs a Session: with no credential it is <c>401</c>, and with the invitee's
    /// own bearer Token (bound to its Personal Space) it is the same <c>401</c>, byte for byte - a Space-bound Token never
    /// joins its Account to another Space. Nothing is written: the Invitation stays <c>Pending</c> and the invitee holds only
    /// its Personal Space. Anchor: the invitee's Session then accepts <c>200</c>. Plant 11: a Token accepted on the route.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-11")]
    [Trait("Requirement", "AD-4")]
    [Trait("Requirement", "AD-28")]
    public async Task An_accept_with_no_Session_or_with_a_bearer_Token_is_the_same_401_and_writes_nothing()
    {
        var database = Available();
        var wire = Wire();
        var displayName = DisplayName();
        Invited? invited = null;
        WireResponse? anonymous = null;
        WireResponse? withToken = null;
        var membershipsBeforeSession = 0;
        string? stateBeforeSession = null;
        WireResponse? withSession = null;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            invited = await InviteAsync(client, database, wire, mail, "credential", "member");

            var invitee = await RegisteredAndSignedInAsync(client, invited.Invitee, displayName);
            var inviteeId = await AccountIdOfAsync(database, invited.Invitee);
            var bearer = await BearerForAsync(invitee, await PersonalSpaceOfAsync(database, displayName));

            anonymous = await new WireSession(client).PostAsync(InvitationRoutes.Acceptance, InvitationWire.AcceptBody(invited.Token), withAntiforgeryToken: false);
            withToken = await invitee.SendWithTokenAndHeadersAsync(HttpMethod.Post, InvitationRoutes.Acceptance, bearer, InvitationWire.AcceptBody(invited.Token), NoKey);

            membershipsBeforeSession = (await wire.MembershipsOfAccountAsync(inviteeId)).Count;
            stateBeforeSession = Assert.Single(await wire.InvitationsInAsync(invited.SpaceId)).State;

            await invitee.FetchAntiforgeryTokenAsync();
            withSession = await AcceptAsync(invitee, invited.Token);
        });

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous?.StatusCode);
        Assert.Equal(anonymous!.StatusCode, withToken?.StatusCode);
        Assert.Equal(anonymous.Body, withToken!.Body);
        Assert.Equal(anonymous.HeaderNames, withToken.HeaderNames);
        Assert.Equal(1, membershipsBeforeSession);
        Assert.Equal("Pending", stateBeforeSession);
        Assert.True(withSession?.StatusCode == HttpStatusCode.OK, $"The Session's anchor: {withSession?.StatusCode} {withSession?.Body}");
    }

    /// <summary>
    /// AC4 ("loading a URL is never sufficient"), AD-28 - a <c>GET</c> on the accept route is <c>405</c> or the unmatched
    /// <c>404</c> and creates nothing: its Invitation stays <c>Pending</c> and its addressee joins nothing. Anchored on a
    /// served <c>POST</c> on the same route first, so the absence is not a route that does not exist (the 4.2 trap).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-11")]
    [Trait("Requirement", "AD-28")]
    public async Task A_GET_on_the_accept_route_creates_nothing()
    {
        var database = Available();
        var wire = Wire();
        Invited? anchor = null;
        Invited? fetched = null;
        WireResponse? posted = null;
        WireResponse? got = null;
        var fetchedId = Guid.Empty;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            anchor = await InviteAsync(client, database, wire, mail, "posted", "member");
            fetched = await InviteIntoAsync(anchor.From, wire, mail, InvitationWire.FreshAddress("fetched"), "member");

            var poster = await RegisteredAndSignedInAsync(client, anchor.Invitee, DisplayName());
            var fetcher = await RegisteredAndSignedInAsync(client, fetched.Invitee, DisplayName());

            fetchedId = await AccountIdOfAsync(database, fetched.Invitee);
            await poster.FetchAntiforgeryTokenAsync();
            posted = await AcceptAsync(poster, anchor.Token);
            got = await fetcher.SendWithHeadersAsync(HttpMethod.Get, InvitationRoutes.Acceptance, content: null, InvitationWire.TokenHeaders(fetched.Token), withAntiforgeryToken: false);
        });

        Assert.True(posted?.StatusCode == HttpStatusCode.OK, $"The POST anchor: {posted?.StatusCode} {posted?.Body}");
        Assert.True(got?.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound, $"{got?.StatusCode} {got?.Body}");
        Assert.Equal("Pending", Assert.Single(await wire.InvitationsInAsync(anchor!.SpaceId), row => row.Id == fetched!.InvitationId).State);
        Assert.DoesNotContain(await wire.MembershipsOfAccountAsync(fetchedId), row => row.SpaceId == anchor.SpaceId);
    }

    /// <summary>
    /// Q12 (a) - an Account already a Member of the Space when it accepts (unreachable through the product: issue refuses a
    /// Member, so the Membership is added after the issue) is refused <c>409 already-a-member</c>, 4.1's type; the
    /// Invitation stays <c>Pending</c> and the Membership keeps its Role. Nothing is written.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-11")]
    public async Task An_Account_already_a_Member_accepting_is_refused_409_already_a_member_and_nothing_changes()
    {
        var database = Available();
        var wire = Wire();
        Invited? invited = null;
        WireResponse? refused = null;
        var inviteeId = Guid.Empty;

        using var mail = new MailDrop();

        await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            invited = await InviteAsync(client, database, wire, mail, "member-already", "admin");

            var invitee = await RegisteredAndSignedInAsync(client, invited.Invitee, DisplayName());

            inviteeId = await AccountIdOfAsync(database, invited.Invitee);
            await wire.AddMembershipAsync(invited.SpaceId, inviteeId, "viewer");

            await invitee.FetchAntiforgeryTokenAsync();
            refused = await AcceptAsync(invitee, invited.Token);
        });

        Assert.True(refused?.StatusCode == HttpStatusCode.Conflict, $"{refused?.StatusCode} {refused?.Body}");
        Assert.Equal(ProblemTypes.AlreadyAMember, ProblemType(refused!));
        Assert.Equal("Pending", Assert.Single(await wire.InvitationsInAsync(invited!.SpaceId)).State);
        Assert.Equal((inviteeId, "Viewer"), Assert.Single(await MembershipsInAsync(database, invited.SpaceId), row => row.AccountId == inviteeId));
    }

    /// <summary>
    /// NFR-6, I-17, AR-34 - neither the token, nor the invited address (as issued or normalised), nor a signed-in stranger's
    /// address, nor the Space's or the issuer's name reaches the Host's log across a view, a mismatched accept, an accept and
    /// a second (uniform) accept. Anchor: the log is the Host's (its listening line is in it) and each request was served as
    /// asserted. Plant 16: the token logged.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-6")]
    [Trait("Requirement", "AD-23")]
    public async Task Neither_the_token_nor_an_address_nor_a_name_reaches_the_Host_log_on_view_or_accept()
    {
        var database = Available();
        var wire = Wire();
        var strangerAddress = RandomAddress();
        Invited? invited = null;

        using var mail = new MailDrop();

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, mail.With(Configured), async client =>
        {
            invited = await InviteAsync(client, database, wire, mail, "unlogged-accept", "member");

            var stranger = await SignedInAsync(client, strangerAddress);
            var invitee = await RegisteredAndSignedInAsync(client, invited.Invitee, DisplayName());

            Assert.Equal(HttpStatusCode.OK, (await ViewAnonymouslyAsync(client, invited.Token)).StatusCode);

            await stranger.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.Forbidden, (await AcceptAsync(stranger, invited.Token)).StatusCode);

            await invitee.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(invitee, invited.Token)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await AcceptAsync(invitee, invited.Token)).StatusCode);
        });

        Assert.Contains(HostProcess.ListeningMarker, log, StringComparison.Ordinal);

        foreach (var secret in new[]
        {
            invited!.Token,
            invited.Invitee,
            EmailAddressNormalisation.Normalise(invited.Invitee),
            strangerAddress,
            invited.SpaceName,
            invited.IssuerName,
        })
        {
            Assert.DoesNotContain(secret, log, StringComparison.OrdinalIgnoreCase);
        }
    }
}
