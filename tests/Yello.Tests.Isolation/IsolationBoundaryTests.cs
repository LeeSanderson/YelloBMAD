using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.ApiTokens;
using Yello.Contracts.Spaces;

namespace Yello.Tests.Isolation;

/// <summary>
/// SM-1's epic-1 rows: two Accounts, each with its own Personal Space, each reaching for the other's
/// by direct identifier, deep link, listing, aggregate and API - on the Session surface and the Token
/// surface alike. AC2, AC6, AC10; I-1, I-3, I-4, I-5, I-9, I-11, I-12, I-13, I-17.
/// </summary>
/// <remarks>
/// <para>
/// <b>Red-phase scaffolds, one fact per row of the story's case list, each looping both surfaces.</b>
/// That is the row's expectation written out. On activation each becomes a row of
/// <c>IsolationCases</c> with an expectation per surface, and these facts collapse into the one
/// generated theory over <c>cases x surfaces</c> (Task 4, B6). The assertions move with them.
/// </para>
/// <para>
/// <b>Every refusal compares bytes</b> - status, content type, body and header names - against the
/// anchor: <c>GET /spaces/{random}</c> on the same surface for a <c>404</c>, the same request with no
/// credential for a <c>401</c>. <b>Every absence is anchored on a presence</b>: the actor reads its own
/// Space on the same surface first. <b>Every case ends with its log slice checked</b> for secrets
/// (I-17), anchored on the Host having written a line for the case at all.
/// </para>
/// <para>
/// <b>Kinds are compared as text</b> (<c>"CrossSpace"</c>, <c>"InsufficientRole"</c>), as
/// <c>AccessRefusal.Kind</c> stores them (<c>nvarchar(32)</c>, migration <c>SpaceBoundaryRefusals</c>).
/// </para>
/// </remarks>
[Trait("Suite", "Isolation")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-35")]
[Trait("Requirement", "NFR-1")]
[Trait("Requirement", "AD-3")]
[Trait("Requirement", "AD-4")]
public sealed class IsolationBoundaryTests
{
    private const string CrossSpace = "CrossSpace";

    private const string InsufficientRole = "InsufficientRole";

    /// <summary>
    /// 1.9-ISO-001 / ISO-DIRECT-SPACE / I-1, I-9 - A asks for B's Space by its identifier and gets the
    /// boundary 404, byte-identical to a Space that does not exist, with one <c>CrossSpace</c> row.
    /// </summary>
    [Fact(Skip = "red: AC2 - the two-surface harness (Task 3) and the case row ISO-DIRECT-SPACE (Task 5) do not exist yet")]
    [Trait("Requirement", "FR-15")]
    [Trait("Requirement", "AD-20")]
    public async Task A_foreign_Space_by_identifier_is_the_boundary_404_on_both_surfaces()
    {
        await using var world = await IsolationWorld.StartAsync();

        foreach (var surface in IsolationWorld.Surfaces)
        {
            var cursor = world.LogCursor();

            var refusalsBefore = (await world.RefusalKindsAsync(world.A.AccountId, world.B.SpaceId)).Count;

            await AssertReadsOwnSpaceAsync(world, world.A, surface);

            var foreign = await world.SendAsync(world.A, surface, HttpMethod.Get, SpaceRoutes.ActiveOf(world.B.SpaceId));

            AssertSameBytes(await AbsentSpaceAnchorAsync(world, surface), foreign);
            AssertDisclosesNothing(world, foreign);

            var kinds = await world.RefusalKindsAsync(world.A.AccountId, world.B.SpaceId);
            Assert.Equal(refusalsBefore + 1, kinds.Count);
            Assert.Equal(CrossSpace, kinds[^1]);

            await AssertLogSliceHoldsNoSecretAsync(world, cursor);
        }
    }

    /// <summary>
    /// 1.9-ISO-002 / ISO-DIRECT-STATUSES / I-1, I-3 - B's statuses by B's Space identifier: the anchor
    /// 404, a <c>CrossSpace</c> row, and none of B's status names in the body.
    /// </summary>
    [Fact(Skip = "red: AC2 - the two-surface harness (Task 3) and the case row ISO-DIRECT-STATUSES (Task 5) do not exist yet")]
    [Trait("Requirement", "FR-15")]
    public async Task A_foreign_Spaces_statuses_are_the_boundary_404_on_both_surfaces()
    {
        await using var world = await IsolationWorld.StartAsync();

        foreach (var surface in IsolationWorld.Surfaces)
        {
            var cursor = world.LogCursor();
            var refusalsBefore = (await world.RefusalKindsAsync(world.A.AccountId, world.B.SpaceId)).Count;

            var own = await world.SendAsync(world.B, surface, HttpMethod.Get, SpaceRoutes.StatusesOf(world.B.SpaceId));
            Assert.Equal(200, own.Status);
            var bStatusNames = Names(own.Body);
            Assert.NotEmpty(bStatusNames);

            var foreign = await world.SendAsync(world.A, surface, HttpMethod.Get, SpaceRoutes.StatusesOf(world.B.SpaceId));

            AssertSameBytes(await AbsentAnchorAsync(world, surface, SpaceRoutes.StatusesOf(Guid.NewGuid())), foreign);
            AssertDisclosesNothing(world, foreign);
            Assert.All(bStatusNames, name => Assert.DoesNotContain(name, foreign.Body, StringComparison.Ordinal));

            var kinds = await world.RefusalKindsAsync(world.A.AccountId, world.B.SpaceId);
            Assert.Equal(refusalsBefore + 1, kinds.Count);
            Assert.Equal(CrossSpace, kinds[^1]);

            await AssertLogSliceHoldsNoSecretAsync(world, cursor);
        }
    }

    /// <summary>
    /// 1.9-ISO-003 / ISO-WRITE-TOKEN / I-2's epic-1 analogue - A tries to issue a Token in B's Space.
    /// Session: the boundary 404, a row, and no Token written. Token: the route is Session-only, so
    /// the bearer is no credential and the answer is the no-credential 401.
    /// </summary>
    [Fact(Skip = "red: AC2 - the two-surface harness (Task 3) and the case row ISO-WRITE-TOKEN (Task 5) do not exist yet")]
    [Trait("Requirement", "FR-36")]
    public async Task A_write_into_a_foreign_Space_writes_nothing_on_either_surface()
    {
        await using var world = await IsolationWorld.StartAsync();
        var route = ApiTokenRoutes.IssueIn(world.B.SpaceId);

        var cursor = world.LogCursor();
        var bySession = await world.SendAsync(world.A, ScaffoldSurface.Session, HttpMethod.Post, route);

        AssertSameBytes(await AbsentAnchorAsync(world, ScaffoldSurface.Session, ApiTokenRoutes.IssueIn(Guid.NewGuid()), HttpMethod.Post), bySession);
        Assert.Equal([CrossSpace], await world.RefusalKindsAsync(world.A.AccountId, world.B.SpaceId));
        Assert.Equal(0, await world.TokenCountAsync(world.A.AccountId, world.B.SpaceId));
        await AssertLogSliceHoldsNoSecretAsync(world, cursor);

        cursor = world.LogCursor();
        var byToken = await world.SendAsync(world.A, ScaffoldSurface.ApiToken, HttpMethod.Post, route);

        AssertSameBytes(await world.SendWithoutCredentialAsync(HttpMethod.Post, route), byToken);
        Assert.Equal(401, byToken.Status);
        Assert.Equal(0, await world.TokenCountAsync(world.A.AccountId, world.B.SpaceId));
        await AssertLogSliceHoldsNoSecretAsync(world, cursor);
    }

    /// <summary>
    /// 1.9-ISO-004 / ISO-LIST-SPACES / I-3, I-11 - the switcher lists exactly A's own Space, each row
    /// exactly <c>{id, name}</c> (AD-24's letter today). A Token is no credential there.
    /// </summary>
    [Fact(Skip = "red: AC2 - the two-surface harness (Task 3) and the case row ISO-LIST-SPACES (Task 5) do not exist yet")]
    [Trait("Requirement", "AD-24")]
    public async Task The_switcher_lists_only_the_callers_Spaces_and_a_Token_is_no_credential_there()
    {
        await using var world = await IsolationWorld.StartAsync();

        var cursor = world.LogCursor();
        var listed = await world.SendAsync(world.A, ScaffoldSurface.Session, HttpMethod.Get, SpaceRoutes.Directory);

        Assert.Equal(200, listed.Status);
        using (var document = JsonDocument.Parse(listed.Body))
        {
            var rows = document.RootElement.EnumerateArray().ToList();

            Assert.Equal([world.A.SpaceId], rows.Select(row => row.GetProperty("id").GetGuid()));
            Assert.All(rows, row => Assert.Equal(["id", "name"], row.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)));
        }

        Assert.DoesNotContain(world.B.SpaceId.ToString("D"), listed.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(world.B.SpaceName, listed.Body, StringComparison.Ordinal);
        await AssertLogSliceHoldsNoSecretAsync(world, cursor);

        cursor = world.LogCursor();
        var byToken = await world.SendAsync(world.A, ScaffoldSurface.ApiToken, HttpMethod.Get, SpaceRoutes.Directory);

        AssertSameBytes(await world.SendWithoutCredentialAsync(HttpMethod.Get, SpaceRoutes.Directory), byToken);
        Assert.Equal(401, byToken.Status);
        await AssertLogSliceHoldsNoSecretAsync(world, cursor);
    }

    /// <summary>
    /// 1.9-ISO-005 / ISO-LIST-TOKENS / I-11 - Account settings lists only A's Tokens, each row exactly
    /// <c>{createdAt, id, name, space}</c>, none bound to B's Space. A Token is no credential there.
    /// </summary>
    [Fact(Skip = "red: AC2 - the two-surface harness (Task 3) and the case row ISO-LIST-TOKENS (Task 5) do not exist yet")]
    [Trait("Requirement", "AD-24")]
    [Trait("Requirement", "FR-36")]
    public async Task The_Token_list_holds_only_the_callers_Tokens_and_a_Token_is_no_credential_there()
    {
        await using var world = await IsolationWorld.StartAsync();

        var cursor = world.LogCursor();
        var listed = await world.SendAsync(world.A, ScaffoldSurface.Session, HttpMethod.Get, ApiTokenRoutes.List);

        Assert.Equal(200, listed.Status);
        using (var document = JsonDocument.Parse(listed.Body))
        {
            var rows = document.RootElement.EnumerateArray().ToList();

            Assert.Equal([world.A.TokenId], rows.Select(row => row.GetProperty("id").GetGuid()));
            Assert.All(rows, row => Assert.Equal(
                ["createdAt", "id", "name", "space"],
                row.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)));
            Assert.All(rows, row => Assert.Equal(world.A.SpaceId, row.GetProperty("space").GetProperty("id").GetGuid()));
        }

        Assert.DoesNotContain(world.B.TokenId.ToString("D"), listed.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(world.B.SpaceId.ToString("D"), listed.Body, StringComparison.OrdinalIgnoreCase);
        await AssertLogSliceHoldsNoSecretAsync(world, cursor);

        cursor = world.LogCursor();
        var byToken = await world.SendAsync(world.A, ScaffoldSurface.ApiToken, HttpMethod.Get, ApiTokenRoutes.List);

        AssertSameBytes(await world.SendWithoutCredentialAsync(HttpMethod.Get, ApiTokenRoutes.List), byToken);
        await AssertLogSliceHoldsNoSecretAsync(world, cursor);
    }

    /// <summary>
    /// 1.9-ISO-006 / ISO-REVOKE-FOREIGN-TOKEN / I-4 - A revokes B's Token by its id: <c>204</c>, the
    /// same answer as revoking a Token that does not exist, and B's Token still works. A Token is no
    /// credential there.
    /// </summary>
    [Fact(Skip = "red: AC2 - the two-surface harness (Task 3) and the case row ISO-REVOKE-FOREIGN-TOKEN (Task 5) do not exist yet")]
    [Trait("Requirement", "FR-36")]
    public async Task Revoking_a_foreign_Token_by_its_id_changes_nothing_on_either_surface()
    {
        await using var world = await IsolationWorld.StartAsync();
        var route = ApiTokenRoutes.RevokeOf(world.B.TokenId);

        var cursor = world.LogCursor();
        var bySession = await world.SendAsync(world.A, ScaffoldSurface.Session, HttpMethod.Delete, route);

        AssertSameBytes(
            await world.SendAsync(world.A, ScaffoldSurface.Session, HttpMethod.Delete, ApiTokenRoutes.RevokeOf(Guid.NewGuid())),
            bySession);
        Assert.Equal(204, bySession.Status);
        Assert.Equal(200, (await world.SendAsync(world.B, ScaffoldSurface.ApiToken, HttpMethod.Get, SpaceRoutes.ActiveOf(world.B.SpaceId))).Status);
        await AssertLogSliceHoldsNoSecretAsync(world, cursor);

        cursor = world.LogCursor();
        var byToken = await world.SendAsync(world.A, ScaffoldSurface.ApiToken, HttpMethod.Delete, route);

        AssertSameBytes(await world.SendWithoutCredentialAsync(HttpMethod.Delete, route), byToken);
        Assert.Equal(200, (await world.SendAsync(world.B, ScaffoldSurface.ApiToken, HttpMethod.Get, SpaceRoutes.ActiveOf(world.B.SpaceId))).Status);
        await AssertLogSliceHoldsNoSecretAsync(world, cursor);
    }

    /// <summary>
    /// 1.9-ISO-007 / ISO-AGGREGATE / I-3 - after B has been active in B's Space, A's statuses are
    /// exactly A's three, on each surface: nothing B touched leaks into A's aggregate.
    /// </summary>
    [Fact(Skip = "red: AC2 - the two-surface harness (Task 3) and the case row ISO-AGGREGATE (Task 5) do not exist yet")]
    [Trait("Requirement", "FR-15")]
    public async Task An_aggregate_holds_only_the_callers_Space_after_the_other_Account_was_active()
    {
        await using var world = await IsolationWorld.StartAsync();

        foreach (var surface in IsolationWorld.Surfaces)
        {
            var cursor = world.LogCursor();
            var bStatuses = await world.SendAsync(world.B, surface, HttpMethod.Get, SpaceRoutes.StatusesOf(world.B.SpaceId));
            var aStatuses = await world.SendAsync(world.A, surface, HttpMethod.Get, SpaceRoutes.StatusesOf(world.A.SpaceId));

            Assert.Equal(200, bStatuses.Status);
            Assert.Equal(200, aStatuses.Status);

            var aIds = Ids(aStatuses.Body);
            Assert.Equal(3, aIds.Count);
            Assert.Empty(aIds.Intersect(Ids(bStatuses.Body)));

            await AssertLogSliceHoldsNoSecretAsync(world, cursor);
        }
    }

    /// <summary>
    /// 1.9-ISO-008 / ISO-IDENTIFIER-CONFERS-NOTHING / I-4 - A holds B's Space id (from B's URL) and
    /// uses it on every Space-scoped GET route: each answer is that route's absent-Space anchor.
    /// </summary>
    [Fact(Skip = "red: AC2 - the two-surface harness (Task 3) and the case row ISO-IDENTIFIER-CONFERS-NOTHING (Task 5) do not exist yet")]
    [Trait("Requirement", "FR-15")]
    public async Task Possessing_a_foreign_Space_identifier_confers_nothing_on_any_Space_route()
    {
        await using var world = await IsolationWorld.StartAsync();
        Func<Guid, string>[] routes = [SpaceRoutes.ActiveOf, SpaceRoutes.StatusesOf];

        foreach (var surface in IsolationWorld.Surfaces)
        {
            foreach (var route in routes)
            {
                var cursor = world.LogCursor();
                var foreign = await world.SendAsync(world.A, surface, HttpMethod.Get, route(world.B.SpaceId));

                AssertSameBytes(await AbsentAnchorAsync(world, surface, route(Guid.NewGuid())), foreign);
                AssertDisclosesNothing(world, foreign);
                await AssertLogSliceHoldsNoSecretAsync(world, cursor);
            }
        }
    }

    /// <summary>
    /// 1.9-ISO-009 / ISO-TOKEN-BOUND-ELSEWHERE / I-12 - A owns a second Space, SA2. A Session carries
    /// no Space (FR-2), so it reads SA2: <c>200</c>. A Token bound to SA is refused at SA2 exactly as a
    /// non-member is, with <c>CrossSpace</c> recorded against SA2 (FR-36). Different answers per
    /// surface, each stated - Q8 (a).
    /// </summary>
    [Fact(Skip = "red: AC2 - the two-surface harness (Task 3) and the case row ISO-TOKEN-BOUND-ELSEWHERE (Task 5) do not exist yet")]
    [Trait("Requirement", "FR-36")]
    [Trait("Requirement", "AD-7")]
    public async Task A_Token_bound_to_one_owned_Space_is_refused_at_another_the_Session_is_not()
    {
        await using var world = await IsolationWorld.StartAsync();
        var secondSpace = await world.AddSecondOwnedSpaceAsync(world.A);

        var cursor = world.LogCursor();
        var bySession = await world.SendAsync(world.A, ScaffoldSurface.Session, HttpMethod.Get, SpaceRoutes.ActiveOf(secondSpace));
        Assert.Equal(200, bySession.Status);
        await AssertLogSliceHoldsNoSecretAsync(world, cursor);

        cursor = world.LogCursor();
        var byToken = await world.SendAsync(world.A, ScaffoldSurface.ApiToken, HttpMethod.Get, SpaceRoutes.ActiveOf(secondSpace));
        var nonMember = await world.SendAsync(world.B, ScaffoldSurface.Session, HttpMethod.Get, SpaceRoutes.ActiveOf(secondSpace));

        AssertSameBytes(nonMember, byToken);
        Assert.Equal(404, byToken.Status);
        Assert.Equal([CrossSpace], await world.RefusalKindsAsync(world.A.AccountId, secondSpace));
        await AssertLogSliceHoldsNoSecretAsync(world, cursor);
    }

    /// <summary>
    /// 1.9-ISO-010 / ISO-UNMATCHED-UNDER-SPACE / I-5 - an unmatched path under B's Space and under A's
    /// own answer the same <c>404 not-available</c>, and neither records a refusal. The fallback does no
    /// resolution, so it is not a Space-existence oracle.
    /// </summary>
    [Fact(Skip = "red: AC2 - the two-surface harness (Task 3) and the case row ISO-UNMATCHED-UNDER-SPACE (Task 5) do not exist yet")]
    [Trait("Priority", "P1")]
    public async Task An_unmatched_path_under_a_foreign_Space_is_the_same_as_under_ones_own()
    {
        await using var world = await IsolationWorld.StartAsync();

        foreach (var surface in IsolationWorld.Surfaces)
        {
            var cursor = world.LogCursor();
            var own = await world.SendAsync(world.A, surface, HttpMethod.Get, SpaceRoutes.ActiveOf(world.A.SpaceId) + "/nothing");
            var foreign = await world.SendAsync(world.A, surface, HttpMethod.Get, SpaceRoutes.ActiveOf(world.B.SpaceId) + "/nothing");

            Assert.Equal(404, own.Status);
            Assert.Contains(ProblemTypes.NotAvailable, own.Body, StringComparison.Ordinal);
            AssertSameBytes(own, foreign);
            Assert.Empty(await world.RefusalKindsAsync(world.A.AccountId, world.B.SpaceId));

            await AssertLogSliceHoldsNoSecretAsync(world, cursor);
        }
    }

    /// <summary>
    /// 1.9-ISO-011 / ISO-RECORD-KIND / AC6, NFR-7, AD-20 - every boundary refusal on either surface is
    /// recorded <c>CrossSpace</c>, never <c>InsufficientRole</c>. The <c>InsufficientRole</c> half has no
    /// wire route until story 2.1 and is a pending row there.
    /// </summary>
    [Fact(Skip = "red: AC6 - the two-surface harness (Task 3) and the case row ISO-RECORD-KIND (Task 5) do not exist yet")]
    [Trait("Requirement", "NFR-7")]
    [Trait("Requirement", "AD-20")]
    public async Task Every_boundary_refusal_on_either_surface_is_recorded_as_cross_Space()
    {
        await using var world = await IsolationWorld.StartAsync();
        Func<Guid, string>[] routes = [SpaceRoutes.ActiveOf, SpaceRoutes.StatusesOf];

        foreach (var surface in IsolationWorld.Surfaces)
        {
            foreach (var route in routes)
            {
                Assert.Equal(404, (await world.SendAsync(world.A, surface, HttpMethod.Get, route(world.B.SpaceId))).Status);
            }
        }

        var kinds = await world.RefusalKindsAsync(world.A.AccountId, world.B.SpaceId);

        Assert.Equal(IsolationWorld.Surfaces.Length * routes.Length, kinds.Count);
        Assert.All(kinds, kind => Assert.Equal(CrossSpace, kind));
        Assert.DoesNotContain(InsufficientRole, kinds);
    }

    /// <summary>
    /// I-13 - a refusal body holds neither Account's Space name, no Space id other than the one in the
    /// URL, and nothing of the other Space's statuses.
    /// </summary>
    private static void AssertDisclosesNothing(IIsolationWorld world, IIsolationAnswer refusal)
    {
        Assert.DoesNotContain(world.A.SpaceName, refusal.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(world.B.SpaceName, refusal.Body, StringComparison.Ordinal);
        Assert.DoesNotContain(world.A.SpaceId.ToString("D"), refusal.Body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// I-17 / AC10 - the case's log slice holds none of the class's secrets. Anchored: the slice holds
    /// at least one line, so an empty capture cannot pass.
    /// </summary>
    private static async Task AssertLogSliceHoldsNoSecretAsync(IIsolationWorld world, long cursor)
    {
        var slice = await world.LogSinceAsync(cursor);

        Assert.False(string.IsNullOrWhiteSpace(slice), "The case's log slice is empty, so a secret check over it proves nothing.");
        Assert.NotEmpty(world.Secrets);

        var leaked = world.Secrets.Where(secret => slice.Contains(secret, StringComparison.Ordinal)).ToList();

        Assert.True(leaked.Count == 0, $"The Host logged {leaked.Count} secret(s) for this case (AC10, spine :274).");
    }

    private static async Task AssertReadsOwnSpaceAsync(IIsolationWorld world, IIsolationActor actor, ScaffoldSurface surface)
    {
        var own = await world.SendAsync(actor, surface, HttpMethod.Get, SpaceRoutes.ActiveOf(actor.SpaceId));

        Assert.Equal(200, own.Status);
        Assert.Contains(actor.SpaceName, own.Body, StringComparison.Ordinal);
    }

    private static Task<IIsolationAnswer> AbsentSpaceAnchorAsync(IIsolationWorld world, ScaffoldSurface surface) =>
        AbsentAnchorAsync(world, surface, SpaceRoutes.ActiveOf(Guid.NewGuid()));

    /// <summary>
    /// The anchor 404: the same route on a Space that does not exist, asked by A on the same surface.
    /// </summary>
    private static Task<IIsolationAnswer> AbsentAnchorAsync(
        IIsolationWorld world,
        ScaffoldSurface surface,
        string route,
        HttpMethod? method = null) =>
        world.SendAsync(world.A, surface, method ?? HttpMethod.Get, route);

    private static void AssertSameBytes(IIsolationAnswer expected, IIsolationAnswer actual)
    {
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.ContentType, actual.ContentType);
        Assert.Equal(expected.Body, actual.Body);
        Assert.Equal(expected.HeaderNames.Order(StringComparer.OrdinalIgnoreCase), actual.HeaderNames.Order(StringComparer.OrdinalIgnoreCase));
    }

    private static List<string> Names(string body)
    {
        using var document = JsonDocument.Parse(body);

        return [.. document.RootElement.EnumerateArray().Select(row => row.GetProperty("name").GetString() ?? string.Empty)];
    }

    private static List<Guid> Ids(string body)
    {
        using var document = JsonDocument.Parse(body);

        return [.. document.RootElement.EnumerateArray().Select(row => row.GetProperty("id").GetGuid())];
    }
}
