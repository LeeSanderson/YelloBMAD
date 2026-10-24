using System.Net;
using Xunit;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Revocation;

/// <summary>
/// SM-2's request-path half on both surfaces: when a credential is ended or narrowed - sign-out, Token
/// revocation, Membership removal, Space deletion, Account deletion, Role change - the very next
/// request on each surface reflects it, with no tolerance. AC9, AC10; V-3, I-10, I-11, I-12; NFR-2.
/// </summary>
/// <remarks>
/// <para>
/// <b>One generated theory over <see cref="RevocationCases"/></b>, <c>cases x surfaces</c>, held by the
/// same both-surfaces gap gate as the isolation list. The story-1.9 ATDD scaffolds REV-001 to REV-008
/// were one fact per event, each looping both surfaces; on activation they became the rows.
/// </para>
/// <para>
/// <b>Every case ends with its log slice checked</b> for every secret the world has held (I-17,
/// AC10), anchored on the Host's own request-finished lines for the case.
/// </para>
/// </remarks>
/// <param name="fixture">The class's world.</param>
[Trait("Suite", "Revocation")]
[Trait("Priority", "P0")]
[Trait("Requirement", "AR-35")]
[Trait("Requirement", "NFR-2")]
[Trait("Requirement", "FR-34")]
[Trait("Requirement", "FR-36")]
[Trait("Requirement", "AD-4")]
public sealed class RevocationCaseTests(RevocationWorldFixture fixture) : IClassFixture<RevocationWorldFixture>
{
    /// <summary>
    /// Every revocation row on every surface.
    /// </summary>
    public static IEnumerable<TheoryDataRow<string, IsolationSurface>> Rows => RevocationCases.CrossProduct;

    /// <summary>
    /// 1.9-REV-000 / AC1 - every revocation case declares what every surface gets; the gate is the
    /// isolation list's, generic over this list.
    /// </summary>
    [Fact]
    public void Every_revocation_case_declares_an_expectation_for_every_surface()
    {
        // Vacuity guard: the five events plus the anchors, and two surfaces.
        Assert.True(RevocationCases.All.Count >= 5, $"Expected at least the five revocation events; found {RevocationCases.All.Count}.");
        Assert.True(Enum.GetValues<IsolationSurface>().Length >= 2);

        Assert.Equal([], SurfaceGaps.Find(RevocationCases.All, @case => @case.Id, @case => @case.Expectations));
    }

    /// <summary>
    /// 1.9-REV-001 to REV-008, as rows: after the row's event, the very next request on the surface
    /// answers as the row says.
    /// </summary>
    /// <param name="caseId">The row.</param>
    /// <param name="surface">The surface.</param>
    /// <returns>A task.</returns>
    [Theory]
    [MemberData(nameof(Rows))]
    public async Task The_very_next_request_on_each_surface_reflects_the_event(string caseId, IsolationSurface surface)
    {
        var world = fixture.World;
        var @case = RevocationCases.Named(caseId);

        Assert.True(@case.Expectations.TryGetValue(surface, out var step), $"{caseId} has no expectation for {surface}: a gap, not a choice.");

        // The cursor comes first, so the slice holds the fresh Account's registration, sign-in and Token
        // issue - the requests that carry the password, the cookie and the plaintext AC10 is about.
        var cursor = await world.Host.LogCursorAsync();
        var context = new RevocationContext(world, await world.NewActorAsync());

        // The anchor: the same credential succeeds on the same request first.
        var before = await context.Actor.SendAsync(surface, HttpMethod.Get, SpaceRoutes.ActiveOf(context.Actor.SpaceId));
        Assert.True(before.StatusCode == HttpStatusCode.OK, $"{caseId} / {surface}: the credential did not work before the event: {before.StatusCode} {before.Body}");

        await step.Event(context);
        await step.Next.VerifyAsync(context, surface);

        await AssertLogSliceHoldsNoSecretAsync(world, cursor);
    }

    /// <summary>
    /// I-17 / AC10: the case's log slice holds no secret any actor in the world has held. Anchored on the
    /// Host's own request-finished lines, so an empty slice cannot pass.
    /// </summary>
    private static async Task AssertLogSliceHoldsNoSecretAsync(RevocationWorld world, HostLogCursor cursor)
    {
        var slice = await world.Host.LogSinceAsync(cursor);

        Assert.Contains("Request finished", slice, StringComparison.Ordinal);
        Assert.NotEmpty(world.Secrets);

        // Counted, never printed: a failure message that quoted the secret would be the leak.
        var leaked = world.Secrets.Count(secret => slice.Contains(secret, StringComparison.Ordinal));

        Assert.True(leaked == 0, $"The Host logged {leaked} secret(s) for this case (AC10, spine :274).");
    }
}
