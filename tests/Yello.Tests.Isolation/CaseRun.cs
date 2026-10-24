using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Isolation;

/// <summary>
/// Runs one row of <see cref="IsolationCases"/> on one surface: the presence anchor, the row's
/// expectation for that surface, and the two invariants every row carries (I-13 inside each
/// expectation, I-17 here).
/// </summary>
internal static class CaseRun
{
    /// <summary>
    /// Runs <paramref name="case"/> on <paramref name="surface"/> against <paramref name="world"/>.
    /// </summary>
    /// <param name="world">The world.</param>
    /// <param name="case">The row.</param>
    /// <param name="surface">The surface.</param>
    /// <returns>A task.</returns>
    public static async Task RunAsync(IsolationWorld world, IsolationCase @case, IsolationSurface surface)
    {
        Assert.Null(@case.PendingUntil);

        // A row with no expectation for this surface is a gap (AC1); the gap gate names it, and so does
        // this, rather than throwing a bare KeyNotFoundException.
        Assert.True(@case.Expectations.TryGetValue(surface, out var expectation), $"{@case.Id} has no expectation for {surface}: a gap, not a choice.");

        var request = await @case.Prepare(world);
        var cursor = await world.Host.LogCursorAsync();

        // Every absence is anchored on a presence: the actor reads its own Space on this surface first,
        // so a refusal below cannot be a credential that never worked.
        await AssertReadsOwnSpaceAsync(request.Actor, surface);

        await expectation.VerifyAsync(world, request, surface);

        await AssertLogSliceHoldsNoSecretAsync(world, cursor, expectation is BoundaryNotFound ? request.TargetSpaceId : null);
    }

    /// <summary>
    /// I-17 / AC10: the case's log slice holds none of the world's secrets. Anchored: the slice holds the
    /// Host's own request-finished lines for this case and, for a boundary refusal, the Host's refusal
    /// line naming the Space - so an empty or misplaced slice cannot pass.
    /// </summary>
    /// <param name="world">The world.</param>
    /// <param name="cursor">Where the case's slice starts.</param>
    /// <param name="refusedSpace">The Space a boundary refusal names, when there is one.</param>
    /// <returns>A task.</returns>
    public static async Task AssertLogSliceHoldsNoSecretAsync(IsolationWorld world, HostLogCursor cursor, Guid? refusedSpace)
    {
        var slice = await world.Host.LogSinceAsync(cursor);

        Assert.Contains("Request finished", slice, StringComparison.Ordinal);

        if (refusedSpace is { } space)
        {
            Assert.Contains($"could not be resolved for {space:D}", slice, StringComparison.OrdinalIgnoreCase);
        }

        Assert.NotEmpty(world.Secrets);

        // Counted, never printed: a failure message that quoted the secret would be the leak.
        var leaked = world.Secrets.Count(secret => slice.Contains(secret, StringComparison.Ordinal));

        Assert.True(leaked == 0, $"The Host logged {leaked} secret(s) for this case (AC10, spine :274).");
    }

    private static async Task AssertReadsOwnSpaceAsync(ProductActor actor, IsolationSurface surface)
    {
        var own = await actor.SendAsync(surface, HttpMethod.Get, SpaceRoutes.ActiveOf(actor.SpaceId));

        Assert.True(own.StatusCode == HttpStatusCode.OK, $"{actor.DisplayName} could not read its own Space on {surface}: {own.StatusCode} {own.Body}");

        using var document = JsonDocument.Parse(own.Body);

        Assert.Equal(actor.SpaceName, document.RootElement.GetProperty("name").GetString());
    }
}
