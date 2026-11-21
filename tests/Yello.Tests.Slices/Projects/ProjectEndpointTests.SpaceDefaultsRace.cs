using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;
using DeleteWire = Yello.Tests.Slices.Tasks.DeleteWire;
using LabelWire = Yello.Tests.Slices.Tasks.LabelWire;
using RemovalWire = Yello.Tests.Slices.Projects.StatusRemovalWire;
using Space = Yello.Tests.Slices.Projects.SpaceDefaultStatusWire;
using StatusRow = Yello.Tests.Slices.Tasks.StatusRow;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;
using Wire = Yello.Tests.Slices.Projects.ProjectStatusSliceWire;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// A Space-level change of the defaults against a Project's own Status write. A Project checks a name against its own set under
/// its own lock; a Space write changes a name in every Project's set at once. Without one lock both pass their own check
/// and commit a set holding one name twice, or a Space rename misses a Project rename committed beside it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The lock is <c>Yello/StatusDefaults/{spaceId}</c></b>: Exclusive for a Space write, Shared for every Project delta write,
/// taken before the Project's own lock. The test holds it (<see cref="SpaceDefaultStatusWire.HoldStatusDefaultsAsync"/>),
/// sends the requests one at a time and waits until each is blocked on <c>LCK_M_%</c> before sending the next, so the order
/// they are granted in is the order they queued in. Commits then release them; nothing is fired together and hoped for.
/// </para>
/// <para>
/// <b>A writer that skips the lock is caught by the wait, not by the answer</b>: it answers before it is ever blocked.
/// </para>
/// </remarks>
public sealed partial class ProjectEndpointTests
{
    /// <summary>
    /// With the Status-defaults lock held, a Space rename of the second default to "Shipped" and a Project add of "shipped"
    /// queue in the given order; released, the first is admitted and the second refused <c>409 status-name-taken</c> - the
    /// Space rename naming the Project when the add went first - and the Project's set never holds the name twice.
    /// </summary>
    /// <param name="spaceFirst">Whether the Space rename queues before the Project add.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "AD-16")]
    [Trait("Requirement", "AD-17")]
    public async Task A_Space_rename_and_a_Project_add_of_one_name_queue_on_the_Status_lock_and_no_set_holds_it_twice(bool spaceFirst)
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var projectId = Guid.Empty;
        IReadOnlyList<StatusRow> defaults = [];
        WireResponse? spaceRename = null;
        WireResponse? projectAdd = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            projectId = IdOf(await CreateAsync(owner, "Contended by name"));
            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);

            // Present in the Space before the race, so both requests meet on the Status lock and nowhere earlier.
            Assert.Equal(HttpStatusCode.OK, (await Wire.ReadAsync(actor, owner.SpaceId, projectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await Space.HoldStatusDefaultsAsync(holder, held, Space.Exclusive, cancellationToken);

            Task<WireResponse> renaming;
            Task<WireResponse> adding;

            if (spaceFirst)
            {
                renaming = Space.RenameAsync(actor, owner.SpaceId, defaults[1].Id, "Shipped");
                await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, renaming, cancellationToken);
                adding = Wire.AddAsync(actor, owner.SpaceId, projectId, "shipped");
            }
            else
            {
                adding = Wire.AddAsync(actor, owner.SpaceId, projectId, "shipped");
                await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, adding, cancellationToken);
                renaming = Space.RenameAsync(actor, owner.SpaceId, defaults[1].Id, "Shipped");
            }

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(renaming, adding), cancellationToken);
            await held.CommitAsync(cancellationToken);

            spaceRename = await renaming;
            projectAdd = await adding;

            await Space.AssertNoSetHoldsANameTwiceAsync(actor, owner.SpaceId, [projectId]);
        });

        Assert.NotNull(owner);

        var after = await Wire.DefaultsAsync(database, owner.SpaceId);

        if (spaceFirst)
        {
            Assert.True(spaceRename?.StatusCode == HttpStatusCode.OK, $"{spaceRename?.StatusCode} {spaceRename?.Body}");
            RemovalWire.AssertRefused(projectAdd, HttpStatusCode.Conflict, ProblemTypes.StatusNameTaken);
            Assert.Equal("Shipped", after[1].Name);
            Assert.DoesNotContain(await Wire.DefinitionsAsync(database, owner.SpaceId), row => row.OwnerProjectId == projectId);
        }
        else
        {
            Assert.True(projectAdd?.StatusCode == HttpStatusCode.Created, $"{projectAdd?.StatusCode} {projectAdd?.Body}");
            RemovalWire.AssertRefused(spaceRename, HttpStatusCode.Conflict, ProblemTypes.StatusNameTaken);
            Assert.Equal([projectId], SpaceStatusWire.ProjectsOf(spaceRename!.Body).Select(project => project.Id));
            Assert.Equal(defaults.Select(row => (row.Id, row.Name)), after.Select(row => (row.Id, row.Name)));
        }

        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With the Status-defaults lock held, a Space add of "Review" and a Project add of "review" both queue; released, exactly
    /// one is admitted (<c>201</c>) and the other is <c>409 status-name-taken</c>; the Space holds one definition of that
    /// name and the Project's set holds it once.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-24")]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "AD-17")]
    public async Task A_Space_add_and_a_Project_add_of_one_name_queue_on_the_Status_lock_and_exactly_one_is_admitted()
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

            projectId = IdOf(await CreateAsync(owner, "Added twice"));
            Assert.Equal(HttpStatusCode.OK, (await Wire.ReadAsync(actor, owner.SpaceId, projectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await Space.HoldStatusDefaultsAsync(holder, held, Space.Exclusive, cancellationToken);

            var racing = Task.WhenAll(
                Space.AddAsync(actor, owner.SpaceId, "Review"),
                Wire.AddAsync(actor, owner.SpaceId, projectId, "review"));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answers = await racing;

            await Space.AssertNoSetHoldsANameTwiceAsync(actor, owner.SpaceId, [projectId]);
        });

        Assert.NotNull(owner);
        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], answers.Select(answer => answer.StatusCode).Order());
        RemovalWire.AssertRefused(answers.Single(answer => answer.StatusCode == HttpStatusCode.Conflict), HttpStatusCode.Conflict, ProblemTypes.StatusNameTaken);
        Assert.Single(await Wire.DefinitionsAsync(database, owner.SpaceId), row => string.Equals(row.Name, "Review", StringComparison.OrdinalIgnoreCase));
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With the Status-defaults lock held, a Project rename of the first default to "Live" and a Space rename of it to
    /// "Shipped" with no decision queue in the given order. Space first: both are admitted and the Project holds "Live" over
    /// "Shipped". Project first: the Space rename is <c>409 status-rename-conflict</c> naming that Project, its name "Live" and
    /// its one Task, and the default keeps its name. Never a Space rename that missed the Project's committed op.
    /// </summary>
    /// <param name="spaceFirst">Whether the Space rename queues before the Project rename.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "UX-DR16")]
    [Trait("Requirement", "AD-16")]
    public async Task A_Project_rename_and_a_Space_rename_of_one_default_queue_and_the_outcome_is_consistent(bool spaceFirst)
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var projectId = Guid.Empty;
        IReadOnlyList<StatusRow> defaults = [];
        WireResponse? spaceRename = null;
        WireResponse? projectRename = null;
        IReadOnlyList<ProjectStatusEntry> set = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);

            projectId = IdOf(await CreateAsync(owner, "Renamed beside the Space"));
            defaults = await Wire.DefaultsAsync(database, owner.SpaceId);
            _ = TaskWire.IdOf(await TaskWire.CreateTaskAsync(actor, owner.SpaceId, projectId, "Held in the first"));

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await Space.HoldStatusDefaultsAsync(holder, held, Space.Exclusive, cancellationToken);

            Task<WireResponse> spaceSide;
            Task<WireResponse> projectSide;

            if (spaceFirst)
            {
                spaceSide = Space.RenameAsync(actor, owner.SpaceId, defaults[0].Id, "Shipped");
                await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, spaceSide, cancellationToken);
                projectSide = Wire.RenameAsync(actor, owner.SpaceId, projectId, defaults[0].Id, "Live");
            }
            else
            {
                projectSide = Wire.RenameAsync(actor, owner.SpaceId, projectId, defaults[0].Id, "Live");
                await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, projectSide, cancellationToken);
                spaceSide = Space.RenameAsync(actor, owner.SpaceId, defaults[0].Id, "Shipped");
            }

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, Task.WhenAll(spaceSide, projectSide), cancellationToken);
            await held.CommitAsync(cancellationToken);

            spaceRename = await spaceSide;
            projectRename = await projectSide;
            set = await Wire.EffectiveAsync(actor, owner.SpaceId, projectId);
        });

        Assert.NotNull(owner);
        Assert.True(projectRename?.StatusCode == HttpStatusCode.OK, $"{projectRename?.StatusCode} {projectRename?.Body}");

        var first = set.Single(entry => entry.Id == defaults[0].Id);
        var after = await Wire.DefaultsAsync(database, owner.SpaceId);

        if (spaceFirst)
        {
            Assert.True(spaceRename?.StatusCode == HttpStatusCode.OK, $"{spaceRename?.StatusCode} {spaceRename?.Body}");
            Assert.Equal(("Live", "Shipped"), (first.Name, first.DefaultName));
            Assert.Equal("Shipped", after[0].Name);
        }
        else
        {
            RemovalWire.AssertRefused(spaceRename, HttpStatusCode.Conflict, ProblemTypes.StatusRenameConflict);

            var conflict = Assert.Single(SpaceStatusWire.ConflictsOf(spaceRename!.Body));

            Assert.Equal((projectId, "Renamed beside the Space", "Live", 1), (conflict.ProjectId, conflict.ProjectName, conflict.Name, conflict.TaskCount));
            Assert.Equal(defaults[0].Name, after[0].Name);
            Assert.Equal(("Live", defaults[0].Name), (first.Name, first.DefaultName));
        }

        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }

    /// <summary>
    /// With the test playing one Project's delta write - the Status-defaults lock Shared, then that Project's lock - a delta
    /// write in a second Project answers <c>201</c> without waiting, while a Space add queues behind the held Shared lock and is
    /// admitted once it is released. A Project write that took the lock Exclusive would wait here.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-27")]
    [Trait("Requirement", "AD-17")]
    public async Task Two_Project_delta_writes_do_not_wait_on_each_other_while_a_Space_write_waits_on_both()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var answeredWhileHeld = false;
        WireResponse? otherProject = null;
        WireResponse? spaceAdd = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var actor = AsTaskActor(owner);
            var playedProject = IdOf(await CreateAsync(owner, "Held by the test"));
            var other = IdOf(await CreateAsync(owner, "Written beside it"));

            Assert.Equal(HttpStatusCode.OK, (await Wire.ReadAsync(actor, owner.SpaceId, other)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await Space.HoldStatusDefaultsAsync(holder, held, Space.Shared, cancellationToken);
            await DeleteWire.HoldProjectRangeAsync(holder, held, playedProject, cancellationToken);

            var writing = Wire.AddAsync(actor, owner.SpaceId, other, "Parked");

            await LabelWire.WaitUntilBlockedOrAnsweredAsync(observer, 1, writing, cancellationToken);
            answeredWhileHeld = writing.IsCompleted;

            var adding = Space.AddAsync(actor, owner.SpaceId, "Archived");

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, answeredWhileHeld ? 1 : 2, adding, cancellationToken);
            await held.CommitAsync(cancellationToken);

            otherProject = await writing;
            spaceAdd = await adding;
        });

        Assert.NotNull(owner);
        Assert.True(answeredWhileHeld, "A Project delta write waited on another Project's write: the Status-defaults lock is not Shared for Project writes.");
        Assert.True(otherProject?.StatusCode == HttpStatusCode.Created, $"{otherProject?.StatusCode} {otherProject?.Body}");
        Assert.True(spaceAdd?.StatusCode == HttpStatusCode.Created, $"{spaceAdd?.StatusCode} {spaceAdd?.Body}");
        Assert.Contains(await Wire.DefaultsAsync(database, owner.SpaceId), row => row.Name == "Archived");
        await Wire.AssertNoTaskOutsideItsProjectsSetAsync(database, owner.SpaceId);
    }
}
