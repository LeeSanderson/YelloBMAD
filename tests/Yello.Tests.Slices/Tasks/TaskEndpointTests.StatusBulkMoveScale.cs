using System.Diagnostics;
using Xunit;
using Yello.Domain.Bounds;
using Yello.Domain.Tasks;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.ProjectMoveSliceWire;
using static Yello.Tests.Slices.Tasks.StatusBulkMoveSliceWire;
using static Yello.Tests.Slices.Tasks.TaskWire;
using StatusWire = Yello.Tests.Slices.Projects.ProjectStatusSliceWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// The bulk move at the Tasks-per-Project bound: a full source column of 5,000 Tasks moved into an empty Project by one
/// request in one transaction.
/// </summary>
/// <remarks>
/// <para>
/// <b>A tiered case</b>, alone in its file because every case in a file with a <c>Tier</c> trait must be <c>Explicit</c>. No
/// duration is asserted: none is stated for a bulk move, and the measurement is recorded for Lee against the write p95 and
/// the ingress timeout. The duration is written to the test output.
/// </para>
/// <para>
/// <b>One 5,000 seed</b>, written by one set-based statement (<see cref="TaskWire.SeedTasksAsync"/>, whose remarks list the
/// constraints it satisfies); both Projects are written by the product.
/// </para>
/// </remarks>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// Moving a Status holding 5,000 Tasks into an empty Project answers <c>200</c> with 5,000 moved; the destination column
    /// holds them in their source order, every key distinct and within the key's maximum length, and no Task is outside its
    /// Project's set.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Explicit = true)]
    [Trait("Tier", "Nightly")]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "AD-15")]
    [Trait("Requirement", "AD-17")]
    public async Task A_Status_of_5000_Tasks_moves_in_one_request_in_source_order()
    {
        var database = Available();
        var limit = BoundRegistry.For(Bound.TasksPerProject).Limit;
        Actor? owner = null;
        TwoProjects? projects = null;
        IReadOnlyList<StatusRow> defaults = [];
        IReadOnlyList<Guid> sourceOrder = [];
        WireResponse? moved = null;
        var elapsed = TimeSpan.Zero;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projects = await TwoProjectsAsync(owner, [], []);
            defaults = await StatusWire.DefaultsAsync(database, owner.SpaceId);

            await SeedTasksAsync(database, owner.SpaceId, projects.SourceId, defaults[0].Id, limit);

            sourceOrder = await MoveWire.ColumnAsync(database, owner.SpaceId, projects.SourceId, defaults[0].Id);

            var clock = Stopwatch.StartNew();

            moved = await BulkMoveAsync(owner, owner.SpaceId, projects.SourceId, defaults[0].Id, ProjectMoveWire.Body(projects.DestinationId));
            elapsed = clock.Elapsed;
        });

        TestContext.Current.TestOutputHelper?.WriteLine($"Bulk move of {limit} Tasks: {elapsed.TotalMilliseconds:F0} ms");

        Assert.NotNull(owner);
        Assert.NotNull(projects);
        Assert.Equal(limit, sourceOrder.Count);
        AssertBulkMoved(moved, projects.DestinationId, defaults[0].Id, limit);
        Assert.Equal(sourceOrder, await MoveWire.ColumnAsync(database, owner.SpaceId, projects.DestinationId, defaults[0].Id));

        var keys = (await TaskRowsAsync(database, owner.SpaceId)).Where(row => row.ProjectId == projects.DestinationId).Select(row => row.PositionKey).ToList();

        Assert.Equal(limit, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, key => Assert.True(key.Length <= PositionKey.MaxLength, $"A key of {key.Length} characters exceeds {PositionKey.MaxLength}."));
        await StatusWire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }
}
