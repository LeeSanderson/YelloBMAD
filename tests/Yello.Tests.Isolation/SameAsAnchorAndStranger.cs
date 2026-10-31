using System.Net;
using Xunit;
using Yello.Contracts.Labels;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// Story 3.4 AC1, Q1 (a) (written by its ATDD run): an id that lives in the Account's other Space, named under its first,
/// is answered byte for byte as the in-Space not-found anchor (a random id on the same route) <b>and</b> byte for byte as
/// B - an Account with no Membership anywhere near that id - is answered for the same id under its own Space, on the same
/// surface; and nothing is recorded for either (Q5 (a): an in-Space not-found writes no record, so no cross-Space read).
/// </summary>
/// <param name="Status">The status all three answer.</param>
/// <param name="ExpectedType">A problem type the body must carry.</param>
internal sealed record SameAsAnchorAndStranger(HttpStatusCode Status, string? ExpectedType = null) : Expectation("identical to the in-Space anchor and to a stranger's answer, nothing recorded")
{
    /// <inheritdoc />
    protected override async Task VerifyCoreAsync(IsolationWorld world, CaseRequest request, IsolationSurface surface)
    {
        var strangerPaths = request.StrangerPaths ?? throw new InvalidOperationException("The case needs a stranger's path for each of its paths.");

        Assert.Equal(request.Paths.Count, strangerPaths.Count);

        var before = await world.Database.RefusalCountAsync(request.Actor.AccountId);
        var strangerBefore = await world.Database.RefusalCountAsync(world.B.AccountId);

        for (var index = 0; index < request.Paths.Count; index++)
        {
            var (path, anchorPath) = request.Paths[index];
            var anchor = await request.SendAnchorAsync(surface, anchorPath);
            var answer = await request.SendAsync(surface, path);
            var stranger = request.Body is null
                ? await world.B.SendAsync(surface, request.Method, strangerPaths[index])
                : await world.B.SendAsync(surface, request.Method, strangerPaths[index], request.Body(), headers: null);

            Assert.True(Status == answer.StatusCode, $"{path}: {answer.StatusCode} {answer.Body}");
            AssertSameBytes(anchor, answer, path);
            AssertSameBytes(stranger, answer, $"{path} against the stranger's {strangerPaths[index]}");
            AssertDisclosesNothing(world, answer, path);

            if (ExpectedType is not null)
            {
                Assert.Contains(ExpectedType, answer.Body, StringComparison.Ordinal);
            }
        }

        Assert.Equal(before, await world.Database.RefusalCountAsync(request.Actor.AccountId));
        Assert.Equal(strangerBefore, await world.Database.RefusalCountAsync(world.B.AccountId));
    }
}

/// <summary>
/// Story 3.4 AC4 (written by its ATDD run): one Account owning two Spaces alternates its reads of each Space's Statuses,
/// Projects and Labels through a Host whose pool holds one connection - on the ApiToken surface with a Token bound to each
/// Space - then reads both concurrently on detached clients; every answer holds exactly its own Space's ids, and the Host
/// held exactly one physical session throughout (the anchor is one, not zero).
/// </summary>
internal sealed record EachOwnSpaceOnOneConnection() : Expectation("one Account alternating its two Spaces on one connection, each sees only its own")
{
    private const int Alternations = 5;

    /// <inheritdoc />
    protected override async Task VerifyCoreAsync(IsolationWorld world, CaseRequest request, IsolationSurface surface)
    {
        var first = request.Actor;
        var second = request.Other ?? throw new InvalidOperationException("The case needs the Account's second Space.");
        var reads = new Func<Guid, string>[] { SpaceRoutes.StatusesOf, ProjectRoutes.ListIn, LabelRoutes.LabelsOf };

        foreach (var read in reads)
        {
            var firstOwn = IdsOf(await first.SendAsync(surface, HttpMethod.Get, read(first.SpaceId)));
            var secondOwn = IdsOf(await second.SendAsync(surface, HttpMethod.Get, read(second.SpaceId)));

            Assert.NotEmpty(firstOwn);
            Assert.NotEmpty(secondOwn);
            Assert.Empty(firstOwn.Intersect(secondOwn));

            for (var round = 0; round < Alternations; round++)
            {
                Assert.Equal(firstOwn.Order(), IdsOf(await first.SendAsync(surface, HttpMethod.Get, read(first.SpaceId))).Order());
                Assert.Equal(secondOwn.Order(), IdsOf(await second.SendAsync(surface, HttpMethod.Get, read(second.SpaceId))).Order());
            }

            // Concurrent as well: an unpinned pool opens a second connection for these, so the count of one below is
            // falsifiable (1.9 plant 13).
            var concurrent = await Task.WhenAll(Enumerable.Range(0, Alternations * 2).Select(index => index % 2 == 0
                ? first.GetDetachedAsync(world.Host.Client, surface, read(first.SpaceId))
                : second.GetDetachedAsync(world.Host.Client, surface, read(second.SpaceId))));

            for (var index = 0; index < concurrent.Length; index++)
            {
                Assert.Equal(index % 2 == 0 ? firstOwn.Order() : secondOwn.Order(), IdsOf(concurrent[index]).Order());
            }
        }

        Assert.Equal(1, await world.HostSessionCountAsync());
    }

    private static List<Guid> IdsOf(WireResponse answer)
    {
        Assert.True(answer.StatusCode == HttpStatusCode.OK, $"{answer.StatusCode} {answer.Body}");

        return IsolationCases.Ids(answer.Body);
    }
}

/// <summary>
/// Story 3.4 AC2, I-10 (written by its ATDD run): the Owner of one Space, a lesser Role in another, alternates an
/// Owner-level write in its own Space (<c>201</c>) with the same write in the other, twice, on one credential per Space;
/// each write in the other is <c>403 insufficient-role</c> naming <paramref name="Capability"/>, with one
/// <c>InsufficientRole</c> record each and no <c>CrossSpace</c> record, so no Role is carried from one Space to the next
/// (FR-15, NFR-2, AD-1).
/// </summary>
/// <param name="Capability">The capability the lesser Role lacks.</param>
internal sealed record RoleNotCarriedAcrossSpaces(string Capability) : Expectation("Owner write in SA, then 403 in SB, alternated; no Role carried")
{
    private const int Rounds = 2;

    /// <inheritdoc />
    protected override async Task VerifyCoreAsync(IsolationWorld world, CaseRequest request, IsolationSurface surface)
    {
        var elsewhere = request.Actor;
        var home = request.Other ?? throw new InvalidOperationException("The case needs the Owner's own Space.");
        var before = await world.Database.RefusalKindsAsync(elsewhere.AccountId, elsewhere.SpaceId);

        for (var round = 0; round < Rounds; round++)
        {
            foreach (var (path, _) in request.Paths)
            {
                var ownPath = path.Replace(elsewhere.SpaceId.ToString("D"), home.SpaceId.ToString("D"), StringComparison.OrdinalIgnoreCase);
                var own = await home.SendAsync(surface, request.Method, ownPath);

                Assert.True(own.StatusCode == HttpStatusCode.Created, $"The Owner's write in its own Space: {own.StatusCode} {own.Body}");

                var refused = await request.SendAsync(surface, path);

                Assert.True(refused.StatusCode == HttpStatusCode.Forbidden, $"{path} straight after an Owner-level write elsewhere: {refused.StatusCode} {refused.Body}");
                Assert.Contains(Capability, refused.Body, StringComparison.Ordinal);
                AssertDisclosesNothing(world, refused, path);
            }
        }

        var after = await world.Database.RefusalKindsAsync(elsewhere.AccountId, elsewhere.SpaceId);
        var added = Rounds * request.Paths.Count;

        Assert.Equal(before.Count + added, after.Count);
        Assert.Equal(before.Count(kind => kind == IsolationCases.InsufficientRole) + added, after.Count(kind => kind == IsolationCases.InsufficientRole));
        Assert.Equal(before.Count(kind => kind == IsolationCases.CrossSpace), after.Count(kind => kind == IsolationCases.CrossSpace));
    }
}
