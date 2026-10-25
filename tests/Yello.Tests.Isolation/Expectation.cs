using System.Net;
using Xunit;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// What one surface gets for one case, and how that is checked. Each kind sends the case's request
/// itself, so a row's expectation is the whole of what the case does on that surface.
/// </summary>
/// <param name="Summary">The expectation in a phrase, for failure messages and the case list.</param>
internal abstract record Expectation(string Summary)
{
    /// <summary>
    /// A further check after the answer has been compared, for what the answer alone cannot show (a
    /// row that must not have been written, a Token that must still work).
    /// </summary>
    public Func<IsolationWorld, CaseRequest, Task>? Then { get; init; }

    /// <summary>
    /// Sends the case's request on <paramref name="surface"/> and asserts this expectation.
    /// </summary>
    /// <param name="world">The world.</param>
    /// <param name="request">The case's request.</param>
    /// <param name="surface">The surface.</param>
    /// <returns>A task.</returns>
    public async Task VerifyAsync(IsolationWorld world, CaseRequest request, IsolationSurface surface)
    {
        await VerifyCoreAsync(world, request, surface);

        if (Then is not null)
        {
            await Then(world, request);
        }
    }

    /// <summary>
    /// The expectation's own assertion.
    /// </summary>
    /// <param name="world">The world.</param>
    /// <param name="request">The case's request.</param>
    /// <param name="surface">The surface.</param>
    /// <returns>A task.</returns>
    protected abstract Task VerifyCoreAsync(IsolationWorld world, CaseRequest request, IsolationSurface surface);

    /// <summary>
    /// Status, content type, body and header names (<c>Date</c> excluded): the bytes AD-3 requires to be
    /// identical, compared as values.
    /// </summary>
    /// <param name="expected">The anchor.</param>
    /// <param name="actual">The answer.</param>
    /// <param name="what">What is being compared, for the message.</param>
    protected static void AssertSameBytes(WireResponse expected, WireResponse actual, string what)
    {
        Assert.True(expected.StatusCode == actual.StatusCode, $"{what}: status {actual.StatusCode}, anchor {expected.StatusCode}. Body: {actual.Body}");
        Assert.Equal(expected.ContentType, actual.ContentType);
        Assert.Equal(expected.Body, actual.Body);
        Assert.Equal(expected.HeaderNames, actual.HeaderNames);
    }

    /// <summary>
    /// I-13: a refusal body holds no actor's Space name or display name in any form, and no Space id
    /// other than the one in the path.
    /// </summary>
    /// <param name="world">The world.</param>
    /// <param name="answer">The refusal.</param>
    /// <param name="path">The path that was sent.</param>
    protected static void AssertDisclosesNothing(IsolationWorld world, WireResponse answer, string path)
    {
        foreach (var actor in new[] { world.A, world.B })
        {
            Assert.DoesNotContain(actor.DisplayName, answer.Body, StringComparison.Ordinal);
            Assert.DoesNotContain(actor.SpaceName, answer.Body, StringComparison.Ordinal);

            var id = actor.SpaceId.ToString("D");

            if (!path.Contains(id, StringComparison.OrdinalIgnoreCase))
            {
                Assert.DoesNotContain(id, answer.Body, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}

/// <summary>
/// The boundary <c>404</c>: every path answers byte-identically to the same route on a Space that does
/// not exist, asked by the same actor on the same surface; each adds exactly one <c>CrossSpace</c> row
/// for (actor, Space) and no <c>InsufficientRole</c> row (AD-3, AD-20, NFR-7); and the body discloses
/// nothing (I-13).
/// </summary>
internal sealed record BoundaryNotFound() : Expectation("404 = the absent-Space anchor, one CrossSpace row per attempt")
{
    /// <inheritdoc />
    protected override async Task VerifyCoreAsync(IsolationWorld world, CaseRequest request, IsolationSurface surface)
    {
        var target = request.TargetSpaceId ?? throw new InvalidOperationException("A boundary refusal needs the Space in its route.");
        var before = await world.Database.RefusalKindsAsync(request.Actor.AccountId, target);

        foreach (var (path, anchorPath) in request.Paths)
        {
            var anchor = await request.SendAnchorAsync(surface, anchorPath);
            var answer = await request.SendAsync(surface, path);

            Assert.Equal(HttpStatusCode.NotFound, answer.StatusCode);
            AssertSameBytes(anchor, answer, path);
            AssertDisclosesNothing(world, answer, path);
        }

        var after = await world.Database.RefusalKindsAsync(request.Actor.AccountId, target);

        AssertAddedOnlyCrossSpace(before, after, request.Paths.Count);
    }

    /// <summary>
    /// The case added exactly <paramref name="added"/> rows, every one <c>CrossSpace</c>. Counted per
    /// kind rather than by position: rows written in the same instant tie on <c>OccurredAt</c> and their
    /// <c>Id</c> order is random.
    /// </summary>
    /// <param name="before">The kinds before the case.</param>
    /// <param name="after">The kinds after it.</param>
    /// <param name="added">How many rows the case should have added.</param>
    internal static void AssertAddedOnlyCrossSpace(IReadOnlyList<string> before, IReadOnlyList<string> after, int added)
    {
        Assert.Equal(before.Count + added, after.Count);
        Assert.Equal(
            before.Count(kind => kind == IsolationCases.CrossSpace) + added,
            after.Count(kind => kind == IsolationCases.CrossSpace));
        Assert.Equal(
            before.Count(kind => kind == IsolationCases.InsufficientRole),
            after.Count(kind => kind == IsolationCases.InsufficientRole));
    }
}

/// <summary>
/// The no-credential <c>401</c>: byte-identical to the same request with no credential at all.
/// </summary>
internal sealed record NoCredential() : Expectation("401 = the no-credential anchor")
{
    /// <inheritdoc />
    protected override async Task VerifyCoreAsync(IsolationWorld world, CaseRequest request, IsolationSurface surface)
    {
        foreach (var (path, _) in request.Paths)
        {
            var anchor = await world.SendWithoutCredentialAsync(request.Method, path);
            var answer = await request.Actor.SendAsync(surface, request.Method, path);

            Assert.Equal(HttpStatusCode.Unauthorized, answer.StatusCode);
            AssertSameBytes(anchor, answer, path);
            AssertDisclosesNothing(world, answer, path);
        }
    }
}

/// <summary>
/// Byte-identical to the anchor path asked by the same actor on the same surface, at a stated status,
/// and recording nothing.
/// </summary>
/// <param name="Status">The status both answer.</param>
/// <param name="ExpectedType">A problem type the body must carry, when there is one.</param>
internal sealed record SameAsAnchor(HttpStatusCode Status, string? ExpectedType = null) : Expectation("identical to the anchor, nothing recorded")
{
    /// <inheritdoc />
    protected override async Task VerifyCoreAsync(IsolationWorld world, CaseRequest request, IsolationSurface surface)
    {
        // The actor's refusals against any Space, so "nothing recorded" holds for a row with no Space in
        // its route as well.
        var before = await world.Database.RefusalCountAsync(request.Actor.AccountId);

        foreach (var (path, anchorPath) in request.Paths)
        {
            var anchor = await request.SendAnchorAsync(surface, anchorPath);
            var answer = await request.SendAsync(surface, path);

            Assert.Equal(Status, answer.StatusCode);
            AssertSameBytes(anchor, answer, path);
            AssertDisclosesNothing(world, answer, path);

            if (ExpectedType is not null)
            {
                Assert.Contains(ExpectedType, answer.Body, StringComparison.Ordinal);
            }
        }

        Assert.Equal(before, await world.Database.RefusalCountAsync(request.Actor.AccountId));
    }
}

/// <summary>
/// The request is served, and the answer passes a case-specific check.
/// </summary>
/// <param name="Status">The status.</param>
/// <param name="Check">The check.</param>
/// <param name="Description">The expectation in a phrase.</param>
internal sealed record Served(HttpStatusCode Status, Func<IsolationWorld, CaseRequest, WireResponse, Task> Check, string Description) : Expectation(Description)
{
    /// <inheritdoc />
    protected override async Task VerifyCoreAsync(IsolationWorld world, CaseRequest request, IsolationSurface surface)
    {
        foreach (var (path, _) in request.Paths)
        {
            var answer = await request.SendAsync(surface, path);

            Assert.True(Status == answer.StatusCode, $"{path}: {answer.StatusCode} {answer.Body}");
            await Check(world, request, answer);
        }
    }
}

/// <summary>
/// I-12: the answer is byte-identical to a non-member's Session asking for the same Space, and records
/// <c>CrossSpace</c> against that Space for the Token's holder (FR-36).
/// </summary>
internal sealed record SameAsNonMember() : Expectation("404 = a non-member Session at the same Space, CrossSpace recorded")
{
    /// <inheritdoc />
    protected override async Task VerifyCoreAsync(IsolationWorld world, CaseRequest request, IsolationSurface surface)
    {
        var target = request.TargetSpaceId ?? throw new InvalidOperationException("The case needs the Space in its route.");
        var before = await world.Database.RefusalKindsAsync(request.Actor.AccountId, target);

        foreach (var (path, _) in request.Paths)
        {
            var nonMember = await world.B.SendAsync(IsolationSurface.Session, request.Method, path);
            var answer = await request.Actor.SendAsync(surface, request.Method, path);

            Assert.Equal(HttpStatusCode.NotFound, answer.StatusCode);
            AssertSameBytes(nonMember, answer, path);
            AssertDisclosesNothing(world, answer, path);
        }

        var after = await world.Database.RefusalKindsAsync(request.Actor.AccountId, target);

        BoundaryNotFound.AssertAddedOnlyCrossSpace(before, after, request.Paths.Count);
    }
}

/// <summary>
/// AC3 / I-6: the boundary 404 and the same route on a Space that does not exist are indistinguishable
/// in body and in the Host's own duration (21 samples, median, 20% - the one method).
/// </summary>
internal sealed record IndistinguishableFromAbsent() : Expectation("indistinguishable from an absent Space in body and duration")
{
    /// <inheritdoc />
    protected override async Task VerifyCoreAsync(IsolationWorld world, CaseRequest request, IsolationSurface surface)
    {
        foreach (var (path, anchorPath) in request.Paths)
        {
            var anchor = await request.Actor.SendAsync(surface, request.Method, anchorPath);
            var answer = await request.Actor.SendAsync(surface, request.Method, path);

            Assert.Equal(HttpStatusCode.NotFound, answer.StatusCode);
            AssertSameBytes(anchor, answer, path);

            var verdict = await DurationIndistinguishability.CompareMeasuredAsync(
                () => world.Host.ServerDurationOfAsync(() => request.Actor.SendAsync(surface, request.Method, path)),
                () => world.Host.ServerDurationOfAsync(() => request.Actor.SendAsync(surface, request.Method, anchorPath)));

            TimingRecord.Add($"boundary vs absent / {surface}: {verdict.Describe()}");
            Assert.True(verdict.AreIndistinguishable, $"{surface}: {verdict.Describe()}");
        }
    }
}

/// <summary>
/// AC3 / I-6, story 2.2 AC10: the boundary 404 (the path) and an in-Space not-found by id (the anchor - the
/// same route in the actor's own Space, naming an id that matches nothing) are indistinguishable in body and
/// in the Host's own duration (21 samples, median, 20% - the one method). The in-Space arm records nothing
/// and the boundary arm writes an <c>AccessRefusal</c>, so this is where that work shows if it shows at all.
/// Each verdict is written to the test output for the Debug Log, whatever the outcome. Written by the story
/// 2.2 ATDD run for <c>ISO-TIMING-IN-SPACE</c>.
/// </summary>
internal sealed record IndistinguishableFromInSpaceNotFound() : Expectation("indistinguishable from an in-Space not-found in body and duration")
{
    /// <inheritdoc />
    protected override async Task VerifyCoreAsync(IsolationWorld world, CaseRequest request, IsolationSurface surface)
    {
        foreach (var (path, anchorPath) in request.Paths)
        {
            var anchor = await request.Actor.SendAsync(surface, request.Method, anchorPath);
            var answer = await request.Actor.SendAsync(surface, request.Method, path);

            Assert.Equal(HttpStatusCode.NotFound, answer.StatusCode);
            AssertSameBytes(anchor, answer, path);

            var verdict = await DurationIndistinguishability.CompareMeasuredAsync(
                () => world.Host.ServerDurationOfAsync(() => request.Actor.SendAsync(surface, request.Method, path)),
                () => world.Host.ServerDurationOfAsync(() => request.Actor.SendAsync(surface, request.Method, anchorPath)));

            TimingRecord.Add($"boundary vs in-Space not-found / {surface}: {verdict.Describe()}");
            Assert.True(verdict.AreIndistinguishable, $"{surface}: {verdict.Describe()}");
        }
    }
}

/// <summary>
/// I-12 timed: a Token bound to one owned Space, used at another, takes the same server time to refuse
/// as a non-member Session at that Space.
/// </summary>
internal sealed record IndistinguishableFromNonMember() : Expectation("indistinguishable from a non-member Session in body and duration")
{
    /// <inheritdoc />
    protected override async Task VerifyCoreAsync(IsolationWorld world, CaseRequest request, IsolationSurface surface)
    {
        foreach (var (path, _) in request.Paths)
        {
            AssertSameBytes(
                await world.B.SendAsync(IsolationSurface.Session, request.Method, path),
                await request.Actor.SendAsync(surface, request.Method, path),
                path);

            var verdict = await DurationIndistinguishability.CompareMeasuredAsync(
                () => world.Host.ServerDurationOfAsync(() => request.Actor.SendAsync(surface, request.Method, path)),
                () => world.Host.ServerDurationOfAsync(() => world.B.SendAsync(IsolationSurface.Session, request.Method, path)));

            TimingRecord.Add($"Token bound elsewhere vs non-member / {surface}: {verdict.Describe()}");
            Assert.True(verdict.AreIndistinguishable, $"{surface}: {verdict.Describe()}");
        }
    }
}

/// <summary>
/// AC4 / I-8: A and B alternately read their own statuses through a Host whose pool holds one
/// connection; each answer holds exactly its own Space's status ids, and the Host held exactly one
/// physical session throughout.
/// </summary>
internal sealed record EachSeesOnlyItsOwnOnOneConnection() : Expectation("alternating A and B on one connection, each sees only its own")
{
    private const int Alternations = 5;

    /// <inheritdoc />
    protected override async Task VerifyCoreAsync(IsolationWorld world, CaseRequest request, IsolationSurface surface)
    {
        var aOwn = IdsOf(await world.A.SendAsync(surface, HttpMethod.Get, SpaceRoutes.StatusesOf(world.A.SpaceId)));
        var bOwn = IdsOf(await world.B.SendAsync(surface, HttpMethod.Get, SpaceRoutes.StatusesOf(world.B.SpaceId)));

        Assert.Equal(3, aOwn.Count);
        Assert.Equal(3, bOwn.Count);
        Assert.Empty(aOwn.Intersect(bOwn));

        for (var round = 0; round < Alternations; round++)
        {
            var a = await world.A.SendAsync(surface, HttpMethod.Get, SpaceRoutes.StatusesOf(world.A.SpaceId));
            var b = await world.B.SendAsync(surface, HttpMethod.Get, SpaceRoutes.StatusesOf(world.B.SpaceId));

            Assert.Equal(aOwn.Order(), IdsOf(a).Order());
            Assert.Equal(bOwn.Order(), IdsOf(b).Order());
        }

        // Concurrent requests for both Spaces as well. A pool that is not pinned opens a second
        // physical connection for these, so the count below can only be one if the pin holds - which is
        // what makes the anchor falsifiable rather than true of any sequential run.
        var concurrent = await Task.WhenAll(Enumerable.Range(0, Alternations * 2).Select(index => index % 2 == 0
            ? world.A.GetDetachedAsync(world.Host.Client, surface, SpaceRoutes.StatusesOf(world.A.SpaceId))
            : world.B.GetDetachedAsync(world.Host.Client, surface, SpaceRoutes.StatusesOf(world.B.SpaceId))));

        for (var index = 0; index < concurrent.Length; index++)
        {
            Assert.Equal(index % 2 == 0 ? aOwn.Order() : bOwn.Order(), IdsOf(concurrent[index]).Order());
        }

        // The anchor is one, not zero: zero would mean the side query cannot see the Host's sessions,
        // and the case would have passed having observed nothing.
        Assert.Equal(1, await world.HostSessionCountAsync());
    }

    // A pool of one under concurrent load can time out; that must read as a status, not as a parse error.
    private static List<Guid> IdsOf(WireResponse answer)
    {
        Assert.True(answer.StatusCode == HttpStatusCode.OK, $"{answer.StatusCode} {answer.Body}");

        return IsolationCases.Ids(answer.Body);
    }
}

/// <summary>
/// The capability <c>403</c> (AD-3, AD-20, NFR-7, V-1): every path answers <c>403 insufficient-role</c>
/// naming <paramref name="Capability"/>, adds exactly one <c>InsufficientRole</c> row and no
/// <c>CrossSpace</c> row for (actor, Space), and the body discloses nothing (I-13). Written by the story
/// 2.1 ATDD run for <c>ISO-RECORD-KIND-INSUFFICIENT-ROLE</c>, the first wire <c>403</c>.
/// </summary>
/// <param name="Capability">The capability's stable code, for example <c>manage-projects</c>.</param>
internal sealed record RoleRefused(string Capability) : Expectation("403 insufficient-role, one InsufficientRole row, no CrossSpace row")
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new(System.Text.Json.JsonSerializerDefaults.Web);

    /// <inheritdoc />
    protected override async Task VerifyCoreAsync(IsolationWorld world, CaseRequest request, IsolationSurface surface)
    {
        var target = request.TargetSpaceId ?? throw new InvalidOperationException("A capability refusal needs the Space in its route.");
        var before = await world.Database.RefusalKindsAsync(request.Actor.AccountId, target);

        foreach (var (path, _) in request.Paths)
        {
            var answer = await request.SendAsync(surface, path);

            Assert.True(answer.StatusCode == HttpStatusCode.Forbidden, $"{path}: {answer.StatusCode} {answer.Body}");

            var problem = System.Text.Json.JsonSerializer.Deserialize<Yello.Contracts.ProblemResponse>(answer.Body, JsonOptions);

            Assert.Equal(Yello.Contracts.ProblemTypes.InsufficientRole, problem?.Type);
            Assert.Equal(Capability, problem?.Capability);
            AssertDisclosesNothing(world, answer, path);
        }

        var after = await world.Database.RefusalKindsAsync(request.Actor.AccountId, target);

        Assert.Equal(before.Count + request.Paths.Count, after.Count);
        Assert.Equal(
            before.Count(kind => kind == IsolationCases.InsufficientRole) + request.Paths.Count,
            after.Count(kind => kind == IsolationCases.InsufficientRole));
        Assert.Equal(
            before.Count(kind => kind == IsolationCases.CrossSpace),
            after.Count(kind => kind == IsolationCases.CrossSpace));
    }
}

/// <summary>
/// A row owned by a later story. It is never executed; it states what the surface will get.
/// </summary>
/// <param name="WillBe">What the surface will get once the owning story builds the route.</param>
internal sealed record Pending(string WillBe) : Expectation("pending: " + WillBe)
{
    /// <inheritdoc />
    protected override Task VerifyCoreAsync(IsolationWorld world, CaseRequest request, IsolationSurface surface) =>
        throw new InvalidOperationException("A pending row is never executed.");
}

/// <summary>
/// The measured gaps, for the Debug Log: each timing verdict is written to the test output too.
/// </summary>
internal static class TimingRecord
{
    /// <summary>
    /// Writes a verdict to the current test's output.
    /// </summary>
    /// <param name="line">The line.</param>
    public static void Add(string line) => TestContext.Current.TestOutputHelper?.WriteLine(line);
}
