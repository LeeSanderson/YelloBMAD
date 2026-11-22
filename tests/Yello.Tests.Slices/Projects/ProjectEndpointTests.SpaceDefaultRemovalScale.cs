using System.Diagnostics;
using System.Net;
using Xunit;
using Yello.Tests.Shared;
using RemovalWire = Yello.Tests.Slices.Projects.StatusRemovalWire;
using Space = Yello.Tests.Slices.Projects.SpaceDefaultStatusWire;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;
using Wire = Yello.Tests.Slices.Projects.ProjectStatusSliceWire;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// The Space-level removal over the bound envelope: 50 Projects, each holding the removed default with Tasks in it, some
/// blocked and given an exception, migrated by one request in one transaction.
/// </summary>
/// <remarks>
/// <para>
/// <b>A tiered case</b>, alone in its file because every case in a file with a <c>Tier</c> trait must be <c>Explicit</c>. No
/// duration is asserted: none is stated for this removal, and the measurement is recorded for Lee against the write p95 and
/// the ingress timeout. The duration is written to the test output.
/// </para>
/// <para>
/// <b>The seed size is the develop phase's to settle</b>: the full envelope is 50 x 5,000 Tasks. <see cref="TasksPerProject"/>
/// starts at the full bound; if that does not fit the 2GB engine, the largest that does replaces it and is recorded in the
/// Debug Log. Tasks are seeded by one set-based statement per Project (<see cref="TaskWire.SeedTasksAsync"/>, whose remarks
/// list the constraints it satisfies); every Project, removal and added Status is written by the product.
/// </para>
/// </remarks>
public sealed partial class ProjectEndpointTests
{
    private const int ProjectsAtTheBound = 50;

    private const int TasksPerProject = 5_000;

    private const int BlockedEvery = 10;

    /// <summary>
    /// Removing the first default across 50 occupied Projects - every tenth one having removed the second default and given
    /// its own added Status as exception - answers <c>200</c>; every Task of a plain Project holds the second default, every
    /// Task of a blocked one holds its exception, keys stay distinct per column, and no Task is outside its set.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Explicit = true, Skip = "red: AC16 - the Space-level removal does not exist yet (Q15: measured, not asserted)")]
    [Trait("Tier", "Nightly")]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-27")]
    public async Task A_Space_removal_across_fifty_occupied_Projects_commits_as_one_transaction()
    {
        var database = Available();
        Actor? owner = null;
        var destination = Guid.Empty;
        var exceptions = new List<(Guid ProjectId, Guid DestinationStatusId)>();
        WireResponse? removed = null;
        var elapsed = TimeSpan.Zero;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            destination = defaults[1].Id;

            for (var index = 0; index < ProjectsAtTheBound; index++)
            {
                var projectId = IdOf(await CreateAsync(owner, $"At the bound {index:D2}"));

                if (index % BlockedEvery == 0)
                {
                    _ = await RemovalWire.RemovedAsync(actor, owner.SpaceId, projectId, defaults[1].Id);
                    exceptions.Add((projectId, await Wire.AddedAsync(actor, owner.SpaceId, projectId, "Parked")));
                }

                await TaskWire.SeedTasksAsync(database, owner.SpaceId, projectId, defaults[0].Id, TasksPerProject);
            }

            var clock = Stopwatch.StartNew();

            removed = await Space.RemoveAsync(actor, owner.SpaceId, defaults[0].Id, destination, exceptions);
            elapsed = clock.Elapsed;
        });

        TestContext.Current.TestOutputHelper?.WriteLine($"Space removal migrating {ProjectsAtTheBound} x {TasksPerProject} Tasks: {elapsed.TotalMilliseconds:F0} ms");

        Assert.NotNull(owner);
        Assert.True(removed?.StatusCode == HttpStatusCode.OK, $"{removed?.StatusCode} {removed?.Body}");

        var rows = await TaskWire.TaskRowsAsync(database, owner.SpaceId);
        var exceptionOf = exceptions.ToDictionary(exception => exception.ProjectId, exception => exception.DestinationStatusId);

        Assert.Equal(ProjectsAtTheBound * TasksPerProject, rows.Count);
        Assert.All(rows, row => Assert.Equal(exceptionOf.GetValueOrDefault(row.ProjectId, destination), row.StatusId));
        Assert.All(
            rows.GroupBy(row => (row.ProjectId, row.StatusId)),
            column => Assert.Equal(column.Count(), column.Select(row => row.PositionKey).Distinct(StringComparer.Ordinal).Count()));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }
}
