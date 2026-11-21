using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;
using DeleteWire = Yello.Tests.Slices.Tasks.DeleteWire;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;
using Wire = Yello.Tests.Slices.Projects.ProjectStatusSliceWire;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// Two adds of one name racing on one Project. The effective set's name uniqueness cannot be a database constraint - a
/// renamed default's name lives in the delta - so it is checked under the Project's lock, and the race is controlled on that
/// lock, never fired and hoped for.
/// </summary>
/// <remarks>
/// Open question 4 (a): delta writes take <c>Yello/ProjectTasks/{spaceId}/{projectId}</c> exclusively, the lock every key
/// placer of the Project's Tasks already takes. The test holds it (<see cref="DeleteWire.HoldProjectRangeAsync"/>), sends both
/// adds, waits until both are blocked on <c>LCK_M_%</c>, then commits. A write that skips the lock lets both read a set
/// without "Parked" and both insert it, under two different ids that no unique index refuses.
/// </remarks>
public sealed partial class ProjectEndpointTests
{
    /// <summary>
    /// With the Project's lock held, two adds of "Parked" both queue; released, exactly one is admitted (<c>201</c>) and the
    /// other is <c>409 status-name-taken</c>; the Project owns one "Parked" and its delta holds one operation.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-25")]
    [Trait("Requirement", "AR-34")]
    public async Task Two_adds_of_one_name_queue_on_the_Projects_lock_and_the_second_is_refused_409()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse[] answers = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            projectId = IdOf(await CreateAsync(owner, "Contended"));

            // Present in the Space before the race, so the two requests meet on the Project's lock and nowhere earlier.
            Assert.Equal(HttpStatusCode.OK, (await Wire.ReadAsync(actor, owner.SpaceId, projectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await DeleteWire.HoldProjectRangeAsync(holder, held, projectId, cancellationToken);

            var racing = Task.WhenAll(
                Wire.AddAsync(actor, owner.SpaceId, projectId, "Parked"),
                Wire.AddAsync(actor, owner.SpaceId, projectId, "parked"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answers = await racing;
        });

        Assert.NotNull(owner);
        Assert.Equal(
            [HttpStatusCode.Created, HttpStatusCode.Conflict],
            answers.Select(answer => answer.StatusCode).Order());
        Assert.Equal(ProblemTypes.StatusNameTaken, ProjectStatusWire.ProblemTypeOf(answers.Single(answer => answer.StatusCode == HttpStatusCode.Conflict).Body));
        Assert.Single(await Wire.DefinitionsAsync(database, owner.SpaceId), row => row.OwnerProjectId == projectId);
        Assert.Equal(1, await Wire.DeltaCountAsync(database, owner.SpaceId, projectId));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }
}
