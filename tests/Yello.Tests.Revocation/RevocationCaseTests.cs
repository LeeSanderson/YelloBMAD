using System.Collections;
using System.Reflection;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Accounts;
using Yello.Contracts.ApiTokens;
using Yello.Contracts.Spaces;

namespace Yello.Tests.Revocation;

/// <summary>
/// SM-2's request-path half on both surfaces: when a credential is ended or narrowed - sign-out, Token
/// revocation, Membership removal, Space deletion, Account deletion, Role change - the very next
/// request on each surface reflects it, with no tolerance. AC9, AC10; V-3, I-10, I-11, I-12; NFR-2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Red-phase scaffolds, one fact per event, each looping both surfaces</b> on a fresh Account.
/// On activation each event becomes a row of <c>RevocationCases</c> with an expectation per surface,
/// generated as <c>cases x surfaces</c> like the isolation list, and the both-surfaces gate below
/// holds it.
/// </para>
/// <para>
/// <b>Every event is anchored</b>: the same credential succeeds on the same request first, so a
/// refusal cannot be a credential that never worked.
/// </para>
/// <para>
/// <b>Lee's decisions, 2026-10-24, are expectations here:</b> (1) sign-out ends Sessions server-side
/// through <c>SessionsValidAfter</c>, so the replayed cookie is <c>401</c>; (2) a Token on sign-out is
/// no credential - <c>400 antiforgery-token-missing</c>, byte-identical to no credential, nothing
/// stamped.
/// </para>
/// </remarks>
[Trait("Suite", "Revocation")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-35")]
[Trait("Requirement", "NFR-2")]
[Trait("Requirement", "FR-34")]
[Trait("Requirement", "AD-4")]
public sealed class RevocationCaseTests
{
    private const string CrossSpace = "CrossSpace";

    /// <summary>
    /// 1.9-REV-000 / AC1 - every revocation case declares an expectation for every surface; the gate
    /// is the isolation list's, generic over this list.
    /// </summary>
    [Fact(Skip = "red: AC9 - the case list RevocationCases does not exist yet (story 1.9 Tasks 4 and 7)")]
    public void Every_revocation_case_declares_an_expectation_for_every_surface()
    {
        var cases = typeof(RevocationCaseTests).Assembly.GetType("Yello.Tests.Revocation.RevocationCases")
            ?.GetProperty("All", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?.GetValue(null) as IEnumerable
            ?? throw new InvalidOperationException("Yello.Tests.Revocation.RevocationCases.All does not exist (story 1.9 Task 7).");

        var surfaceType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => Array.Find(assembly.GetTypes(), type => type.IsEnum && type.Name == "IsolationSurface"))
            .FirstOrDefault(type => type is not null)
            ?? throw new InvalidOperationException("IsolationSurface does not exist (story 1.9 Task 3).");

        var surfaces = Enum.GetNames(surfaceType);
        var rows = cases.Cast<object>().ToList();

        // Vacuity guard: the five events plus the anchors, and two surfaces.
        Assert.True(rows.Count >= 5, $"Expected at least the five revocation events; found {rows.Count}.");
        Assert.True(surfaces.Length >= 2);

        var gaps = rows
            .SelectMany(row =>
            {
                var declared = (row.GetType().GetProperty("Expectations")?.GetValue(row) as IDictionary)?.Keys.Cast<object>()
                    .Select(key => key.ToString()).ToHashSet(StringComparer.Ordinal) ?? [];

                return surfaces.Where(surface => !declared.Contains(surface))
                    .Select(surface => $"{row.GetType().GetProperty("Id")?.GetValue(row)} / {surface}");
            })
            .ToList();

        Assert.True(gaps.Count == 0, "A revocation case exists for one surface and not the other (AD-4): " + string.Join(", ", gaps));
    }

    /// <summary>
    /// 1.9-REV-001 - the credential ended by its holder. Session: sign-out, then the replayed
    /// pre-sign-out cookie is <c>401</c> (Lee's decision 1). Token: revoked, then the next bearer
    /// request is <c>401</c>. Both byte-identical to no credential.
    /// </summary>
    [Fact(Skip = "red: AC9 - the two-surface harness (Task 3) and the revocation rows (Task 7) do not exist yet")]
    [Trait("Requirement", "FR-2")]
    [Trait("Requirement", "FR-36")]
    public async Task A_credential_its_holder_ended_is_refused_on_the_very_next_request()
    {
        foreach (var surface in RevocationWorld.Surfaces)
        {
            await using var world = await RevocationWorld.StartAsync();
            var route = SpaceRoutes.ActiveOf(world.SpaceId);
            var cursor = world.LogCursor();

            Assert.Equal(200, (await world.SendAsync(surface, HttpMethod.Get, route)).Status);

            if (surface == RevocationSurface.Session)
            {
                Assert.Equal(204, (await world.SignOutAsync()).Status);
                Assert.NotNull(await world.SessionsValidAfterAsync());
            }
            else
            {
                Assert.Equal(204, (await world.SendAsync(RevocationSurface.Session, HttpMethod.Delete, ApiTokenRoutes.RevokeOf(world.TokenId))).Status);
            }

            var next = await world.SendAsync(surface, HttpMethod.Get, route);

            AssertSameBytes(await world.SendWithoutCredentialAsync(HttpMethod.Get, route), next);
            Assert.Equal(401, next.Status);
            await AssertLogSliceHoldsNoSecretAsync(world, cursor);
        }
    }

    /// <summary>
    /// 1.9-REV-002 / V-3 - Membership removed. Session: the boundary 404 and a <c>CrossSpace</c> row,
    /// not <c>401</c> - the Session is still valid for the Account. Token: <c>401</c>, the row gone by
    /// cascade.
    /// </summary>
    [Fact(Skip = "red: AC9 - the two-surface harness (Task 3) and the revocation rows (Task 7) do not exist yet")]
    public async Task Membership_removal_is_reflected_on_the_very_next_request_on_both_surfaces()
    {
        foreach (var surface in RevocationWorld.Surfaces)
        {
            await using var world = await RevocationWorld.StartAsync();
            var route = SpaceRoutes.ActiveOf(world.SpaceId);
            var cursor = world.LogCursor();

            Assert.Equal(200, (await world.SendAsync(surface, HttpMethod.Get, route)).Status);

            await world.RemoveMembershipAsync(commit: true);

            var next = await world.SendAsync(surface, HttpMethod.Get, route);

            if (surface == RevocationSurface.Session)
            {
                AssertSameBytes(await world.SendAsync(surface, HttpMethod.Get, SpaceRoutes.ActiveOf(Guid.NewGuid())), next);
                Assert.Equal(404, next.Status);
                Assert.Equal(204, (await world.SendAsync(surface, HttpMethod.Get, SessionRoutes.Current)).Status);
                Assert.Equal(CrossSpace, (await world.RefusalKindsAsync())[^1]);
            }
            else
            {
                AssertSameBytes(await world.SendWithoutCredentialAsync(HttpMethod.Get, route), next);
                Assert.Equal(401, next.Status);
            }

            await AssertLogSliceHoldsNoSecretAsync(world, cursor);
        }
    }

    /// <summary>
    /// 1.9-REV-003 / V-3 - Space deleted. Session: the boundary 404, the Session still valid. Token:
    /// <c>401</c>, the row gone by the cascade through Membership.
    /// </summary>
    [Fact(Skip = "red: AC9 - the two-surface harness (Task 3) and the revocation rows (Task 7) do not exist yet")]
    public async Task Space_deletion_is_reflected_on_the_very_next_request_on_both_surfaces()
    {
        foreach (var surface in RevocationWorld.Surfaces)
        {
            await using var world = await RevocationWorld.StartAsync();
            var route = SpaceRoutes.ActiveOf(world.SpaceId);
            var cursor = world.LogCursor();

            Assert.Equal(200, (await world.SendAsync(surface, HttpMethod.Get, route)).Status);

            await world.DeleteSpaceAsync();

            var next = await world.SendAsync(surface, HttpMethod.Get, route);

            Assert.Equal(surface == RevocationSurface.Session ? 404 : 401, next.Status);
            AssertSameBytes(
                surface == RevocationSurface.Session
                    ? await world.SendAsync(surface, HttpMethod.Get, SpaceRoutes.ActiveOf(Guid.NewGuid()))
                    : await world.SendWithoutCredentialAsync(HttpMethod.Get, route),
                next);
            await AssertLogSliceHoldsNoSecretAsync(world, cursor);
        }
    }

    /// <summary>
    /// 1.9-REV-004 / V-3 - Account deleted (Memberships first, then the Account, one transaction): both
    /// surfaces <c>401</c>, the Session because <c>SessionRevocation</c> rejects a missing Account.
    /// </summary>
    [Fact(Skip = "red: AC9 - the two-surface harness (Task 3) and the revocation rows (Task 7) do not exist yet")]
    public async Task Account_deletion_is_reflected_on_the_very_next_request_on_both_surfaces()
    {
        foreach (var surface in RevocationWorld.Surfaces)
        {
            await using var world = await RevocationWorld.StartAsync();
            var route = SpaceRoutes.ActiveOf(world.SpaceId);
            var cursor = world.LogCursor();

            Assert.Equal(200, (await world.SendAsync(surface, HttpMethod.Get, route)).Status);

            await world.DeleteAccountAsync();

            var next = await world.SendAsync(surface, HttpMethod.Get, route);

            AssertSameBytes(await world.SendWithoutCredentialAsync(HttpMethod.Get, route), next);
            Assert.Equal(401, next.Status);
            await AssertLogSliceHoldsNoSecretAsync(world, cursor);
        }
    }

    /// <summary>
    /// 1.9-REV-005 / I-10 / NFR-2 - Role changed: the very next request on each surface is served at
    /// the new Role, read per request with nothing cached.
    /// </summary>
    [Fact(Skip = "red: AC9 - the two-surface harness (Task 3) and the revocation rows (Task 7) do not exist yet")]
    [Trait("Requirement", "FR-36")]
    public async Task A_Role_change_is_served_on_the_very_next_request_on_both_surfaces()
    {
        foreach (var surface in RevocationWorld.Surfaces)
        {
            await using var world = await RevocationWorld.StartAsync();
            var route = SpaceRoutes.ActiveOf(world.SpaceId);
            var cursor = world.LogCursor();

            Assert.Equal("owner", RoleOf(await world.SendAsync(surface, HttpMethod.Get, route)));

            await world.ChangeRoleAsync("viewer");

            Assert.Equal("viewer", RoleOf(await world.SendAsync(surface, HttpMethod.Get, route)));
            await AssertLogSliceHoldsNoSecretAsync(world, cursor);
        }
    }

    /// <summary>
    /// 1.9-REV-006 - negative anchor: a Membership removal rolled back leaves both credentials working,
    /// so REV-002's refusals are the removal and not the act of trying.
    /// </summary>
    [Fact(Skip = "red: AC9 - the two-surface harness (Task 3) and the revocation rows (Task 7) do not exist yet")]
    public async Task A_rolled_back_Membership_removal_leaves_both_credentials_working()
    {
        await using var world = await RevocationWorld.StartAsync();
        var route = SpaceRoutes.ActiveOf(world.SpaceId);

        await world.RemoveMembershipAsync(commit: false);

        foreach (var surface in RevocationWorld.Surfaces)
        {
            Assert.Equal(200, (await world.SendAsync(surface, HttpMethod.Get, route)).Status);
        }
    }

    /// <summary>
    /// 1.9-REV-007 / story 1.8 Q11 - negative anchor: signing out of the Session leaves the Token
    /// working. Anchored on the replayed cookie being <c>401</c>, so the sign-out took effect.
    /// </summary>
    [Fact(Skip = "red: AC9 - the two-surface harness (Task 3) and the revocation rows (Task 7) do not exist yet")]
    [Trait("Priority", "P1")]
    public async Task Signing_out_leaves_the_Token_working()
    {
        await using var world = await RevocationWorld.StartAsync();
        var route = SpaceRoutes.ActiveOf(world.SpaceId);

        Assert.Equal(204, (await world.SignOutAsync()).Status);
        Assert.Equal(401, (await world.SendAsync(RevocationSurface.Session, HttpMethod.Get, route)).Status);
        Assert.Equal(200, (await world.SendAsync(RevocationSurface.ApiToken, HttpMethod.Get, route)).Status);
    }

    /// <summary>
    /// 1.9-REV-008 / Lee's decision 2 - a Token on <c>DELETE /api/v1/sessions</c> is no credential:
    /// <c>400 antiforgery-token-missing</c>, byte-identical to no credential, and nothing is stamped.
    /// Anchored on a real sign-out stamping.
    /// </summary>
    [Fact(Skip = "red: AC9 - the two-surface harness (Task 3) and the revocation rows (Task 7) do not exist yet")]
    [Trait("Requirement", "AR-14")]
    public async Task A_Token_on_sign_out_is_no_credential_and_stamps_nothing()
    {
        await using var world = await RevocationWorld.StartAsync();

        var withToken = await world.SignOutWithTokenAsync();

        AssertSameBytes(await world.SignOutWithoutCredentialAsync(), withToken);
        Assert.Equal(400, withToken.Status);
        Assert.Contains(ProblemTypes.AntiforgeryTokenMissing, withToken.Body, StringComparison.Ordinal);
        Assert.Null(await world.SessionsValidAfterAsync());

        Assert.Equal(204, (await world.SignOutAsync()).Status);
        Assert.NotNull(await world.SessionsValidAfterAsync());
    }

    private static async Task AssertLogSliceHoldsNoSecretAsync(IRevocationWorld world, long cursor)
    {
        var slice = await world.LogSinceAsync(cursor);

        Assert.False(string.IsNullOrWhiteSpace(slice), "The case's log slice is empty, so a secret check over it proves nothing.");
        Assert.NotEmpty(world.Secrets);

        var leaked = world.Secrets.Where(secret => slice.Contains(secret, StringComparison.Ordinal)).ToList();

        Assert.True(leaked.Count == 0, $"The Host logged {leaked.Count} secret(s) for this case (AC10, spine :274).");
    }

    private static void AssertSameBytes(IRevocationAnswer expected, IRevocationAnswer actual)
    {
        Assert.Equal(expected.Status, actual.Status);
        Assert.Equal(expected.ContentType, actual.ContentType);
        Assert.Equal(expected.Body, actual.Body);
        Assert.Equal(expected.HeaderNames.Order(StringComparer.OrdinalIgnoreCase), actual.HeaderNames.Order(StringComparer.OrdinalIgnoreCase));
    }

    private static string? RoleOf(IRevocationAnswer answer)
    {
        Assert.Equal(200, answer.Status);

        using var document = JsonDocument.Parse(answer.Body);

        return document.RootElement.GetProperty("role").GetString();
    }
}
