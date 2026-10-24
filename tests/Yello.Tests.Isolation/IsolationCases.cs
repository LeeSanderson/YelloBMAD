using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.ApiTokens;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// SM-1's epic-1 isolation cases, held as data: one row per case, each naming the route it is about
/// and what <b>every</b> surface gets. The theory data is generated from this list as
/// <c>cases x surfaces</c>, so a row with no expectation for a surface fails as a gap (AC1, AD-4) and a
/// mapped route with no row fails as a gap (AC8, B6). Story 1.9 Tasks 4 and 5.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no "not applicable".</b> Where a surface cannot reach a route, its expectation says what
/// it gets - a Token on a Session-only route is the no-credential <c>401</c>, byte for byte.
/// </para>
/// <para>
/// <b>A case never branches on the surface.</b> It reads its expectation from the row; the only
/// surface-shaped code is <see cref="ProductActor.SendAsync"/>, which sends what each surface sends.
/// </para>
/// <para>
/// <b>Rows owned by a later story are rows too</b>, with <see cref="IsolationCase.PendingUntil"/> naming
/// it. They execute as nothing, and <c>IsolationCaseListTests</c> fails the suite once that story is no
/// longer <c>backlog</c> while the row is still pending.
/// </para>
/// <para>
/// <b>Two cross-cutting invariants are not rows</b>, because they are not requests: I-13 (no Space name
/// and no unrequested Space id in a refusal body) and I-17 (no secret in the case's log slice). Every
/// row's run applies both - see <see cref="CaseRun"/>.
/// </para>
/// </remarks>
internal static class IsolationCases
{
    /// <summary>
    /// The kind <c>AccessRefusal</c> stores for a cross-Space attempt.
    /// </summary>
    public const string CrossSpace = "CrossSpace";

    /// <summary>
    /// The kind <c>AccessRefusal</c> stores for a within-Space Role failure.
    /// </summary>
    public const string InsufficientRole = "InsufficientRole";

    /// <summary>
    /// Every case, active and pending.
    /// </summary>
    public static IReadOnlyList<IsolationCase> All { get; } =
    [
        new(
            "ISO-DIRECT-SPACE", "I-1, I-9", IsolationVector.DirectIdentifier, HttpMethod.Get, SpaceRoutes.Active,
            world => ForeignAsync(world, SpaceRoutes.ActiveOf),
            Both(new BoundaryNotFound())),

        new(
            "ISO-DIRECT-STATUSES", "I-1, I-3", IsolationVector.DirectIdentifier, HttpMethod.Get, SpaceRoutes.Statuses,
            world => ForeignAsync(world, SpaceRoutes.StatusesOf),
            Both(new BoundaryNotFound())),

        new(
            "ISO-WRITE-TOKEN", "I-2 (epic-1 analogue)", IsolationVector.Write, HttpMethod.Post, ApiTokenRoutes.Issue,
            world => ForeignAsync(world, ApiTokenRoutes.IssueIn, HttpMethod.Post),
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new BoundaryNotFound { Then = NoTokenWrittenInTheForeignSpaceAsync },
                [IsolationSurface.ApiToken] = new NoCredential { Then = NoTokenWrittenInTheForeignSpaceAsync },
            }),

        new(
            "ISO-LIST-SPACES", "I-3, I-11", IsolationVector.Listing, HttpMethod.Get, SpaceRoutes.Directory,
            world => OwnAsync(world, SpaceRoutes.Directory),
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.OK, ListsExactlyTheCallersSpaceAsync, "200, exactly [SA], rows exactly {id, name}"),
                [IsolationSurface.ApiToken] = new NoCredential(),
            }),

        new(
            "ISO-LIST-TOKENS", "I-11", IsolationVector.Listing, HttpMethod.Get, ApiTokenRoutes.List,
            world => OwnAsync(world, ApiTokenRoutes.List),
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.OK, ListsOnlyTheCallersTokensAsync, "200, only A's Tokens, rows exactly {createdAt, id, name, space}"),
                [IsolationSurface.ApiToken] = new NoCredential(),
            }),

        new(
            "ISO-REVOKE-FOREIGN-TOKEN", "I-4", IsolationVector.DirectIdentifier, HttpMethod.Delete, ApiTokenRoutes.Revoke,
            world => Task.FromResult(new CaseRequest(world.A, HttpMethod.Delete, [new(ApiTokenRoutes.RevokeOf(world.B.TokenId), ApiTokenRoutes.RevokeOf(Guid.NewGuid()))], TargetSpaceId: null)),
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new SameAsAnchor(HttpStatusCode.NoContent) { Then = TheForeignTokenStillWorksAsync },
                [IsolationSurface.ApiToken] = new NoCredential { Then = TheForeignTokenStillWorksAsync },
            }),

        new(
            "ISO-AGGREGATE", "I-3", IsolationVector.Aggregate, HttpMethod.Get, SpaceRoutes.Statuses,
            async world =>
            {
                // B is active in B's Space first, on both surfaces, so anything B touched is in play.
                foreach (var surface in Enum.GetValues<IsolationSurface>())
                {
                    Assert.Equal(HttpStatusCode.OK, (await world.B.SendAsync(surface, HttpMethod.Get, SpaceRoutes.StatusesOf(world.B.SpaceId))).StatusCode);
                }

                return await OwnAsync(world, SpaceRoutes.StatusesOf(world.A.SpaceId));
            },
            Both(new Served(HttpStatusCode.OK, HoldsExactlyTheCallersThreeStatusesAsync, "exactly SA's three status ids, none of SB's"))),

        new(
            "ISO-IDENTIFIER-CONFERS-NOTHING", "I-4", IsolationVector.DeepLink, HttpMethod.Get, SpaceRoutes.Active,
            world => Task.FromResult(new CaseRequest(
                world.A,
                HttpMethod.Get,
                [
                    new(SpaceRoutes.ActiveOf(world.B.SpaceId), SpaceRoutes.ActiveOf(Guid.NewGuid())),
                    new(SpaceRoutes.StatusesOf(world.B.SpaceId), SpaceRoutes.StatusesOf(Guid.NewGuid())),
                ],
                world.B.SpaceId)),
            Both(new BoundaryNotFound())),

        new(
            "ISO-TOKEN-BOUND-ELSEWHERE", "I-12", IsolationVector.Api, HttpMethod.Get, SpaceRoutes.Active,
            TokenBoundElsewhereAsync,
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.OK, (_, _, _) => Task.CompletedTask, "200 - a Session carries no Space (FR-2)"),
                [IsolationSurface.ApiToken] = new SameAsNonMember(),
            }),

        new(
            "ISO-UNMATCHED-UNDER-SPACE", "I-5", IsolationVector.DeepLink, HttpMethod.Get, UnmatchedApi,
            world => Task.FromResult(new CaseRequest(
                world.A,
                HttpMethod.Get,
                [new(SpaceRoutes.ActiveOf(world.B.SpaceId) + "/nothing", SpaceRoutes.ActiveOf(world.A.SpaceId) + "/nothing")],
                world.B.SpaceId)),
            Both(new SameAsAnchor(HttpStatusCode.NotFound, ExpectedType: ProblemTypes.NotAvailable))),

        new(
            "ISO-RECORD-KIND", "AC6, NFR-7", IsolationVector.RecordKind, HttpMethod.Get, SpaceRoutes.Statuses,
            world => Task.FromResult(new CaseRequest(
                world.A,
                HttpMethod.Get,
                [
                    new(SpaceRoutes.ActiveOf(world.B.SpaceId), SpaceRoutes.ActiveOf(Guid.NewGuid())),
                    new(SpaceRoutes.StatusesOf(world.B.SpaceId), SpaceRoutes.StatusesOf(Guid.NewGuid())),
                ],
                world.B.SpaceId)),
            Both(new BoundaryNotFound())),

        new(
            "ISO-RECORD-KIND-INSUFFICIENT-ROLE", "AC6, NFR-7", IsolationVector.RecordKind, HttpMethod.Get, SpaceRoutes.Active,
            world => throw new InvalidOperationException("Pending until 2.1."),
            Both(new Pending("403 for a Role below the route's capability, with one InsufficientRole row, distinguishable from CrossSpace")),
            PendingUntil: "2.1",
            PendingReason: "No route a Session or Token reaches today is refused for a Role (story 1.6 decision 4); the first wire 403 is story 2.1's. The filter is proved in-process by SpaceBoundaryPipelineTests."),

        new(
            "ISO-TIMING-BOUNDARY", "I-6", IsolationVector.Timing, HttpMethod.Get, SpaceRoutes.Active,
            world => ForeignAsync(world, SpaceRoutes.ActiveOf),
            Both(new IndistinguishableFromAbsent()),
            Runner: CaseRunner.Timing),

        new(
            "ISO-TIMING-TOKEN-ELSEWHERE", "I-6, I-12", IsolationVector.Timing, HttpMethod.Get, SpaceRoutes.Active,
            TokenBoundElsewhereAsync,
            new Dictionary<IsolationSurface, Expectation>
            {
                [IsolationSurface.Session] = new Served(HttpStatusCode.OK, (_, _, _) => Task.CompletedTask, "200 - a Session carries no Space (FR-2), so there is no refusal to time"),
                [IsolationSurface.ApiToken] = new IndistinguishableFromNonMember(),
            },
            Runner: CaseRunner.Timing),

        new(
            "ISO-TIMING-IN-SPACE", "I-6", IsolationVector.Timing, HttpMethod.Get, SpaceRoutes.Active,
            world => throw new InvalidOperationException("Pending until 2.2."),
            Both(new Pending("a boundary 404 and an in-Space not-found indistinguishable in body and duration (21 / median / 20%)")),
            Runner: CaseRunner.Timing,
            PendingUntil: "2.2",
            PendingReason: "No route looks a resource up by id inside a Space until story 2.2; the unmatched-path fallback does no resolution and is not the work AD-3 means (Q5)."),

        new(
            "ISO-POOLED", "I-8", IsolationVector.PooledConnection, HttpMethod.Get, SpaceRoutes.Statuses,
            world => OwnAsync(world, SpaceRoutes.StatusesOf(world.A.SpaceId)),
            Both(new EachSeesOnlyItsOwnOnOneConnection()),
            Runner: CaseRunner.Pooled),
    ];

    /// <summary>
    /// The fallback the Host maps under <c>/api</c> for any unmatched path.
    /// </summary>
    public static string UnmatchedApi => "/api/{**path}";

    /// <summary>
    /// The generated theory data: every active case on every surface, named "case id / surface".
    /// </summary>
    public static IEnumerable<TheoryDataRow<string, IsolationSurface>> CrossProduct =>
        All.Where(@case => @case.PendingUntil is null).SelectMany(Rows);

    /// <summary>
    /// The generated theory data for one runner's cases.
    /// </summary>
    /// <param name="runner">The runner.</param>
    /// <returns>Its rows.</returns>
    public static IEnumerable<TheoryDataRow<string, IsolationSurface>> CrossProductFor(CaseRunner runner) =>
        All.Where(@case => @case.PendingUntil is null && @case.Runner == runner).SelectMany(Rows);

    /// <summary>
    /// The boundary runner's rows, for <c>MemberData</c>.
    /// </summary>
    public static IEnumerable<TheoryDataRow<string, IsolationSurface>> BoundaryRows => CrossProductFor(CaseRunner.Boundary);

    /// <summary>
    /// The timing runner's rows, for <c>MemberData</c>.
    /// </summary>
    public static IEnumerable<TheoryDataRow<string, IsolationSurface>> TimingRows => CrossProductFor(CaseRunner.Timing);

    /// <summary>
    /// The pooled runner's rows, for <c>MemberData</c>.
    /// </summary>
    public static IEnumerable<TheoryDataRow<string, IsolationSurface>> PooledRows => CrossProductFor(CaseRunner.Pooled);

    /// <summary>
    /// A case by its id.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>The case.</returns>
    public static IsolationCase Named(string id) => All.Single(@case => string.Equals(@case.Id, id, StringComparison.Ordinal));

    private static IEnumerable<TheoryDataRow<string, IsolationSurface>> Rows(IsolationCase @case) =>
        Enum.GetValues<IsolationSurface>().Select(surface =>
            new TheoryDataRow<string, IsolationSurface>(@case.Id, surface) { TestDisplayName = $"{@case.Id} / {surface}" });

    private static Dictionary<IsolationSurface, Expectation> Both(Expectation expectation) =>
        Enum.GetValues<IsolationSurface>().ToDictionary(surface => surface, _ => expectation);

    private static Task<CaseRequest> ForeignAsync(IsolationWorld world, Func<Guid, string> route, HttpMethod? method = null) =>
        Task.FromResult(new CaseRequest(
            world.A,
            method ?? HttpMethod.Get,
            [new(route(world.B.SpaceId), route(Guid.NewGuid()))],
            world.B.SpaceId));

    private static Task<CaseRequest> OwnAsync(IsolationWorld world, string path) =>
        Task.FromResult(new CaseRequest(world.A, HttpMethod.Get, [new(path, path)], TargetSpaceId: null));

    /// <summary>
    /// A fresh Account that owns its Personal Space and a second Space; its Token is bound to the first,
    /// and the request is for the second. B, a non-member, is the comparison.
    /// </summary>
    private static async Task<CaseRequest> TokenBoundElsewhereAsync(IsolationWorld world)
    {
        var owner = await world.NewActorAsync("Cy");
        var second = await world.Database.AddOwnedSpaceAsync(owner.AccountId, $"{owner.DisplayName} second");

        return new CaseRequest(owner, HttpMethod.Get, [new(SpaceRoutes.ActiveOf(second), SpaceRoutes.ActiveOf(Guid.NewGuid()))], second);
    }

    private static async Task NoTokenWrittenInTheForeignSpaceAsync(IsolationWorld world, CaseRequest request) =>
        Assert.Equal(0, await world.Database.TokenCountAsync(request.Actor.AccountId, world.B.SpaceId));

    private static async Task TheForeignTokenStillWorksAsync(IsolationWorld world, CaseRequest request) =>
        Assert.Equal(HttpStatusCode.OK, (await world.B.SendAsync(IsolationSurface.ApiToken, HttpMethod.Get, SpaceRoutes.ActiveOf(world.B.SpaceId))).StatusCode);

    private static Task ListsExactlyTheCallersSpaceAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        using var document = JsonDocument.Parse(answer.Body);
        var rows = document.RootElement.EnumerateArray().ToList();

        Assert.Equal([world.A.SpaceId], rows.Select(row => row.GetProperty("id").GetGuid()));
        Assert.All(rows, row => Assert.Equal(["id", "name"], row.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)));
        Assert.DoesNotContain(world.B.SpaceId.ToString("D"), answer.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(world.B.DisplayName, answer.Body, StringComparison.Ordinal);

        return Task.CompletedTask;
    }

    private static Task ListsOnlyTheCallersTokensAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        using var document = JsonDocument.Parse(answer.Body);
        var rows = document.RootElement.EnumerateArray().ToList();

        Assert.Equal([world.A.TokenId], rows.Select(row => row.GetProperty("id").GetGuid()));
        Assert.All(rows, row => Assert.Equal(["createdAt", "id", "name", "space"], row.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)));
        Assert.All(rows, row => Assert.Equal(world.A.SpaceId, row.GetProperty("space").GetProperty("id").GetGuid()));
        Assert.DoesNotContain(world.B.TokenId.ToString("D"), answer.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(world.B.SpaceId.ToString("D"), answer.Body, StringComparison.OrdinalIgnoreCase);

        return Task.CompletedTask;
    }

    private static async Task HoldsExactlyTheCallersThreeStatusesAsync(IsolationWorld world, CaseRequest request, WireResponse answer)
    {
        var own = Ids(answer.Body);
        var theirs = Ids((await world.B.SendAsync(IsolationSurface.Session, HttpMethod.Get, SpaceRoutes.StatusesOf(world.B.SpaceId))).Body);

        Assert.Equal(3, own.Count);
        Assert.Equal(3, theirs.Count);
        Assert.Empty(own.Intersect(theirs));
    }

    /// <summary>
    /// The status ids in a statuses answer.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The ids.</returns>
    public static List<Guid> Ids(string body)
    {
        using var document = JsonDocument.Parse(body);

        return [.. document.RootElement.EnumerateArray().Select(row => row.GetProperty("id").GetGuid())];
    }
}

/// <summary>
/// One isolation case.
/// </summary>
/// <param name="Id">The case id, for example <c>ISO-DIRECT-SPACE</c>.</param>
/// <param name="TestDesignId">The test-design ids it lands (<c>test-design-qa.md:265-277</c>).</param>
/// <param name="Vector">How the attempt reaches across.</param>
/// <param name="Method">The route's method.</param>
/// <param name="Route">The route pattern, from a <c>Yello.Contracts</c> constant - the gap gate's key.</param>
/// <param name="Prepare">Builds the request against a world.</param>
/// <param name="Expectations">What each surface gets. Every surface, always.</param>
/// <param name="Runner">Which test class executes it.</param>
/// <param name="PendingUntil">The story that owns the row, while it is pending.</param>
/// <param name="PendingReason">Why it is pending.</param>
internal sealed record IsolationCase(
    string Id,
    string TestDesignId,
    IsolationVector Vector,
    HttpMethod Method,
    string Route,
    Func<IsolationWorld, Task<CaseRequest>> Prepare,
    IReadOnlyDictionary<IsolationSurface, Expectation> Expectations,
    CaseRunner Runner = CaseRunner.Boundary,
    string? PendingUntil = null,
    string? PendingReason = null);

/// <summary>
/// How an attempt reaches for another Space (the story's vector column).
/// </summary>
internal enum IsolationVector
{
    DirectIdentifier,
    DeepLink,
    Listing,
    Aggregate,
    Api,
    Write,
    Timing,
    PooledConnection,
    RecordKind,
}

/// <summary>
/// Which test class runs a case: the boundary cases share one world; the timing and pooled cases run
/// in collections with parallelisation disabled, on worlds of their own.
/// </summary>
internal enum CaseRunner
{
    Boundary,
    Timing,
    Pooled,
}

/// <summary>
/// What a case sends: who, how, and each path with its anchor - the same route on a Space that does not
/// exist (or, for a served case, the path itself).
/// </summary>
/// <param name="Actor">Who sends it.</param>
/// <param name="Method">The method.</param>
/// <param name="Paths">Each path and its anchor path.</param>
/// <param name="TargetSpaceId">The Space in the route, for the refusal record; <c>null</c> when there is none.</param>
internal sealed record CaseRequest(ProductActor Actor, HttpMethod Method, IReadOnlyList<CasePath> Paths, Guid? TargetSpaceId);

/// <summary>
/// One path a case sends and the path its answer is compared with.
/// </summary>
/// <param name="Path">The path sent.</param>
/// <param name="Anchor">The comparison path.</param>
internal sealed record CasePath(string Path, string Anchor);
