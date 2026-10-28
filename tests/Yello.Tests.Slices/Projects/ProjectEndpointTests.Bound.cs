using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;

namespace Yello.Tests.Slices.Projects;

/// <summary>
/// Story 2.1 AC5 and AC10 over the wire: the Projects-per-Space bound of 50, refused visibly with a
/// machine-readable reason inside the creation's transaction, and never by two concurrent creations
/// at 49 both passing.
/// </summary>
/// <remarks>
/// The bound is 50 (<c>BoundRegistry</c>, AR-30). AC10's other half - that the pipeline, not the slice,
/// consults it - is an architecture gate (<c>RefusalBoundaryGateTests.Projects.cs</c>), because a wire
/// case cannot tell where a refusal was decided.
/// </remarks>
public sealed partial class ProjectEndpointTests
{
    private const int ProjectsPerSpace = 50;
    private const string ProjectsPerSpaceCode = "projects-per-space";

    /// <summary>
    /// AC5, AR-30, AD-25 - with 49 Projects, the 50th is created (the anchor) and the 51st is refused
    /// <c>409 bound-reached</c> carrying <c>bound: projects-per-space</c> and <c>limit: 50</c>; the Space
    /// still holds 50, and the refusal is not an access refusal, so nothing is recorded.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "AD-25")]
    [Trait("Requirement", "NFR-8")]
    public async Task The_51st_Project_is_refused_409_bound_reached_and_the_Space_still_holds_50()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? fiftieth = null;
        WireResponse? fiftyFirst = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            await SeedProjectsAsync(database, owner.SpaceId, ProjectsPerSpace - 1);

            fiftieth = await CreateAsync(owner, "The fiftieth");
            fiftyFirst = await CreateAsync(owner, "The fifty-first");
        });

        Assert.NotNull(owner);
        Assert.True(fiftieth?.StatusCode == HttpStatusCode.Created, $"Anchor: {fiftieth?.StatusCode} {fiftieth?.Body}");
        Assert.True(fiftyFirst?.StatusCode == HttpStatusCode.Conflict, $"{fiftyFirst?.StatusCode} {fiftyFirst?.Body}");

        var problem = Problem(fiftyFirst!);

        Assert.Equal(ProblemTypes.BoundReached, problem?.Type);
        Assert.Equal(ProjectsPerSpaceCode, problem?.Bound);
        Assert.Equal(ProjectsPerSpace, problem?.Limit);

        var rows = await ProjectRowsAsync(database, owner.SpaceId);

        Assert.Equal(ProjectsPerSpace, rows.Count);
        Assert.DoesNotContain(rows, row => row.Name == "The fifty-first");
        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
    }

    /// <summary>
    /// AC5, Task 6 - two creations sent concurrently at 49 do not both pass: exactly one <c>201</c> and one
    /// <c>409 bound-reached</c>, and the Space holds 50. The count is taken under a lock held to the end of
    /// the unit of work, so the second waits for the first to commit (story 1.6's presence race, not
    /// repeated for a bound whose whole point is the number).
    /// </summary>
    /// <remarks>
    /// <b>Deterministic since story 2.2 Task 7</b> (the 2.1 review's plant P11 - the count without
    /// <c>UPDLOCK, HOLDLOCK</c> - was caught in 2 of 3 runs, because two requests fired together need not
    /// race). The test now takes the bound's own range lock first, sends both creations, waits until both are
    /// blocked on a lock, then commits. With the hint, both queue on the count. Without it, both counts read
    /// 49 through the test's <c>RangeS-U</c> and both inserts wait on <c>RangeI-N</c> until it commits, so
    /// both succeed and the case fails every time. The wait is "blocked on any lock", never one wait type,
    /// so the plant is caught at the insert. <b>Both Accounts are present in the Space before the race</b>:
    /// a first request inserts a <c>SpacePresence</c> row, and the other request's presence count waits on it,
    /// serialising the pair before the bound count - which is why the old case caught P11 only sometimes.
    /// Rewritten by the story 2.2 ATDD run; green against 2.1's product, so a standing gate.
    /// </remarks>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "AD-25")]
    public async Task Two_concurrent_creations_at_49_yield_exactly_one_201_and_one_409()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        WireResponse[] answers = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            // A second member with a client of its own, so the two requests share no WireSession state.
            var member = await RegisterAsync(client, database);
            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");
            await SeedProjectsAsync(database, owner.SpaceId, ProjectsPerSpace - 1);

            // Presence first. An Account's first request in a Space inserts its SpacePresence row; before story 2.9's
            // READ_COMMITTED_SNAPSHOT the second Account's presence count waited on that uncommitted row, which would have
            // serialised the pair before either reached the bound count, so a missing count lock would go unseen.
            Assert.Equal(HttpStatusCode.OK, (await owner.Session.GetAsync(Yello.Contracts.Projects.ProjectRoutes.ListIn(owner.SpaceId))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await member.Session.GetAsync(Yello.Contracts.Projects.ProjectRoutes.ListIn(owner.SpaceId))).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            // The product's bound lock since story 2.9 (open question 14 (a)): the Space's own application lock, which both
            // creations take before they count.
            await using (System.Data.Common.DbCommand count = holder.CreateCommand())
            {
                count.Transaction = held;
                count.CommandText = "EXEC sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction'";
                count.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@resource", $"Yello/ProjectsPerSpace/{owner.SpaceId:D}"));
                await count.ExecuteNonQueryAsync(cancellationToken);
            }

            var racing = Task.WhenAll(
                CreateAsync(owner, "Racer one"),
                CreateAsync(member, "Racer two", owner.SpaceId));

            await Tasks.TaskWire.WaitUntilBlockedOnALockAsync(observer, 2, racing, cancellationToken);

            await held.CommitAsync(cancellationToken);

            answers = await racing;
        });

        Assert.NotNull(owner);
        Assert.Equal(
            [HttpStatusCode.Created, HttpStatusCode.Conflict],
            answers.Select(answer => answer.StatusCode).Order());
        Assert.Equal(ProblemTypes.BoundReached, Problem(answers.Single(answer => answer.StatusCode == HttpStatusCode.Conflict))?.Type);
        Assert.Equal(ProjectsPerSpace, (await ProjectRowsAsync(database, owner.SpaceId)).Count);
    }

    /// <summary>
    /// AC9, Task 7 - the replay comes before the bound: replaying the key of the 50th creation, once the
    /// Space is full, returns that creation's original <c>201</c>, not the 51st's <c>409</c>, and applies
    /// nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-25")]
    [Trait("Requirement", "AD-18")]
    public async Task Replaying_the_key_of_the_50th_creation_returns_its_201_not_the_bound_409()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? original = null;
        WireResponse? replay = null;
        WireResponse? fresh = null;
        var key = Guid.NewGuid().ToString("D");

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            await SeedProjectsAsync(database, owner.SpaceId, ProjectsPerSpace - 1);

            original = await CreateAsync(owner, "The fiftieth", key: key);
            replay = await CreateAsync(owner, "The fiftieth", key: key);

            // Anchor: the Space really is full, so the replay's 201 is the record's and not the bound's.
            fresh = await CreateAsync(owner, "The fiftieth", key: Guid.NewGuid().ToString("D"));
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.Created, original?.StatusCode);
        Assert.Equal(HttpStatusCode.Created, replay?.StatusCode);
        Assert.Equal(original!.Body, replay!.Body);
        Assert.Equal(original.Header("Location"), replay.Header("Location"));
        Assert.Equal(HttpStatusCode.Conflict, fresh?.StatusCode);
        Assert.Equal(ProjectsPerSpace, (await ProjectRowsAsync(database, owner.SpaceId)).Count);
        Assert.DoesNotContain(CrossSpaceKind, await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
    }
}
