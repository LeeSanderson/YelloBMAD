using System.Diagnostics;
using System.Net;
using Xunit;
using Yello.Tests.Shared;
using RemovalWire = Yello.Tests.Slices.Projects.StatusRemovalWire;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;
using Wire = Yello.Tests.Slices.Projects.ProjectStatusSliceWire;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// The removal at the Tasks-per-Project bound: 5,000 Tasks migrated by one request, in one transaction.
/// </summary>
/// <remarks>
/// <para>
/// <b>A tiered case</b>: minutes of seeding and migration on every PR run would buy nothing the 3-Task cases do not, so it
/// runs on the nightly stage only and is <c>Explicit</c>, alone in its file because every case in a file with a <c>Tier</c>
/// trait must be. No duration is asserted: no requirement states one for this removal. The duration is written to the test
/// output for the record.
/// </para>
/// <para>
/// <b>Seeded by one set-based statement</b> (<see cref="TaskWire.SeedTasksAsync"/>, whose remarks list the constraints it
/// satisfies): 5,000 requests would be the slow part of the case and prove nothing about the removal.
/// </para>
/// </remarks>
public sealed partial class ProjectEndpointTests
{
    /// <summary>
    /// Removing a Status that holds 5,000 Tasks to another answers <c>200</c>; every Task holds the destination, their keys
    /// are distinct, the removed Status has left the set, and no Task is outside it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Explicit = true)]
    [Trait("Tier", "Nightly")]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "FR-26")]
    public async Task A_removal_migrating_five_thousand_Tasks_commits_as_one_transaction()
    {
        const int bound = 5_000;
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        var destination = Guid.Empty;
        WireResponse? removed = null;
        var elapsed = TimeSpan.Zero;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            projectId = IdOf(await CreateAsync(owner, "At the bound"));
            destination = defaults[1].Id;
            await TaskWire.SeedTasksAsync(database, owner.SpaceId, projectId, defaults[0].Id, bound);

            var clock = Stopwatch.StartNew();

            removed = await RemovalWire.RemoveAsync(AsTaskActor(owner), owner.SpaceId, projectId, defaults[0].Id, destination);
            elapsed = clock.Elapsed;
        });

        TestContext.Current.TestOutputHelper?.WriteLine($"Removal migrating {bound} Tasks: {elapsed.TotalMilliseconds:F0} ms");

        Assert.NotNull(owner);
        Assert.True(removed?.StatusCode == HttpStatusCode.OK, $"{removed?.StatusCode} {removed?.Body}");

        var rows = (await TaskWire.TaskRowsAsync(database, owner.SpaceId)).Where(row => row.ProjectId == projectId).ToList();

        Assert.Equal(bound, rows.Count);
        Assert.All(rows, row => Assert.Equal(destination, row.StatusId));
        Assert.Equal(bound, rows.Select(row => row.PositionKey).Distinct(StringComparer.Ordinal).Count());
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }
}
