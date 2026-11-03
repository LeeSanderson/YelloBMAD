using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Accounts;
using Yello.Contracts.ApiTokens;
using Yello.Contracts.Invitations;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;
using Yello.Contracts.Memberships;
using Yello.Tests.Shared;

namespace Yello.Tests.Revocation;

/// <summary>
/// SM-2's request-path half, held as data like the isolation list: one row per event that ends or
/// narrows a credential, each stating what the very next request on <b>every</b> surface gets. AC9;
/// V-3, I-10, I-11, I-12; NFR-2's request-path clause. Story 1.9 Task 7.
/// </summary>
/// <remarks>
/// <para>
/// <b>Each row runs on a fresh Account</b>, anchored by the same credential succeeding on the same
/// request first, so a refusal cannot be a credential that never worked.
/// </para>
/// <para>
/// <b>Lee's decisions, 2026-10-24, are rows:</b> <c>REV-ENDED-BY-HOLDER</c> (sign-out ends Sessions
/// server-side, so the replayed cookie is <c>401</c>) and <c>REV-TOKEN-ON-SIGN-OUT</c> (a Token on
/// sign-out is no credential: <c>400 antiforgery-token-missing</c>, byte-identical to none, nothing
/// stamped).
/// </para>
/// </remarks>
internal static class RevocationCases
{
    private const string CrossSpace = "CrossSpace";

    /// <summary>
    /// The Owner's Space, as each <c>REV-ROLE-CHANGED-BY-OWNER</c> run's actor sees it after joining: its Session and a Token
    /// bound there. Story 4.4 (ATDD run).
    /// </summary>
    private static readonly ConditionalWeakTable<RevocationContext, ProductActor> Joined = new();

    /// <summary>
    /// Every revocation case.
    /// </summary>
    public static IReadOnlyList<RevocationCase> All { get; } =
    [
        new(
            "REV-ENDED-BY-HOLDER", "V-3, FR-2, FR-36",
            new Dictionary<IsolationSurface, RevocationStep>
            {
                [IsolationSurface.Session] = new(SignOutAsync, new NoCredential(ClearsTheDeadCookie: true)),
                [IsolationSurface.ApiToken] = new(RevokeOwnTokenAsync, new NoCredential()),
            }),

        new(
            "REV-MEMBERSHIP-REMOVED", "V-3, I-11",
            new Dictionary<IsolationSurface, RevocationStep>
            {
                [IsolationSurface.Session] = new(context => context.World.Database.RemoveMembershipAsync(context.Actor.SpaceId, context.Actor.AccountId, commit: true), new BoundaryNotFound()),
                [IsolationSurface.ApiToken] = new(context => context.World.Database.RemoveMembershipAsync(context.Actor.SpaceId, context.Actor.AccountId, commit: true), new NoCredential()),
            }),

        new(
            "REV-SPACE-DELETED", "V-3",
            new Dictionary<IsolationSurface, RevocationStep>
            {
                [IsolationSurface.Session] = new(context => context.World.Database.DeleteSpaceAsync(context.Actor.SpaceId), new BoundaryNotFound()),
                [IsolationSurface.ApiToken] = new(context => context.World.Database.DeleteSpaceAsync(context.Actor.SpaceId), new NoCredential()),
            }),

        // Story 3.3's row (AC11, deferred-work.md:382): the Owner deletes the Space through the product - the
        // endpoint-driven twin of REV-SPACE-DELETED, which stays as the raw-SQL schema proof.
        new(
            "REV-SPACE-DELETED-BY-OWNER", "V-3, NFR-2 (3.3 AC11)",
            new Dictionary<IsolationSurface, RevocationStep>
            {
                [IsolationSurface.Session] = new(OwnerDeletesTheSpaceAsync, new BoundaryNotFound()),
                [IsolationSurface.ApiToken] = new(OwnerDeletesTheSpaceAsync, new NoCredential()),
            })
        {
            // PRD section 12 #2 (story 3.4, AC14): selected on both surfaces by --filter "Assumption~PRD-12".
            Assumption = "PRD-12-2",
        },

        new(
            "REV-ACCOUNT-DELETED", "V-3",
            new Dictionary<IsolationSurface, RevocationStep>
            {
                [IsolationSurface.Session] = new(context => context.World.Database.DeleteAccountAsync(context.Actor.AccountId), new NoCredential(ClearsTheDeadCookie: true)),
                [IsolationSurface.ApiToken] = new(context => context.World.Database.DeleteAccountAsync(context.Actor.AccountId), new NoCredential()),
            }),

        new(
            "REV-ROLE-CHANGED", "I-10, NFR-2",
            Both(new RevocationStep(context => context.World.Database.ChangeRoleAsync(context.Actor.SpaceId, context.Actor.AccountId, "viewer"), new ServedAtRole("viewer")))),

        // Story 4.4's row (AC6, V-1; the 3.3 REV-SPACE-DELETED-BY-OWNER precedent): the Owner demotes an Admin to Member
        // through the product's PATCH - the endpoint-driven twin of REV-ROLE-CHANGED, which stays as the raw-SQL proof.
        new(
            "REV-ROLE-CHANGED-BY-OWNER", "I-10, NFR-2, V-1 (4.4 AC6)",
            Both(new RevocationStep(AdminDemotedByTheOwnerAsync, new GovernedAsMember())))
        {
            Assumption = "PRD-12-4",
        },

        new(
            "REV-ROLLED-BACK-REMOVAL", "V-3 (negative anchor)",
            Both(new RevocationStep(context => context.World.Database.RemoveMembershipAsync(context.Actor.SpaceId, context.Actor.AccountId, commit: false), new ServedAtRole("owner")))),

        new(
            "REV-SIGN-OUT-LEAVES-TOKEN", "story 1.8 Q11 (negative anchor)",
            new Dictionary<IsolationSurface, RevocationStep>
            {
                [IsolationSurface.Session] = new(SignOutAsync, new NoCredential(ClearsTheDeadCookie: true)),
                [IsolationSurface.ApiToken] = new(SignOutAsync, new ServedAtRole("owner")),
            }),

        new(
            "REV-TOKEN-ON-SIGN-OUT", "AR-14, Lee's decision 2",
            new Dictionary<IsolationSurface, RevocationStep>
            {
                [IsolationSurface.Session] = new(_ => Task.CompletedTask, new SignOutStamps()),
                [IsolationSurface.ApiToken] = new(_ => Task.CompletedTask, new SignOutIsNoCredential()),
            }),
    ];

    /// <summary>
    /// The generated theory data: every case on every surface, named "case id / surface".
    /// </summary>
    public static IEnumerable<TheoryDataRow<string, IsolationSurface>> CrossProduct =>
        All.SelectMany(@case => Enum.GetValues<IsolationSurface>().Select(surface =>
        {
            var row = new TheoryDataRow<string, IsolationSurface>(@case.Id, surface) { TestDisplayName = $"{@case.Id} / {surface}", Skip = @case.Skip };

            // A case resting on an unresolved assumption carries it as a trait on each surface's row (story 3.4, AC14).
            return @case.Assumption is { } assumption ? row.WithTrait("Assumption", assumption) : row;
        }));

    /// <summary>
    /// A case by its id.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>The case.</returns>
    public static RevocationCase Named(string id) => All.Single(@case => string.Equals(@case.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// Sign-out through the Session, then the pre-sign-out cookie is held again, so the next Session
    /// request is the replay Lee's decision 1 says grants nothing. Anchored: the sign-out answered
    /// <c>204</c> and stamped <c>SessionsValidAfter</c>.
    /// </summary>
    private static async Task SignOutAsync(RevocationContext context)
    {
        Assert.Equal(HttpStatusCode.NoContent, (await context.Actor.SignOutAndKeepTheOldCookieAsync()).StatusCode);
        Assert.NotNull(await context.World.Database.SessionsValidAfterAsync(context.Actor.AccountId));
    }

    /// <summary>
    /// Story 3.3 - the fresh Account, Owner of its Personal Space, deletes it through its Session, as the browser does:
    /// <c>204</c>. Whatever the surface under test, the event is the Owner's deletion in the browser; the very next request
    /// on that surface is what the row asserts (V-3: a script's Token dies with the Space).
    /// </summary>
    private static async Task OwnerDeletesTheSpaceAsync(RevocationContext context) =>
        Assert.Equal(HttpStatusCode.NoContent, (await context.Actor.SendAsync(IsolationSurface.Session, HttpMethod.Delete, SpaceRoutes.ActiveOf(context.Actor.SpaceId))).StatusCode);

    /// <summary>
    /// Story 4.4 - a fresh Owner invites the row's actor at Admin through 4.1's route; the actor accepts through 4.3's route
    /// and issues itself a Token bound to the Owner's Space; both surfaces list the Space's Invitations (<c>200</c>, the
    /// anchor: the Admin held the capability); then the Owner demotes it to Member through <c>PATCH .../memberships/{id}</c>
    /// (<c>200</c>). Both Accounts' addresses and names, and the Invitation token, are held as secrets (I-17).
    /// </summary>
    private static async Task AdminDemotedByTheOwnerAsync(RevocationContext context)
    {
        var (world, actor) = (context.World, context.Actor);
        var owner = await world.NewActorAsync();

        actor.HoldsSecret(actor.EmailAddress);
        actor.HoldsSecret(actor.DisplayName);
        owner.HoldsSecret(owner.EmailAddress);
        owner.HoldsSecret(owner.DisplayName);

        var issued = await owner.SendAsync(IsolationSurface.Session, HttpMethod.Post, InvitationRoutes.InvitationsOf(owner.SpaceId), InvitationWire.Body(actor.EmailAddress, "admin"), headers: null);

        Assert.True(issued.StatusCode == HttpStatusCode.NoContent, $"Issuing through the product: {issued.StatusCode} {issued.Body}");

        var token = InvitationWire.TokenIn(await world.Mail.WaitForEmailToAsync(actor.EmailAddress));

        owner.HoldsSecret(token);

        var accepted = await actor.SendAsync(IsolationSurface.Session, HttpMethod.Post, InvitationRoutes.Acceptance, InvitationWire.AcceptBody(token), headers: null);

        Assert.True(accepted.StatusCode == HttpStatusCode.OK, $"Accepting through the product: {accepted.StatusCode} {accepted.Body}");

        var view = actor.InSpace(owner.SpaceId, owner.SpaceName, await actor.IssueTokenAsync(owner.SpaceId, "rev-admin"));

        foreach (var surface in Enum.GetValues<IsolationSurface>())
        {
            var anchor = await view.SendAsync(surface, HttpMethod.Get, InvitationRoutes.InvitationsOf(owner.SpaceId));

            Assert.True(anchor.StatusCode == HttpStatusCode.OK, $"As Admin, {surface} lists the Invitations before the change: {anchor.StatusCode} {anchor.Body}");
        }

        var membership = await world.Database.MembershipOfAsync(owner.SpaceId, actor.AccountId);
        var demoted = await owner.SendAsync(IsolationSurface.Session, HttpMethod.Patch, MembershipRoutes.MembershipOf(owner.SpaceId, membership.Id), MembershipWire.ChangeBody("member"), headers: null);

        Assert.True(demoted.StatusCode == HttpStatusCode.OK, $"The Owner's demotion through the product: {demoted.StatusCode} {demoted.Body}");

        Joined.AddOrUpdate(context, view);
    }

    private static async Task RevokeOwnTokenAsync(RevocationContext context) =>
        Assert.Equal(HttpStatusCode.NoContent, (await context.Actor.Session.DeleteAsync(ApiTokenRoutes.RevokeOf(context.Actor.TokenId))).StatusCode);

    private static Dictionary<IsolationSurface, RevocationStep> Both(RevocationStep step) =>
        Enum.GetValues<IsolationSurface>().ToDictionary(surface => surface, _ => step);

    /// <summary>
    /// The very next request is the no-credential <c>401</c>: status, content type and body identical to
    /// a request with no credential at all.
    /// </summary>
    /// <param name="ClearsTheDeadCookie">
    /// Whether the refused credential is a Session cookie the Host now knows to be dead. Then the
    /// answer additionally tells the browser to drop it - an expiring <c>Set-Cookie</c> for the Session
    /// cookie with <c>Cache-Control</c>, <c>Expires</c> and <c>Pragma</c> - and is otherwise the anchor's
    /// header for header. Measured by story 1.9: the cookie handler does that for a rejected principal,
    /// and the difference tells the holder only that the cookie they sent is dead. A Token refusal has
    /// no cookie to clear and must be byte-identical.
    /// </param>
    private sealed record NoCredential(bool ClearsTheDeadCookie = false) : NextAnswer("401 = the no-credential anchor")
    {
        private static readonly string[] CookieClearingHeaders = ["Cache-Control", "Expires", "Pragma", "Set-Cookie"];

        public override async Task VerifyAsync(RevocationContext context, IsolationSurface surface)
        {
            var route = SpaceRoutes.ActiveOf(context.Actor.SpaceId);
            var anchor = await context.World.SendWithoutCredentialAsync(HttpMethod.Get, route);
            var next = await context.Actor.SendAsync(surface, HttpMethod.Get, route);

            Assert.Equal(HttpStatusCode.Unauthorized, next.StatusCode);

            if (!ClearsTheDeadCookie)
            {
                AssertSameBytes(anchor, next);

                return;
            }

            Assert.Equal(anchor.ContentType, next.ContentType);
            Assert.Equal(anchor.Body, next.Body);
            Assert.Equal(
                anchor.HeaderNames.Except(CookieClearingHeaders, StringComparer.OrdinalIgnoreCase),
                next.HeaderNames.Except(CookieClearingHeaders, StringComparer.OrdinalIgnoreCase));

            foreach (var header in CookieClearingHeaders)
            {
                Assert.True(next.Headers.ContainsKey(header), $"The dead-cookie 401 should carry {header}; it carries {string.Join(", ", next.HeaderNames)}.");
            }

            var cleared = Assert.Single(next.Headers["Set-Cookie"]);

            Assert.StartsWith("yello_session=;", cleared, StringComparison.Ordinal);
            Assert.Contains("expires=Thu, 01 Jan 1970", cleared, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The very next request is the boundary <c>404</c> - byte-identical to a Space that does not
    /// exist - with one <c>CrossSpace</c> row, and the Session itself still valid: the Account lost the
    /// Space, not its sign-in.
    /// </summary>
    private sealed record BoundaryNotFound() : NextAnswer("404 = the absent-Space anchor, CrossSpace recorded, Session still valid")
    {
        public override async Task VerifyAsync(RevocationContext context, IsolationSurface surface)
        {
            var before = await context.World.Database.RefusalKindsAsync(context.Actor.AccountId, context.Actor.SpaceId);
            var next = await context.Actor.SendAsync(surface, HttpMethod.Get, SpaceRoutes.ActiveOf(context.Actor.SpaceId));
            var anchor = await context.Actor.SendAsync(surface, HttpMethod.Get, SpaceRoutes.ActiveOf(Guid.NewGuid()));

            Assert.Equal(HttpStatusCode.NotFound, next.StatusCode);
            AssertSameBytes(anchor, next);

            var kinds = await context.World.Database.RefusalKindsAsync(context.Actor.AccountId, context.Actor.SpaceId);

            // Counted per kind, not by position: rows written in the same instant tie on OccurredAt.
            Assert.Equal(before.Count + 1, kinds.Count);
            Assert.Equal(before.Count(kind => kind == CrossSpace) + 1, kinds.Count(kind => kind == CrossSpace));
            Assert.True((await context.Actor.Session.GetAsync(SessionRoutes.Current)).StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent, "The Session should still be valid for the Account.");
        }
    }

    /// <summary>
    /// The very next request is served, at <paramref name="Role"/>: read per request, nothing cached.
    /// </summary>
    /// <param name="Role">The Role's stable code.</param>
    private sealed record ServedAtRole(string Role) : NextAnswer($"200, served at {Role}")
    {
        public override async Task VerifyAsync(RevocationContext context, IsolationSurface surface)
        {
            var next = await context.Actor.SendAsync(surface, HttpMethod.Get, SpaceRoutes.ActiveOf(context.Actor.SpaceId));

            Assert.True(next.StatusCode == HttpStatusCode.OK, $"{next.StatusCode} {next.Body}");

            using var document = JsonDocument.Parse(next.Body);

            Assert.Equal(Role, document.RootElement.GetProperty("role").GetString());
        }
    }

    /// <summary>
    /// Story 4.4 - after <see cref="AdminDemotedByTheOwnerAsync"/>, the very next request on the surface is governed by the new
    /// Role: listing the Owner's Space's Invitations is <c>403 insufficient-role</c> naming
    /// <c>issue-and-revoke-invitations</c>, and the actor's own write - a Project created there - still succeeds (<c>201</c>).
    /// Read per request, nothing cached (NFR-2).
    /// </summary>
    private sealed record GovernedAsMember() : NextAnswer("403 on the Invitations list, 201 on a Project create: governed as Member")
    {
        public override async Task VerifyAsync(RevocationContext context, IsolationSurface surface)
        {
            Assert.True(Joined.TryGetValue(context, out var view), "The event recorded no joined view of the Owner's Space.");

            var refused = await view.SendAsync(surface, HttpMethod.Get, InvitationRoutes.InvitationsOf(view.SpaceId));

            Assert.True(refused.StatusCode == HttpStatusCode.Forbidden, $"{surface}: the very next Invitations list answered {refused.StatusCode} {refused.Body}");
            Assert.Contains(ProblemTypes.InsufficientRole, refused.Body, StringComparison.Ordinal);
            Assert.Contains(InvitationWire.CapabilityCode, refused.Body, StringComparison.Ordinal);

            var written = await view.SendAsync(surface, HttpMethod.Post, ProjectRoutes.ListIn(view.SpaceId));

            Assert.True(written.StatusCode == HttpStatusCode.Created, $"{surface}: a Member's Project create answered {written.StatusCode} {written.Body}");
        }
    }

    /// <summary>
    /// The anchor for Lee's decision 2: the Session's own sign-out answers <c>204</c> and stamps.
    /// </summary>
    private sealed record SignOutStamps() : NextAnswer("sign-out with the Session: 204, SessionsValidAfter stamped")
    {
        public override async Task VerifyAsync(RevocationContext context, IsolationSurface surface)
        {
            Assert.Null(await context.World.Database.SessionsValidAfterAsync(context.Actor.AccountId));
            Assert.Equal(HttpStatusCode.NoContent, (await context.Actor.SignOutAndKeepTheOldCookieAsync()).StatusCode);
            Assert.NotNull(await context.World.Database.SessionsValidAfterAsync(context.Actor.AccountId));
        }
    }

    /// <summary>
    /// Lee's decision 2: a Token on <c>DELETE /api/v1/sessions</c> is no credential -
    /// <c>400 antiforgery-token-missing</c>, byte-identical to no credential at all, and nothing stamped.
    /// </summary>
    private sealed record SignOutIsNoCredential() : NextAnswer("sign-out with a Token: 400 antiforgery-token-missing = no credential, nothing stamped")
    {
        public override async Task VerifyAsync(RevocationContext context, IsolationSurface surface)
        {
            var withToken = await context.Actor.SignOutWithTokenAsync();
            var withNothing = await context.World.SendWithoutCredentialAsync(HttpMethod.Delete, SessionRoutes.Authenticate);

            Assert.Equal(HttpStatusCode.BadRequest, withToken.StatusCode);
            Assert.Contains(ProblemTypes.AntiforgeryTokenMissing, withToken.Body, StringComparison.Ordinal);
            AssertSameBytes(withNothing, withToken);
            Assert.Null(await context.World.Database.SessionsValidAfterAsync(context.Actor.AccountId));
        }
    }
}

/// <summary>
/// One revocation case.
/// </summary>
/// <param name="Id">The case id.</param>
/// <param name="TestDesignId">What it lands.</param>
/// <param name="Expectations">For each surface: the event, and what the very next request gets.</param>
internal sealed record RevocationCase(string Id, string TestDesignId, IReadOnlyDictionary<IsolationSurface, RevocationStep> Expectations)
{
    /// <summary>
    /// The unresolved assumption the case rests on (for example <c>PRD-12-2</c>), emitted as an <c>Assumption</c> trait on
    /// each surface's generated row (story 3.4, AC14), or <c>null</c>.
    /// </summary>
    public string? Assumption { get; init; }

    /// <summary>
    /// A red-phase skip reason (story 4.4's ATDD run): the row is generated on every surface but skipped with this reason
    /// until its story's route exists, or <c>null</c> for a live row.
    /// </summary>
    public string? Skip { get; init; }
}

/// <summary>
/// An event, and what the very next request on the surface gets after it.
/// </summary>
/// <param name="Event">What ends or narrows the credential.</param>
/// <param name="Next">What the next request gets.</param>
internal sealed record RevocationStep(Func<RevocationContext, Task> Event, NextAnswer Next);

/// <summary>
/// The world and the fresh Account a case runs on.
/// </summary>
/// <param name="World">The world.</param>
/// <param name="Actor">The Account.</param>
internal sealed record RevocationContext(RevocationWorld World, ProductActor Actor);

/// <summary>
/// What the very next request gets.
/// </summary>
/// <param name="Summary">The expectation in a phrase.</param>
internal abstract record NextAnswer(string Summary)
{
    /// <summary>
    /// Sends the next request on <paramref name="surface"/> and asserts.
    /// </summary>
    /// <param name="context">The case's world and Account.</param>
    /// <param name="surface">The surface.</param>
    /// <returns>A task.</returns>
    public abstract Task VerifyAsync(RevocationContext context, IsolationSurface surface);

    /// <summary>
    /// Status, content type, body and header names, compared as values.
    /// </summary>
    /// <param name="expected">The anchor.</param>
    /// <param name="actual">The answer.</param>
    protected static void AssertSameBytes(WireResponse expected, WireResponse actual)
    {
        Assert.Equal(expected.StatusCode, actual.StatusCode);
        Assert.Equal(expected.ContentType, actual.ContentType);
        Assert.Equal(expected.Body, actual.Body);
        Assert.True(
            expected.HeaderNames.SequenceEqual(actual.HeaderNames, StringComparer.Ordinal),
            $"Header names differ. Anchor: {string.Join(", ", expected.HeaderNames)}. Answer: {string.Join(", ", actual.HeaderNames)}.");
    }
}
