using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Domain;
using Yello.Domain.Bounds;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Story 2.2 AC7 and AC11 over the wire: the Tasks-per-Project bound of 5,000, counted by the pipeline for
/// the route's Project, refused visibly with a machine-readable reason inside the creation's transaction,
/// never by two concurrent creations at 4,999 both passing, and never spilling into another Project.
/// </summary>
/// <remarks>
/// <para>
/// <b>Seeding 4,999 is one set-based statement</b> (<see cref="TaskWire.SeedTasksAsync"/>), never 4,999
/// requests (story Testing requirements).
/// </para>
/// <para>
/// <b>The race case does not depend on the race happening to race</b> (Task 7, story 2.1 review P11). The
/// test takes the bound's own lock first - the Project's Tasks lock, an application lock since story 2.9 - sends both
/// creations, waits until both are blocked on a lock, then lets go. Both queue on the lock; without it both counts would
/// read 4,999, both pass the bound, and the case fails every time - the second creation then meets the first at the
/// column's last key (the same <c>PositionKey.After</c>) and the unique index answers <c>500</c>, or, keys apart, the
/// Project ends at 5,001. It waits on "blocked on any lock", never on one wait type.
/// </para>
/// </remarks>
public sealed partial class TaskEndpointTests
{
    private const string BoundTitle = "The 5,001st";

    /// <summary>
    /// AC7, AR-30, AD-25 - with 4,999 Tasks, the 5,000th is created (the anchor) and the 5,001st is refused
    /// <c>409 bound-reached</c> carrying <c>bound: tasks-per-project</c> and <c>limit: 5000</c>; the Project
    /// still holds 5,000, and the refusal is not an access refusal, so nothing is recorded.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "AD-25")]
    [Trait("Requirement", "NFR-8")]
    public async Task The_5001st_Task_is_refused_409_bound_reached_and_the_Project_still_holds_5000()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse? fiveThousandth = null;
        WireResponse? fiveThousandFirst = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = await CreateProjectAsync(owner, "Full");

            var first = (await StatusesAsync(database, owner.SpaceId))[0];
            await SeedTasksAsync(database, owner.SpaceId, projectId, first.Id, BoundRegistry.For(Bound.TasksPerProject).Limit - 1);

            fiveThousandth = await CreateTaskAsync(owner, owner.SpaceId, projectId, "The 5,000th");
            fiveThousandFirst = await CreateTaskAsync(owner, owner.SpaceId, projectId, BoundTitle);
        });

        Assert.NotNull(owner);
        Assert.True(fiveThousandth?.StatusCode == HttpStatusCode.Created, $"Anchor: {fiveThousandth?.StatusCode} {fiveThousandth?.Body}");
        Assert.True(fiveThousandFirst?.StatusCode == HttpStatusCode.Conflict, $"{fiveThousandFirst?.StatusCode} {fiveThousandFirst?.Body}");

        var problem = Problem(fiveThousandFirst!);

        Assert.Equal(ProblemTypes.BoundReached, problem?.Type);
        Assert.Equal(StableCode.Of(Bound.TasksPerProject), problem?.Bound);
        Assert.Equal(BoundRegistry.For(Bound.TasksPerProject).Limit, problem?.Limit);

        var rows = await TaskRowsAsync(database, owner.SpaceId);

        Assert.Equal(BoundRegistry.For(Bound.TasksPerProject).Limit, rows.Count(row => row.ProjectId == projectId));
        Assert.DoesNotContain(rows, row => row.Title == BoundTitle);
        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
    }

    /// <summary>
    /// AC11, AD-25 - the bound is per Project, not per Space: while one Project holds 5,000, a second Project
    /// in the same Space takes a Task.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "AD-25")]
    public async Task A_full_Project_does_not_block_a_Task_in_another_Project_of_the_same_Space()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? inFull = null;
        WireResponse? inOther = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var full = await CreateProjectAsync(owner, "Full");
            var other = await CreateProjectAsync(owner, "Other");

            var first = (await StatusesAsync(database, owner.SpaceId))[0];
            await SeedTasksAsync(database, owner.SpaceId, full, first.Id, BoundRegistry.For(Bound.TasksPerProject).Limit);

            // Anchor: the full Project really is full, so the second answer is the scope's and not a bound
            // that never counted.
            inFull = await CreateTaskAsync(owner, owner.SpaceId, full, "One too many");
            inOther = await CreateTaskAsync(owner, owner.SpaceId, other, "Elsewhere");
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.Conflict, inFull?.StatusCode);
        Assert.True(inOther?.StatusCode == HttpStatusCode.Created, $"{inOther?.StatusCode} {inOther?.Body}");
    }

    /// <summary>
    /// AC11, Task 5 - a creation naming a Project that matches nothing in the Space counts nothing, passes the
    /// bound and is answered by the slice's <c>404</c>, never the bound's <c>409</c> - even while another
    /// Project in the Space is full.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-25")]
    [Trait("Requirement", "AD-3")]
    public async Task A_creation_in_an_unknown_Project_counts_nothing_and_answers_404_not_409()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? unknown = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var full = await CreateProjectAsync(owner, "Full");

            var first = (await StatusesAsync(database, owner.SpaceId))[0];
            await SeedTasksAsync(database, owner.SpaceId, full, first.Id, BoundRegistry.For(Bound.TasksPerProject).Limit);

            unknown = await CreateTaskAsync(owner, owner.SpaceId, Guid.NewGuid(), "Nowhere");
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.NotFound, unknown?.StatusCode);
        Assert.Equal(ProblemTypes.NotAvailable, Problem(unknown!)?.Type);
    }

    /// <summary>
    /// AC11, Task 7 - two creations sent concurrently at 4,999 yield exactly one <c>201</c> and one
    /// <c>409 bound-reached</c>, and the Project holds 5,000. The interleaving is the test's, not the
    /// scheduler's: see the class remarks.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "AD-25")]
    public async Task Two_concurrent_creations_at_4999_yield_exactly_one_201_and_one_409()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse[] answers = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            // A second member with a client of its own, so the two requests share no WireSession state.
            var member = await RegisterAsync(client, database);
            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            projectId = await CreateProjectAsync(owner, "Racing");
            var first = (await StatusesAsync(database, owner.SpaceId))[0];
            await SeedTasksAsync(database, owner.SpaceId, projectId, first.Id, BoundRegistry.For(Bound.TasksPerProject).Limit - 1);

            // Presence first: a first request's SpacePresence insert would otherwise serialise the pair before
            // the bound count (story 2.2 ATDD finding on 2.1's BND-002).
            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, projectId)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(member, owner.SpaceId, projectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            // The Project's Tasks lock both creations' counts take (story 2.9: an application lock of the Project's own).
            await DeleteWire.HoldProjectRangeAsync(holder, held, projectId, cancellationToken);

            var racing = Task.WhenAll(
                CreateTaskAsync(owner, owner.SpaceId, projectId, "Racer one"),
                CreateTaskAsync(member, owner.SpaceId, projectId, "Racer two"));

            await WaitUntilBlockedOnALockAsync(observer, 2, racing, cancellationToken);

            await held.CommitAsync(cancellationToken);

            answers = await racing;
        });

        Assert.NotNull(owner);
        Assert.Equal(
            [HttpStatusCode.Created, HttpStatusCode.Conflict],
            answers.Select(answer => answer.StatusCode).Order());
        Assert.Equal(ProblemTypes.BoundReached, Problem(answers.Single(answer => answer.StatusCode == HttpStatusCode.Conflict))?.Type);
        Assert.Equal(BoundRegistry.For(Bound.TasksPerProject).Limit, await TaskCountAsync(database, owner.SpaceId, projectId));
    }

    /// <summary>
    /// AC11, AR-25 - the replay comes before the bound (2.1 Task 7's order): replaying the key of the 5,000th
    /// creation, once the Project is full, returns that creation's original <c>201</c>, not the 5,001st's
    /// <c>409</c>, and applies nothing. Anchor: a fresh key is refused.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    public async Task Replaying_the_key_of_the_5000th_creation_returns_its_201_not_the_bound_409()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse? original = null;
        WireResponse? replay = null;
        WireResponse? fresh = null;
        var key = Guid.NewGuid().ToString("D");

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = await CreateProjectAsync(owner, "Full");

            var first = (await StatusesAsync(database, owner.SpaceId))[0];
            await SeedTasksAsync(database, owner.SpaceId, projectId, first.Id, BoundRegistry.For(Bound.TasksPerProject).Limit - 1);

            original = await CreateTaskAsync(owner, owner.SpaceId, projectId, "The 5,000th", key);
            replay = await CreateTaskAsync(owner, owner.SpaceId, projectId, "The 5,000th", key);
            fresh = await CreateTaskAsync(owner, owner.SpaceId, projectId, "The 5,000th", Guid.NewGuid().ToString("D"));
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.Created, original?.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replay?.StatusCode);
        Assert.Equal(original!.Body, replay!.Body);
        Assert.Equal(original.Header("Location"), replay.Header("Location"));
        Assert.Equal(HttpStatusCode.Conflict, fresh?.StatusCode);
        Assert.Equal(BoundRegistry.For(Bound.TasksPerProject).Limit, await TaskCountAsync(database, owner.SpaceId, projectId));
        Assert.DoesNotContain(CrossSpaceKind, await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
    }
}
