using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Accounts;
using Yello.Contracts.ApiTokens;
using Yello.Contracts.Spaces;
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
            new TheoryDataRow<string, IsolationSurface>(@case.Id, surface) { TestDisplayName = $"{@case.Id} / {surface}" }));

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
            Assert.Equal(anchor.HeaderNames, next.HeaderNames.Except(CookieClearingHeaders, StringComparer.OrdinalIgnoreCase));

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
            var before = (await context.World.Database.RefusalKindsAsync(context.Actor.AccountId, context.Actor.SpaceId)).Count;
            var next = await context.Actor.SendAsync(surface, HttpMethod.Get, SpaceRoutes.ActiveOf(context.Actor.SpaceId));
            var anchor = await context.Actor.SendAsync(surface, HttpMethod.Get, SpaceRoutes.ActiveOf(Guid.NewGuid()));

            Assert.Equal(HttpStatusCode.NotFound, next.StatusCode);
            AssertSameBytes(anchor, next);

            var kinds = await context.World.Database.RefusalKindsAsync(context.Actor.AccountId, context.Actor.SpaceId);

            Assert.Equal(before + 1, kinds.Count);
            Assert.Equal(CrossSpace, kinds[^1]);
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
internal sealed record RevocationCase(string Id, string TestDesignId, IReadOnlyDictionary<IsolationSurface, RevocationStep> Expectations);

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
