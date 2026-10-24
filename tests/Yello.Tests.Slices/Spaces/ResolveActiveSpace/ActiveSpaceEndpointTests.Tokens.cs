using System.Buffers.Text;
using System.Data;
using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Accounts;
using Yello.Contracts.ApiTokens;
using Yello.Contracts.Spaces;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 1.8 over the wire: issuing an API Token bound to one Space, using it, and every way it
/// stops working. AC1-AC5, AC7-AC10, AC12; test-design I-12, the Token half of I-11, V-3 as far as
/// the schema carries it, the NFR-6 credentials row, and I-17.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written as red-phase scaffolds by the story 1.8 ATDD run on 2026-10-24, activated by its
/// dev-story run.</b> Routes are the <see cref="ApiTokenRoutes"/> constants and the issue body is
/// <see cref="IssueApiTokenRequest"/>; responses are still read as raw JSON, so an added property
/// cannot hide behind a DTO. The columns read by SQL are <c>ApiToken.Id</c>, <c>AccountId</c>,
/// <c>BoundSpaceId</c> and <c>SecretHash</c>.
/// </para>
/// <para>
/// <b>A Token request carries no cookie.</b> Every bearer call goes through
/// <see cref="WireSession.GetWithTokenAsync"/> and its siblings, which attach no cookie and no
/// anti-forgery header whatever the session holds, so a case asserting a Token was served cannot
/// pass because the Session cookie rode along. The one case about both credentials uses
/// <see cref="WireSession.GetWithTokenAndCookiesAsync"/> explicitly.
/// </para>
/// <para>
/// <b>In this class rather than a new one</b>, as a third partial file: a container-backed class is
/// one more SQL Server engine on a full run, and the machine has no headroom for it.
/// </para>
/// <para>
/// <b>Seed constraints checked</b> against <c>20260828164716_InitialSchema.cs</c>,
/// <c>20261024121107_SpaceBoundaryRefusals.cs</c> and <c>20261024143930_AccountScopedMembership.cs</c>:
/// every seeded <c>Membership</c>'s Account is the product-registered one or is added earlier in the
/// same block (<c>FK_Membership_Account_AccountId</c>, Restrict); every <c>Space</c> is added in the
/// block that first writes a <c>Membership</c> into it (<c>FK_Membership_Space_SpaceId</c>,
/// Cascade); at most one Owner per Space (<c>UX_Membership_SpaceId_Owner</c>); one Membership per
/// Account per Space (<c>AK_Membership_SpaceId_AccountId</c>, unique - and the alternate key Task 2
/// adds for the Token's composite foreign key). Every Token is issued through the product for a
/// Space its Account holds a Membership in, so the composite FK <c>(BoundSpaceId, AccountId) -&gt;
/// Membership(SpaceId, AccountId)</c> is satisfied by construction; no case inserts an
/// <c>ApiToken</c> row by hand. Deleting a Space cascades into <c>Membership</c>,
/// <c>StatusDefinition</c> and <c>SpacePresence</c> (all <c>Cascade</c>), and is run under that
/// Space's own session context so the FILTER predicate shows every row the cascade must reach.
/// Deleting an Account is legal only after its Memberships go (<c>Restrict</c>); nothing else holds
/// a foreign key to <c>Account</c> (<c>AccessRefusal</c> and <c>SpacePresence</c> carry none).
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    private const string TokenPrefix = "yello_pat_";

    private static readonly string[] TokenListRowProperties = ["createdAt", "id", "name", "space"];
    private static readonly string[] IssuedTokenProperties = ["createdAt", "id", "name", "space", "token"];

    /// <summary>
    /// AC1 / AC4 / NFR-6 - issuing a Token for a Space the Account belongs to answers <c>201</c> with
    /// the plaintext; the row is bound to that Space and holds a 32-byte hash, and no stored value
    /// anywhere equals or contains the plaintext.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "NFR-6")]
    [Trait("Requirement", "AD-6")]
    public async Task Issuing_a_Token_binds_it_to_the_Space_and_stores_only_a_hash()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var spaceId = Guid.NewGuid();
        var accountId = Guid.Empty;

        WireResponse? issued = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            accountId = await AccountIdOfAsync(database, address);

            await SeedNamedSpaceAsync(database, spaceId, "Alpha " + Suffix(), accountId, Role.Member);

            issued = await IssueAsync(session, spaceId, "deploy script");
        });

        Assert.NotNull(issued);
        Assert.Equal(HttpStatusCode.Created, issued.StatusCode);

        var token = IssuedToken(issued);

        Assert.Equal(IssuedTokenProperties, token.Properties);
        Assert.StartsWith(TokenPrefix, token.Plaintext, StringComparison.Ordinal);
        Assert.Equal(spaceId, token.SpaceId);

        var row = Assert.Single(await TokenRowsOfAsync(database, accountId));

        Assert.Equal(token.Id, row.Id);
        Assert.Equal(spaceId, row.BoundSpaceId);
        Assert.Equal(32, row.SecretHash.Length);
        Assert.NotEqual(System.Text.Encoding.UTF8.GetBytes(token.Plaintext), row.SecretHash);

        // "A read of the datastore yields no usable Token": every text value in every table, read on
        // a connection with no context (ApiToken carries no policy, Q4) and on the bound Space's.
        await using var plain = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var scoped = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);

        var stored = (await RegistrationDatabase.ReadEveryTextValueAsync(plain, TestContext.Current.CancellationToken))
            .Concat(await RegistrationDatabase.ReadEveryTextValueAsync(scoped, TestContext.Current.CancellationToken))
            .ToList();

        Assert.NotEmpty(stored);
        Assert.DoesNotContain(stored, value => value.Contains(token.Plaintext, StringComparison.Ordinal));
        Assert.DoesNotContain(stored, value => value.Contains(token.Plaintext[TokenPrefix.Length..], StringComparison.Ordinal));
    }

    /// <summary>
    /// AC1 / the capability matrix (<c>RoleCapabilityMatrix.cs:52</c>) - a Viewer may issue a Token
    /// for themselves; a Space the Account is not in and a Space that does not exist are both refused
    /// with the boundary's byte-identical <c>404</c>, each recorded as one <c>CrossSpace</c> refusal.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "AR-11")]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-20")]
    public async Task A_Viewer_may_issue_a_Token_and_a_Space_without_Membership_is_refused_as_the_boundary_404()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var viewed = Guid.NewGuid();
        var foreign = Guid.NewGuid();
        var missing = Guid.NewGuid();
        var accountId = Guid.Empty;

        WireResponse? asViewer = null;
        WireResponse? notAMember = null;
        WireResponse? noSuchSpace = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            accountId = await AccountIdOfAsync(database, address);

            await SeedNamedSpaceAsync(database, viewed, "Alpha " + Suffix(), accountId, Role.Viewer);
            await SeedForeignSpaceAsync(database, foreign, "Beta " + Suffix());

            asViewer = await IssueAsync(session, viewed, "viewer script");
            notAMember = await IssueAsync(session, foreign, "nope");
            noSuchSpace = await IssueAsync(session, missing, "nope");
        });

        Assert.NotNull(asViewer);
        Assert.Equal(HttpStatusCode.Created, asViewer.StatusCode);
        Assert.Equal(viewed, IssuedToken(asViewer).SpaceId);

        Assert.NotNull(notAMember);
        Assert.NotNull(noSuchSpace);
        Assert.Equal(HttpStatusCode.NotFound, notAMember.StatusCode);
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(notAMember));
        Assert.Equal(notAMember.Body, noSuchSpace.Body);
        Assert.Equal(notAMember.ContentType, noSuchSpace.ContentType);
        Assert.Equal(notAMember.HeaderNames, noSuchSpace.HeaderNames);

        // Only the Viewer's Token was written.
        Assert.Equal([viewed], (await TokenRowsOfAsync(database, accountId)).Select(row => row.BoundSpaceId));

        var refusals = await RefusalsOfAsync(database, accountId);

        Assert.Equal(new[] { foreign, missing }.Order(), refusals.Select(row => row.TargetSpaceId).Order());
        Assert.All(refusals, row =>
        {
            Assert.Equal("CrossSpace", row.Kind);
            Assert.Equal("404", row.Outcome);
            Assert.Equal(nameof(Capability.IssueApiTokenForOneself), row.Capability);
        });
    }

    /// <summary>
    /// AC2 / I-12 - an Account owning Spaces A and B issues a Token for A. Used against B, the Token
    /// is refused exactly as an Account with no Membership in B is - byte-identical body, the
    /// <c>CrossSpace</c> record against B - and the same for a Space of another Account. Anchored on
    /// the Token being served in A.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "AD-6")]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-20")]
    public async Task A_Token_is_refused_in_another_Space_its_Account_owns_exactly_as_a_non_member_is()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var (otherAddress, otherName) = (RandomAddress(), DisplayName());
        var spaceA = Guid.NewGuid();
        var spaceB = Guid.NewGuid();
        var strangers = Guid.NewGuid();
        var accountId = Guid.Empty;

        WireResponse? servedInA = null;
        WireResponse? tokenInB = null;
        WireResponse? nonMemberInB = null;
        WireResponse? tokenInStrangers = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            accountId = await AccountIdOfAsync(database, address);

            await SeedNamedSpaceAsync(database, spaceA, "Alpha " + Suffix(), accountId, Role.Owner);
            await SeedNamedSpaceAsync(database, spaceB, "Beta " + Suffix(), accountId, Role.Owner);
            await SeedForeignSpaceAsync(database, strangers, "Gamma " + Suffix());

            var token = IssuedToken(await IssueAsync(session, spaceA, "only A")).Plaintext;

            // The comparison arm: a Session of an Account holding no Membership in B.
            var outsider = await RegisteredAndSignedInAsync(client, otherAddress, otherName);

            servedInA = await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceA), token);
            tokenInB = await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceB), token);
            nonMemberInB = await outsider.GetAsync(SpaceRoutes.ActiveOf(spaceB));
            tokenInStrangers = await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(strangers), token);
        });

        Assert.NotNull(servedInA);
        Assert.Equal(HttpStatusCode.OK, servedInA.StatusCode);
        Assert.Equal(spaceA, ActiveSpaceAnswer(servedInA).Id);

        Assert.NotNull(tokenInB);
        Assert.NotNull(nonMemberInB);
        Assert.NotNull(tokenInStrangers);

        Assert.Equal(HttpStatusCode.NotFound, tokenInB.StatusCode);
        Assert.Equal(nonMemberInB.Body, tokenInB.Body);
        Assert.Equal(nonMemberInB.ContentType, tokenInB.ContentType);
        Assert.Equal(nonMemberInB.HeaderNames, tokenInB.HeaderNames);
        Assert.Equal(tokenInB.Body, tokenInStrangers.Body);
        Assert.Equal(tokenInB.HeaderNames, tokenInStrangers.HeaderNames);

        // Recorded against the ROUTE's Space - B and the stranger's - never against A, the bound one.
        var refusals = await RefusalsOfAsync(database, accountId);

        Assert.Equal(new[] { spaceB, strangers }.Order(), refusals.Select(row => row.TargetSpaceId).Order());
        Assert.All(refusals, row =>
        {
            Assert.Equal("CrossSpace", row.Kind);
            Assert.Equal("404", row.Outcome);
        });
    }

    /// <summary>
    /// AC2 / AD-3 - a Token used against a Space it is not bound to takes the same time to refuse as
    /// a non-member Session's request to that Space, by the B3 method over the wire.
    /// </summary>
    /// <remarks>
    /// The planted oracle that shows the method can see a skipped Membership read is
    /// <c>SpaceBoundaryPipelineTests.The_duration_method_tells_a_refusal_that_skips_the_Membership_read_from_one_that_makes_it</c>:
    /// in-process, because a planted early return cannot be put into a separate Host process from a
    /// test.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "AR-10")]
    [Trait("Requirement", "AD-3")]
    public async Task A_Token_used_in_another_Space_takes_as_long_to_refuse_as_a_non_member_Session()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var (otherAddress, otherName) = (RandomAddress(), DisplayName());
        var spaceA = Guid.NewGuid();
        var spaceB = Guid.NewGuid();

        DurationVerdict? verdict = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            var accountId = await AccountIdOfAsync(database, address);

            await SeedNamedSpaceAsync(database, spaceA, "Alpha " + Suffix(), accountId, Role.Owner);
            await SeedForeignSpaceAsync(database, spaceB, "Beta " + Suffix());

            var token = IssuedToken(await IssueAsync(session, spaceA, "only A")).Plaintext;
            var outsider = await RegisteredAndSignedInAsync(client, otherAddress, otherName);

            // Anchored: both arms are refusals of the same Space, not a refusal against a success.
            Assert.Equal(HttpStatusCode.NotFound, (await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceB), token)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(SpaceRoutes.ActiveOf(spaceB))).StatusCode);

            verdict = await DurationIndistinguishability.CompareAsync(
                async () => await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceB), token),
                async () => await outsider.GetAsync(SpaceRoutes.ActiveOf(spaceB)));
        });

        Assert.NotNull(verdict);
        Assert.True(verdict.AreIndistinguishable, verdict.Describe());
    }

    /// <summary>
    /// AC3 / AD-1 - a Token acts with its Account's <b>current</b> Role, read on each request: a
    /// Role changed between two Token requests reaches the second, and the Token row stores none.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "AR-4")]
    [Trait("Requirement", "AD-1")]
    [Trait("Requirement", "AD-6")]
    public async Task A_Token_acts_with_the_current_Role_read_on_each_request()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var spaceId = Guid.NewGuid();

        WireResponse? before = null;
        WireResponse? after = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            var accountId = await AccountIdOfAsync(database, address);
            var membership = SpaceSeeding.NewMembership(spaceId, accountId, Role.Admin);

            await SpaceSeeding.SeedAsync(database, spaceId, context =>
            {
                context.Spaces.Add(NamedSpace(spaceId, "Alpha " + Suffix()));
                context.Memberships.Add(membership);
            }, TestContext.Current.CancellationToken);

            var token = IssuedToken(await IssueAsync(session, spaceId, "role probe")).Plaintext;

            before = await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceId), token);

            // Demoted after issue: the Token must narrow on the very next request.
            await SpaceSeeding.SeedAsync(database, spaceId, context =>
            {
                context.Memberships.Attach(membership);
                membership.Role = Role.Viewer;
            }, TestContext.Current.CancellationToken);

            after = await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceId), token);
        });

        Assert.NotNull(before);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal("admin", ActiveSpaceAnswer(before).Role);

        Assert.NotNull(after);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Equal("viewer", ActiveSpaceAnswer(after).Role);
    }

    /// <summary>
    /// AC5 / AC8 / V-3 - removing the Membership deletes its Token in the same statement, and the
    /// next Token request is <c>401</c> even though the Account is still a member elsewhere. Anchored
    /// on a removal that is rolled back leaving the Token working.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "FR-2")]
    [Trait("Requirement", "AD-6")]
    public async Task Removing_the_Membership_invalidates_its_Token_in_the_same_statement()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var spaceId = Guid.NewGuid();
        var accountId = Guid.Empty;

        WireResponse? afterRollback = null;
        WireResponse? afterRemoval = null;
        var rowsAfterRollback = -1;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            accountId = await AccountIdOfAsync(database, address);

            await SeedNamedSpaceAsync(database, spaceId, "Alpha " + Suffix(), accountId, Role.Member);

            var token = IssuedToken(await IssueAsync(session, spaceId, "doomed")).Plaintext;

            // The negative anchor: the same DELETE, rolled back, leaves the Token and its row alone.
            await RemoveMembershipAsync(database, spaceId, accountId, commit: false);
            rowsAfterRollback = (await TokenRowsOfAsync(database, accountId)).Count;
            afterRollback = await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceId), token);

            await RemoveMembershipAsync(database, spaceId, accountId, commit: true);

            afterRemoval = await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceId), token);
        });

        Assert.Equal(1, rowsAfterRollback);
        Assert.NotNull(afterRollback);
        Assert.Equal(HttpStatusCode.OK, afterRollback.StatusCode);

        // Gone by the cascade, not by any Token code.
        Assert.Empty(await TokenRowsOfAsync(database, accountId));

        Assert.NotNull(afterRemoval);
        Assert.Equal(HttpStatusCode.Unauthorized, afterRemoval.StatusCode);
        Assert.Equal(ProblemTypes.AuthenticationFailed, ProblemType(afterRemoval));
    }

    /// <summary>
    /// AC5 / AC8 / V-3 - deleting the Space cascades Space -&gt; Membership -&gt; ApiToken in one
    /// statement, and the next Token request is <c>401</c>. Story 3.3 owns the event; this proves the
    /// schema carries it.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "AD-6")]
    public async Task Deleting_the_Space_invalidates_its_Tokens()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var doomed = Guid.NewGuid();
        var kept = Guid.NewGuid();
        var accountId = Guid.Empty;

        WireResponse? keptServed = null;
        WireResponse? afterDeletion = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            accountId = await AccountIdOfAsync(database, address);

            await SeedNamedSpaceAsync(database, doomed, "Alpha " + Suffix(), accountId, Role.Owner);
            await SeedNamedSpaceAsync(database, kept, "Beta " + Suffix(), accountId, Role.Owner);

            var doomedToken = IssuedToken(await IssueAsync(session, doomed, "doomed")).Plaintext;
            var keptToken = IssuedToken(await IssueAsync(session, kept, "kept")).Plaintext;

            // A request first, so a SpacePresence row exists for the cascade to take as well.
            Assert.Equal(HttpStatusCode.OK, (await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(doomed), doomedToken)).StatusCode);

            await DeleteSpaceAsync(database, doomed);

            afterDeletion = await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(doomed), doomedToken);
            keptServed = await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(kept), keptToken);
        });

        Assert.NotNull(keptServed);
        Assert.Equal(HttpStatusCode.OK, keptServed.StatusCode);

        Assert.Equal([kept], (await TokenRowsOfAsync(database, accountId)).Select(row => row.BoundSpaceId));

        Assert.NotNull(afterDeletion);
        Assert.Equal(HttpStatusCode.Unauthorized, afterDeletion.StatusCode);
        Assert.Equal(ProblemTypes.AuthenticationFailed, ProblemType(afterDeletion));
    }

    /// <summary>
    /// AC5 / V-3 - deleting an Account (its Memberships first, then the Account, in one transaction -
    /// the order <c>Restrict</c> forces and story 5.4 will follow) takes every Token it held with it.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "FR-3")]
    [Trait("Requirement", "AD-6")]
    public async Task Deleting_the_Account_with_its_Memberships_invalidates_its_Tokens()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var spaceId = Guid.NewGuid();
        var accountId = Guid.Empty;

        WireResponse? before = null;
        WireResponse? after = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            accountId = await AccountIdOfAsync(database, address);

            await SeedNamedSpaceAsync(database, spaceId, "Alpha " + Suffix(), accountId, Role.Member);

            var token = IssuedToken(await IssueAsync(session, spaceId, "doomed")).Plaintext;

            before = await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceId), token);

            await DeleteAccountAsync(database, accountId);

            after = await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceId), token);
        });

        Assert.NotNull(before);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        Assert.Empty(await TokenRowsOfAsync(database, accountId));

        Assert.NotNull(after);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    /// <summary>
    /// AC8 / Q3 - revoking a Token answers <c>204</c> and the next request with it is <c>401</c>; an id
    /// belonging to another Account answers the same <c>204</c> and that Token keeps working.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "FR-2")]
    [Trait("Requirement", "AD-24")]
    public async Task A_revoked_Token_is_refused_on_the_next_request_and_another_Accounts_Token_cannot_be_revoked()
    {
        var database = Available();
        var (addressX, nameX) = (RandomAddress(), DisplayName());
        var (addressY, nameY) = (RandomAddress(), DisplayName());
        var spaceX = Guid.NewGuid();
        var spaceY = Guid.NewGuid();

        WireResponse? crossRevoke = null;
        WireResponse? afterCrossRevoke = null;
        WireResponse? ownRevoke = null;
        WireResponse? afterOwnRevoke = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var x = await RegisteredAndSignedInAsync(client, addressX, nameX);
            await SeedNamedSpaceAsync(database, spaceX, "Alpha " + Suffix(), await AccountIdOfAsync(database, addressX), Role.Member);
            var tokenX = IssuedToken(await IssueAsync(x, spaceX, "x"));

            var y = await RegisteredAndSignedInAsync(client, addressY, nameY);
            await SeedNamedSpaceAsync(database, spaceY, "Beta " + Suffix(), await AccountIdOfAsync(database, addressY), Role.Member);
            _ = IssuedToken(await IssueAsync(y, spaceY, "y"));

            await y.FetchAntiforgeryTokenAsync();
            crossRevoke = await y.DeleteAsync(ApiTokenRoutes.RevokeOf(tokenX.Id));
            afterCrossRevoke = await x.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceX), tokenX.Plaintext);

            await x.FetchAntiforgeryTokenAsync();
            ownRevoke = await x.DeleteAsync(ApiTokenRoutes.RevokeOf(tokenX.Id));
            afterOwnRevoke = await x.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceX), tokenX.Plaintext);
        });

        Assert.NotNull(crossRevoke);
        Assert.Equal(HttpStatusCode.NoContent, crossRevoke.StatusCode);
        Assert.NotNull(afterCrossRevoke);
        Assert.Equal(HttpStatusCode.OK, afterCrossRevoke.StatusCode);

        Assert.NotNull(ownRevoke);
        Assert.Equal(HttpStatusCode.NoContent, ownRevoke.StatusCode);
        Assert.Equal(crossRevoke.HeaderNames, ownRevoke.HeaderNames);
        Assert.Equal(crossRevoke.Body, ownRevoke.Body);

        Assert.NotNull(afterOwnRevoke);
        Assert.Equal(HttpStatusCode.Unauthorized, afterOwnRevoke.StatusCode);
        Assert.Equal(ProblemTypes.AuthenticationFailed, ProblemType(afterOwnRevoke));
    }

    /// <summary>
    /// AC8 / Task 4 - every refused bearer answers exactly as no credential does: no credential, a
    /// malformed bearer, a well-formed unknown Token, a revoked Token and a Token whose Membership was
    /// removed are byte-identical <c>401 authentication-failed</c> - body, content type and header
    /// names, so a <c>WWW-Authenticate</c> that names the Token scheme is caught too.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "FR-2")]
    [Trait("Requirement", "AR-10")]
    public async Task Every_refused_Token_answers_exactly_as_no_credential_does()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var revokedSpace = Guid.NewGuid();
        var removedSpace = Guid.NewGuid();

        var refusals = new List<(string Cause, WireResponse Response)>();
        WireResponse? anchor = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            var accountId = await AccountIdOfAsync(database, address);

            await SeedNamedSpaceAsync(database, revokedSpace, "Alpha " + Suffix(), accountId, Role.Member);
            await SeedNamedSpaceAsync(database, removedSpace, "Beta " + Suffix(), accountId, Role.Member);

            var revoked = IssuedToken(await IssueAsync(session, revokedSpace, "revoked"));
            var removed = IssuedToken(await IssueAsync(session, removedSpace, "removed")).Plaintext;

            anchor = await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(removedSpace), removed);

            await session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await session.DeleteAsync(ApiTokenRoutes.RevokeOf(revoked.Id))).StatusCode);
            await RemoveMembershipAsync(database, removedSpace, accountId, commit: true);

            var route = SpaceRoutes.ActiveOf(revokedSpace);

            refusals.Add(("no credential", await session.GetAnonymouslyAsync(route)));
            refusals.Add(("malformed", await session.GetWithTokenAsync(route, "not-a-token")));
            refusals.Add(("unknown", await session.GetWithTokenAsync(route, WellFormedUnknownToken())));
            refusals.Add(("revoked", await session.GetWithTokenAsync(route, revoked.Plaintext)));
            refusals.Add(("Membership removed", await session.GetWithTokenAsync(route, removed)));
        });

        Assert.NotNull(anchor);
        Assert.Equal(HttpStatusCode.OK, anchor.StatusCode);

        Assert.Equal(5, refusals.Count);

        var expected = refusals[0].Response;

        Assert.Equal(HttpStatusCode.Unauthorized, expected.StatusCode);
        Assert.Equal(ProblemTypes.AuthenticationFailed, ProblemType(expected));

        Assert.All(refusals, refusal =>
        {
            Assert.True(expected.StatusCode == refusal.Response.StatusCode, $"{refusal.Cause}: {refusal.Response.StatusCode}");
            Assert.True(expected.Body == refusal.Response.Body, $"{refusal.Cause}: {refusal.Response.Body}");
            Assert.True(expected.ContentType == refusal.Response.ContentType, $"{refusal.Cause}: {refusal.Response.ContentType}");
            Assert.True(
                expected.HeaderNames.SequenceEqual(refusal.Response.HeaderNames, StringComparer.OrdinalIgnoreCase),
                $"{refusal.Cause}: {string.Join(", ", refusal.Response.HeaderNames)}");
        });
    }

    /// <summary>
    /// AC9 / the Token half of I-11 (<c>deferred-work.md:316</c>) / CAP-35 / AD-24 - a valid Token is
    /// answered at every Session-only surface exactly as no credential is: the switcher, the Token
    /// list, issue, revoke and <c>sessions/current</c> are <c>401</c>, nothing is enumerated, and a
    /// Token's sign-out attempt does not stamp <c>SessionsValidAfter</c>. Anchored on the same Token
    /// being served in its own Space.
    /// </summary>
    /// <remarks>
    /// <b>Sign-out is compared, not asserted <c>401</c>.</b> AC9 lists <c>DELETE /api/v1/sessions</c>
    /// among the 401s, but story 1.7 made sign-out with no Session idempotent (Q4) and anti-forgery
    /// answers a credential-less <c>DELETE</c> first - so "exactly as no credential" is a <c>400</c>
    /// there, and the wire gives exactly that: <c>400 antiforgery-token-missing</c> for both, pinned
    /// below. The Token scheme authenticates nothing on sign-out, so the request is the anonymous one.
    /// AC9 was amended to say so (Lee, 2026-10-24: it conflicts with 1.7 Q4).
    /// </remarks>
    [Fact]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "FR-35")]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "AD-24")]
    public async Task A_Token_reaches_none_of_the_Session_only_surfaces()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var spaceId = Guid.NewGuid();
        var accountId = Guid.Empty;

        WireResponse? served = null;
        var pairs = new List<(string Surface, WireResponse WithToken, WireResponse Anonymous)>();
        (WireResponse WithToken, WireResponse Anonymous)? signOut = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            accountId = await AccountIdOfAsync(database, address);

            await SeedNamedSpaceAsync(database, spaceId, "Alpha " + Suffix(), accountId, Role.Owner);

            var token = IssuedToken(await IssueAsync(session, spaceId, "script"));

            served = await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceId), token.Plaintext);

            var anonymous = new WireSession(client);

            pairs.Add(("switcher", await session.GetWithTokenAsync(SpaceRoutes.Directory, token.Plaintext), await anonymous.GetAsync(SpaceRoutes.Directory)));
            pairs.Add(("token list", await session.GetWithTokenAsync(ApiTokenRoutes.List, token.Plaintext), await anonymous.GetAsync(ApiTokenRoutes.List)));
            pairs.Add(("issue", await session.PostWithTokenAsync(ApiTokenRoutes.IssueIn(spaceId), NameBody("from a script"), token.Plaintext), await anonymous.PostAsync(ApiTokenRoutes.IssueIn(spaceId), NameBody("anonymous"), withAntiforgeryToken: false)));
            pairs.Add(("revoke", await session.DeleteWithTokenAsync(ApiTokenRoutes.RevokeOf(token.Id), token.Plaintext), await anonymous.DeleteAsync(ApiTokenRoutes.RevokeOf(token.Id), withAntiforgeryToken: false)));
            pairs.Add(("current session", await session.GetWithTokenAsync(SessionRoutes.Current, token.Plaintext), await anonymous.GetAsync(SessionRoutes.Current)));

            signOut = (
                await session.DeleteWithTokenAsync(SessionRoutes.Authenticate, token.Plaintext),
                await anonymous.DeleteAsync(SessionRoutes.Authenticate, withAntiforgeryToken: false));
        });

        Assert.NotNull(served);
        Assert.Equal(HttpStatusCode.OK, served.StatusCode);

        Assert.Equal(5, pairs.Count);
        Assert.All(pairs, pair =>
        {
            Assert.True(pair.WithToken.StatusCode == HttpStatusCode.Unauthorized, $"{pair.Surface}: {pair.WithToken.StatusCode}");
            Assert.True(ProblemTypes.AuthenticationFailed == ProblemType(pair.WithToken), $"{pair.Surface}: {pair.WithToken.Body}");
            Assert.True(pair.Anonymous.Body == pair.WithToken.Body, $"{pair.Surface}: differs from no credential");
            Assert.True(
                pair.Anonymous.HeaderNames.SequenceEqual(pair.WithToken.HeaderNames, StringComparer.OrdinalIgnoreCase),
                $"{pair.Surface}: header names differ from no credential");
            Assert.DoesNotContain(spaceId.ToString("D"), pair.WithToken.Body, StringComparison.OrdinalIgnoreCase);
        });

        Assert.NotNull(signOut);
        Assert.Equal(signOut.Value.Anonymous.StatusCode, signOut.Value.WithToken.StatusCode);
        Assert.Equal(signOut.Value.Anonymous.Body, signOut.Value.WithToken.Body);
        Assert.Equal(HttpStatusCode.BadRequest, signOut.Value.WithToken.StatusCode);
        Assert.Equal(ProblemTypes.AntiforgeryTokenMissing, ProblemType(signOut.Value.WithToken));

        // A Token never issues a Token: still exactly the one issued through the Session.
        Assert.Single(await TokenRowsOfAsync(database, accountId));
        Assert.Null(await SessionsValidAfterOfAsync(database, accountId));
    }

    /// <summary>
    /// Q11 (a) - signing out ends Sessions, not Tokens: after the Account signs out of its Session, a
    /// Token it issued still serves its Space. Recorded so the choice is visible rather than
    /// incidental.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "FR-2")]
    public async Task A_Token_keeps_working_after_its_Account_signs_out_of_a_Session()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var spaceId = Guid.NewGuid();

        WireResponse? cookieAfter = null;
        WireResponse? tokenAfter = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            await SeedNamedSpaceAsync(database, spaceId, "Alpha " + Suffix(), await AccountIdOfAsync(database, address), Role.Member);

            var token = IssuedToken(await IssueAsync(session, spaceId, "outlives the Session")).Plaintext;

            await session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await session.DeleteAsync(SessionRoutes.Authenticate)).StatusCode);

            cookieAfter = await session.GetAsync(SpaceRoutes.ActiveOf(spaceId));
            tokenAfter = await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceId), token);
        });

        // Anchor: the sign-out really ended the Session.
        Assert.NotNull(cookieAfter);
        Assert.Equal(HttpStatusCode.Unauthorized, cookieAfter.StatusCode);

        Assert.NotNull(tokenAfter);
        Assert.Equal(HttpStatusCode.OK, tokenAfter.StatusCode);
    }

    /// <summary>
    /// AC12 / Q5 (a) - a request carrying a valid Session cookie <b>and</b> a bearer is authenticated by
    /// the bearer alone: with an invalid bearer it is <c>401</c>, though the same cookie alone is
    /// served; and with a valid bearer bound to another Space it is the boundary <c>404</c>, though
    /// the cookie's Account is a member of the route's Space - the cookie is never consulted.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "AR-14")]
    [Trait("Requirement", "AD-7")]
    public async Task A_request_carrying_a_cookie_and_a_bearer_is_authenticated_by_the_bearer_alone()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var spaceId = Guid.NewGuid();

        var otherSpaceId = Guid.NewGuid();

        WireResponse? cookieOnly = null;
        WireResponse? cookieAndBadBearer = null;
        WireResponse? boundBearerServed = null;
        WireResponse? cookieAndBearerBoundElsewhere = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            var accountId = await AccountIdOfAsync(database, address);
            await SeedNamedSpaceAsync(database, spaceId, "Alpha " + Suffix(), accountId, Role.Member);
            await SeedNamedSpaceAsync(database, otherSpaceId, "Beta " + Suffix(), accountId, Role.Owner);

            cookieOnly = await session.GetAsync(SpaceRoutes.ActiveOf(spaceId));
            cookieAndBadBearer = await session.GetWithTokenAndCookiesAsync(SpaceRoutes.ActiveOf(spaceId), WellFormedUnknownToken());

            var boundElsewhere = IssuedToken(await IssueAsync(session, otherSpaceId, "bound to Beta")).Plaintext;

            boundBearerServed = await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(otherSpaceId), boundElsewhere);
            cookieAndBearerBoundElsewhere = await session.GetWithTokenAndCookiesAsync(SpaceRoutes.ActiveOf(spaceId), boundElsewhere);
        });

        Assert.NotNull(cookieOnly);
        Assert.Equal(HttpStatusCode.OK, cookieOnly.StatusCode);

        Assert.NotNull(cookieAndBadBearer);
        Assert.Equal(HttpStatusCode.Unauthorized, cookieAndBadBearer.StatusCode);
        Assert.Equal(ProblemTypes.AuthenticationFailed, ProblemType(cookieAndBadBearer));

        // Anchor: the bearer is valid where it is bound.
        Assert.NotNull(boundBearerServed);
        Assert.Equal(HttpStatusCode.OK, boundBearerServed.StatusCode);

        Assert.NotNull(cookieAndBearerBoundElsewhere);
        Assert.Equal(HttpStatusCode.NotFound, cookieAndBearerBoundElsewhere.StatusCode);
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(cookieAndBearerBoundElsewhere));
    }

    /// <summary>
    /// AC10 / NFR-6 / AR-34 / I-17 - the plaintext appears in the single issuance response and nowhere
    /// else: not in any line the Host wrote to stdout or stderr across issue, use, a cross-Space
    /// refusal, a corrupted bearer, listing and revocation, and not in any other response body.
    /// </summary>
    /// <remarks>
    /// Anchored on the log being captured at all (the Host's listening line is in it) and on the
    /// plaintext having really been issued.
    /// </remarks>
    [Fact]
    [Trait("Requirement", "NFR-6")]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-36")]
    public async Task The_plaintext_appears_only_in_the_issuance_response()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var spaceId = Guid.NewGuid();
        var elsewhere = Guid.NewGuid();
        var plaintext = string.Empty;

        var others = new List<WireResponse>();

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            await SeedNamedSpaceAsync(database, spaceId, "Alpha " + Suffix(), await AccountIdOfAsync(database, address), Role.Member);
            await SeedForeignSpaceAsync(database, elsewhere, "Beta " + Suffix());

            var issued = IssuedToken(await IssueAsync(session, spaceId, "logged nowhere"));
            plaintext = issued.Plaintext;

            others.Add(await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceId), plaintext));
            others.Add(await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(elsewhere), plaintext));
            others.Add(await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceId), plaintext + "x"));
            others.Add(await session.GetWithTokenAsync(SpaceRoutes.Directory, plaintext));
            others.Add(await session.GetAsync(ApiTokenRoutes.List));

            await session.FetchAntiforgeryTokenAsync();
            others.Add(await session.DeleteAsync(ApiTokenRoutes.RevokeOf(issued.Id)));
            others.Add(await session.GetWithTokenAsync(SpaceRoutes.ActiveOf(spaceId), plaintext));
        });

        Assert.StartsWith(TokenPrefix, plaintext, StringComparison.Ordinal);
        Assert.Contains(HostProcess.ListeningMarker, log, StringComparison.Ordinal);

        var secretBody = plaintext[TokenPrefix.Length..];

        Assert.DoesNotContain(secretBody, log, StringComparison.Ordinal);

        Assert.Equal(7, others.Count);
        Assert.All(others, response => Assert.DoesNotContain(secretBody, response.Body, StringComparison.Ordinal));
    }

    /// <summary>
    /// AC7 / AR-29 / AD-24 - the Token list in Account settings carries Space identity and nothing
    /// else: each row is exactly <c>{id, name, space{id, name}, createdAt}</c>, with no Role, no hash
    /// and no secret anywhere in the body. Read as raw JSON so an added property cannot hide behind a
    /// DTO.
    /// </summary>
    [Fact]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "AR-29")]
    [Trait("Requirement", "AD-24")]
    public async Task The_Token_list_carries_identity_only_and_no_Role()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var owned = Guid.NewGuid();
        var viewed = Guid.NewGuid();
        var ownedName = "Alpha " + Suffix();
        var viewedName = "Beta " + Suffix();

        WireResponse? listed = null;
        var issued = new List<IssuedTokenAnswer>();

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            var accountId = await AccountIdOfAsync(database, address);

            await SeedNamedSpaceAsync(database, owned, ownedName, accountId, Role.Owner);
            await SeedNamedSpaceAsync(database, viewed, viewedName, accountId, Role.Viewer);

            issued.Add(IssuedToken(await IssueAsync(session, owned, "owned script")));
            issued.Add(IssuedToken(await IssueAsync(session, viewed, "viewed script")));

            listed = await session.GetAsync(ApiTokenRoutes.List);
        });

        Assert.NotNull(listed);
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);

        using var body = JsonDocument.Parse(listed.Body);
        var rows = body.RootElement.EnumerateArray().ToList();

        Assert.Equal(issued.Select(token => token.Id).Order(), rows.Select(row => row.GetProperty("id").GetGuid()).Order());

        Assert.All(rows, row =>
        {
            Assert.Equal(TokenListRowProperties, PropertyNames(row));
            Assert.Equal(IdentityProperties, PropertyNames(row.GetProperty("space")));
        });

        Assert.Contains(rows, row => row.GetProperty("space").GetProperty("id").GetGuid() == viewed
            && row.GetProperty("space").GetProperty("name").GetString() == viewedName);

        Assert.DoesNotContain("role", listed.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("viewer", listed.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("owner", listed.Body, StringComparison.OrdinalIgnoreCase);
        Assert.All(issued, token => Assert.DoesNotContain(token.Plaintext[TokenPrefix.Length..], listed.Body, StringComparison.Ordinal));
    }

    /// <summary>
    /// Q8 - a Token's name is required, trimmed, 1-100 characters: an empty and a 101-character name are
    /// <c>400 invalid-api-token</c>, the type the client branches on, and nothing is written; a
    /// 100-character name is issued, and so is one padded with spaces beyond 100, which is trimmed.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "AR-34")]
    public async Task A_Token_name_outside_1_to_100_characters_is_refused_with_400()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var spaceId = Guid.NewGuid();
        var accountId = Guid.Empty;

        WireResponse? longest = null;
        WireResponse? padded = null;
        WireResponse? empty = null;
        WireResponse? tooLong = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var session = await RegisteredAndSignedInAsync(client, address, name);
            accountId = await AccountIdOfAsync(database, address);
            await SeedNamedSpaceAsync(database, spaceId, "Alpha " + Suffix(), accountId, Role.Member);

            longest = await IssueAsync(session, spaceId, new string('n', 100));
            padded = await IssueAsync(session, spaceId, "  " + new string('p', 100) + "  ");
            empty = await IssueAsync(session, spaceId, "   ");
            tooLong = await IssueAsync(session, spaceId, new string('n', 101));
        });

        Assert.NotNull(longest);
        Assert.Equal(HttpStatusCode.Created, longest.StatusCode);

        Assert.NotNull(padded);
        Assert.Equal(HttpStatusCode.Created, padded.StatusCode);

        using (var paddedBody = JsonDocument.Parse(padded.Body))
        {
            Assert.Equal(new string('p', 100), paddedBody.RootElement.GetProperty("name").GetString());
        }

        foreach (var refused in new[] { empty, tooLong })
        {
            Assert.NotNull(refused);
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.Equal(ProblemTypes.InvalidApiToken, ProblemType(refused));
        }

        Assert.Equal(2, (await TokenRowsOfAsync(database, accountId)).Count);
    }

    /// <summary>
    /// A body that is not JSON is read after the pipeline, not bound before it: with no credential it
    /// is the authentication boundary's <c>401</c>, identical to a well-formed body with no
    /// credential; from a non-member it is the boundary <c>404</c>, recorded; and from a member it is
    /// <c>400 invalid-api-token</c>. Code review of story 1.8.
    /// </summary>
    [Fact]
    [Trait("Priority", "P1")]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-20")]
    public async Task A_malformed_issue_body_is_answered_by_the_boundary_first()
    {
        var database = Available();
        var (address, name) = (RandomAddress(), DisplayName());
        var spaceId = Guid.NewGuid();
        var foreign = Guid.NewGuid();
        var accountId = Guid.Empty;

        WireResponse? anonymousMalformed = null;
        WireResponse? anonymousWellFormed = null;
        WireResponse? nonMember = null;
        WireResponse? member = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var anonymous = new WireSession(client);
            anonymousMalformed = await anonymous.PostAsync(ApiTokenRoutes.IssueIn(spaceId), MalformedBody(), withAntiforgeryToken: false);
            anonymousWellFormed = await anonymous.PostAsync(ApiTokenRoutes.IssueIn(spaceId), NameBody("script"), withAntiforgeryToken: false);

            var session = await RegisteredAndSignedInAsync(client, address, name);
            accountId = await AccountIdOfAsync(database, address);
            await SeedNamedSpaceAsync(database, spaceId, "Alpha " + Suffix(), accountId, Role.Member);
            await SeedForeignSpaceAsync(database, foreign, "Beta " + Suffix());

            await session.FetchAntiforgeryTokenAsync();
            nonMember = await session.PostAsync(ApiTokenRoutes.IssueIn(foreign), MalformedBody());
            member = await session.PostAsync(ApiTokenRoutes.IssueIn(spaceId), MalformedBody());
        });

        Assert.NotNull(anonymousMalformed);
        Assert.NotNull(anonymousWellFormed);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousMalformed.StatusCode);
        Assert.Equal(anonymousWellFormed.StatusCode, anonymousMalformed.StatusCode);
        Assert.Equal(anonymousWellFormed.Body, anonymousMalformed.Body);
        Assert.Equal(anonymousWellFormed.ContentType, anonymousMalformed.ContentType);

        Assert.NotNull(nonMember);
        Assert.Equal(HttpStatusCode.NotFound, nonMember.StatusCode);
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(nonMember));

        Assert.NotNull(member);
        Assert.Equal(HttpStatusCode.BadRequest, member.StatusCode);
        Assert.Equal(ProblemTypes.InvalidApiToken, ProblemType(member));

        var refusal = Assert.Single(await RefusalsOfAsync(database, accountId));
        Assert.Equal(foreign, refusal.TargetSpaceId);
        Assert.Equal("CrossSpace", refusal.Kind);

        Assert.Empty(await TokenRowsOfAsync(database, accountId));
    }

    private static StringContent MalformedBody() => new("{ not json", System.Text.Encoding.UTF8, "application/json");

    private static JsonContent NameBody(string name) => JsonContent.Create(new IssueApiTokenRequest(name), options: JsonOptions);

    /// <summary>
    /// Issues a Token through the Session, as the browser does: a fresh anti-forgery token first,
    /// because the one held was minted before sign-in and is bound to the anonymous caller.
    /// </summary>
    private static async Task<WireResponse> IssueAsync(WireSession session, Guid spaceId, string name)
    {
        await session.FetchAntiforgeryTokenAsync();

        return await session.PostAsync(ApiTokenRoutes.IssueIn(spaceId), NameBody(name));
    }

    private static IssuedTokenAnswer IssuedToken(WireResponse response)
    {
        Assert.True(response.StatusCode == HttpStatusCode.Created, $"Issue answered {response.StatusCode}: {response.Body}");

        using var body = JsonDocument.Parse(response.Body);
        var root = body.RootElement;

        return new IssuedTokenAnswer(
            PropertyNames(root),
            root.GetProperty("id").GetGuid(),
            root.GetProperty("space").GetProperty("id").GetGuid(),
            root.GetProperty("token").GetString() ?? string.Empty);
    }

    private static string[] PropertyNames(JsonElement element) =>
        [.. element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)];

    /// <summary>
    /// A bearer with the right prefix, alphabet and length that was never issued - Q9's format.
    /// </summary>
    private static string WellFormedUnknownToken() =>
        TokenPrefix + Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// A Space owned by a seeded Account the caller has nothing to do with. One B2 block: the
    /// <c>Account</c> before its <c>Membership</c>, the <c>Space</c> before both.
    /// </summary>
    private static Task SeedForeignSpaceAsync(RegistrationDatabase database, Guid spaceId, string name)
    {
        var stranger = SpaceSeeding.NewAccount();

        return SpaceSeeding.SeedAsync(database, spaceId, context =>
        {
            context.Accounts.Add(stranger);
            context.Spaces.Add(NamedSpace(spaceId, name));
            context.Memberships.Add(SpaceSeeding.NewMembership(spaceId, stranger.Id, Role.Owner));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// The Account's <c>ApiToken</c> rows. The table carries no policy (Q4), so no session context is
    /// needed; were it ever put under one, this read and Q4's record would change together.
    /// </summary>
    private static async Task<IReadOnlyList<TokenRow>> TokenRowsOfAsync(RegistrationDatabase database, Guid accountId)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Id, BoundSpaceId, SecretHash FROM dbo.[ApiToken] WHERE AccountId = @account";
        command.Parameters.Add(new SqlParameter("@account", accountId));

        var rows = new List<TokenRow>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            rows.Add(new TokenRow(reader.GetGuid(0), reader.GetGuid(1), await reader.GetFieldValueAsync<byte[]>(2, TestContext.Current.CancellationToken)));
        }

        return rows;
    }

    /// <summary>
    /// Removes a Membership under its Space's own context, committed or rolled back. The rolled-back
    /// form is the negative anchor for AC5's "same transaction".
    /// </summary>
    private static async Task RemoveMembershipAsync(RegistrationDatabase database, Guid spaceId, Guid accountId, bool commit)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = "DELETE FROM dbo.[Membership] WHERE SpaceId = @space AND AccountId = @account";
        command.Parameters.Add(new SqlParameter("@space", spaceId));
        command.Parameters.Add(new SqlParameter("@account", accountId));

        Assert.Equal(1, await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        if (commit)
        {
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Deletes a Space under its own context, so the FILTER predicate shows every Space-scoped row the
    /// cascade must reach. <c>Space</c> itself carries no policy.
    /// </summary>
    private static async Task DeleteSpaceAsync(RegistrationDatabase database, Guid spaceId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "DELETE FROM dbo.[Space] WHERE Id = @space";
        command.Parameters.Add(new SqlParameter("@space", spaceId));

        Assert.Equal(1, await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Deletes an Account the only way the schema allows - every Membership first (<c>Restrict</c>),
    /// then the Account - in one transaction, under the <c>AccountId</c> key, which story 1.7's
    /// predicate uses to show exactly that Account's Memberships across Spaces.
    /// </summary>
    private static async Task DeleteAccountAsync(RegistrationDatabase database, Guid accountId)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);

        await using (var key = connection.CreateCommand())
        {
            key.CommandText = "EXEC sp_set_session_context @key = N'AccountId', @value = @value, @read_only = 1";
            key.Parameters.Add(new SqlParameter("@value", SqlDbType.Variant) { Value = accountId });
            await key.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using var transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);

        foreach (var statement in new[]
        {
            "DELETE FROM dbo.[Membership] WHERE AccountId = @account",
            "DELETE FROM dbo.[Account] WHERE Id = @account",
        })
        {
            await using DbCommand command = connection.CreateCommand();

            command.Transaction = transaction;
#pragma warning disable CA2100 // Review SQL queries for security vulnerabilities - constant statements above.
            command.CommandText = statement;
#pragma warning restore CA2100
            command.Parameters.Add(new SqlParameter("@account", accountId));

            Assert.True(await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken) >= 1, statement);
        }

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<DateTimeOffset?> SessionsValidAfterOfAsync(RegistrationDatabase database, Guid accountId)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT SessionsValidAfter FROM dbo.[Account] WHERE Id = @account";
        command.Parameters.Add(new SqlParameter("@account", accountId));

        var value = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);

        return value is DateTimeOffset stamped ? stamped : null;
    }

    private sealed record IssuedTokenAnswer(string[] Properties, Guid Id, Guid SpaceId, string Plaintext);

    private sealed record TokenRow(Guid Id, Guid BoundSpaceId, byte[] SecretHash);
}
