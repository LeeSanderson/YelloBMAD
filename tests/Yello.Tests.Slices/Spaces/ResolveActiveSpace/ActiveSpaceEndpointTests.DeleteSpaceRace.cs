using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Application.Spaces;
using Yello.Contracts;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;
using Yello.Domain.Memberships;
using Yello.Infrastructure.Persistence;
using Yello.Tests.Shared;
using DeleteWire = Yello.Tests.Slices.Tasks.DeleteWire;
using LabelWire = Yello.Tests.Slices.Tasks.LabelWire;
using TaskWire = Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Spaces.ResolveActiveSpace;

/// <summary>
/// Story 3.3 AC13 over the wire: the races a Space deletion makes reachable. A request resolved against a Space that is
/// deleted before its unit of work commits answers the uniform boundary <c>404</c> - never a <c>200</c> for a write that
/// touched nothing (<c>deferred-work.md:666</c>), never a <c>500</c> from a foreign key (547) or from a name read that found
/// no row (<c>:360</c>). Q4 (a); AD-1, AD-3, AD-20, AR-34. Written as red-phase scaffolds by the story 3.3 ATDD run and activated by its dev phase.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every race is controlled, never hoped for</b> (TESTING-CONVENTIONS "Races"). The test plays the deletion in the
/// product's statement shape (Q4 (a): the Space's <c>Yello/ProjectsPerSpace/{spaceId}</c> application lock, then one
/// <c>DELETE</c> of the <c>Space</c> under its own key, the cascades doing the rest), holds it uncommitted, sends the
/// competing request, waits until that request is blocked on a lock (<see cref="TaskWire.WaitUntilBlockedOnALockAsync"/>,
/// any <c>LCK_M_%</c>), then commits. Each racing Account is made present in the Space first (a <c>GET</c>), so its
/// request writes no <c>SpacePresence</c> row on the way - except RACE-S2, whose subject is exactly that write.
/// </para>
/// <para>
/// <b>Where each request waits, as the survey predicts it</b> (confirm on activation, and record any correction in the
/// Debug Log): RACE-S1's rename on its <c>UPDATE</c> of the <c>Space</c> row, which the test holds
/// <c>UPDLOCK, HOLDLOCK</c> before it deletes the row in the same transaction; RACE-S2's first request on its presence
/// insert's foreign-key check against the deleted <c>Space</c> row; RACE-S3's Project creation on the Projects-per-Space
/// application lock; RACE-S4's Task creation on its insert's foreign-key check against the cascaded <c>Project</c> row.
/// Under RCSI (story 2.9) the resolution's Membership read and the name read take no lock, so none of them waits there.
/// </para>
/// <para>
/// <b>The name read (<c>:360</c>) has no wire race.</b> Between the resolution and <c>ActiveSpaceNameReader</c>'s read a
/// <c>GET</c> by a present Account takes no lock at all, so there is no point at which a test can hold it. RACE-S6 drives
/// the reader directly instead, on the slice suite's database, with a resolved context for a Space deleted after it was
/// resolved - the exact state the race produces. The plant "restore <c>SingleAsync</c>" turns it red.
/// </para>
/// <para>
/// <b>Seed constraints:</b> as <c>ActiveSpaceEndpointTests.DeleteSpace.cs</c> - Accounts and their Personal Spaces by
/// registration, Memberships by <see cref="TaskWire.AddMemberAsync"/>, Projects by the product. RACE-S5's one SQL insert
/// writes <c>dbo.[Project]</c> with exactly the columns <c>ProjectConfiguration</c> requires (<c>Id</c> supplied -
/// <c>ValueGeneratedNever</c>, <c>SpaceId</c>, <c>Name</c>, <c>CreatedAt</c>), under the Space's own key (the
/// <c>BLOCK AFTER INSERT</c> predicate), into a Space that exists when it is sent (<c>FK_Project_Space_SpaceId</c>).
/// </para>
/// </remarks>
public sealed partial class ActiveSpaceEndpointTests
{
    /// <summary>
    /// RACE-S1 (AC13, Q4 (a), <c>deferred-work.md:666</c>) - the test holds the Space row; a rename by its Owner queues on
    /// it; the test deletes the Space in the same transaction and commits; the rename's <c>UPDATE</c> now touches no row
    /// and must answer the boundary <c>404 not-available</c>. Today it answers <c>200</c> with the name it never wrote.
    /// Sent with no <c>Idempotency-Key</c>, so the answer is the rename's own and not the record insert's.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "AD-3")]
    public async Task A_rename_that_loses_its_Space_to_a_deletion_answers_the_boundary_404_not_200()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        var spaceId = Guid.Empty;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await TaskWire.RegisterAsync(client, database);
            spaceId = owner.SpaceId;

            // Present in the Space, so the rename writes nothing before its UPDATE.
            Assert.Equal(HttpStatusCode.OK, (await owner.Session.GetAsync(SpaceRoutes.ActiveOf(spaceId))).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await RunAsync(holder, held, "SELECT COUNT(*) FROM dbo.[Space] WITH (UPDLOCK, HOLDLOCK) WHERE Id = @space", spaceId, cancellationToken);

            var racing = owner.Session.SendWithHeadersAsync(
                HttpMethod.Patch,
                SpaceRoutes.ActiveOf(spaceId),
                JsonContent.Create(new { name = "Renamed into nothing " + Suffix() }, options: JsonOptions),
                NoKey);

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);

            Assert.Equal(1, await RunAsync(holder, held, "DELETE FROM dbo.[Space] WHERE Id = @space", spaceId, cancellationToken));
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.True(answer?.StatusCode == HttpStatusCode.NotFound, $"The rename of a Space deleted under it answered {answer?.StatusCode}: {answer?.Body}");
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(answer!));
        Assert.Empty(await SpaceNamesOfAsync(database, spaceId));
    }

    /// <summary>
    /// RACE-S2 (AC13, Q4 (a)) - a Member's <b>first</b> request in the Space queues behind an uncommitted deletion on its
    /// presence insert's foreign key; once the deletion commits, the insert meets 547, and the answer must be the uniform
    /// boundary <c>404</c> with a <c>CrossSpace</c> record - the next request's answer - never a <c>500</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-20")]
    public async Task A_first_request_queued_behind_a_Space_deletion_answers_the_boundary_404_never_500()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        var (spaceId, memberId) = (Guid.Empty, Guid.Empty);
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await TaskWire.RegisterAsync(client, database);
            var member = await TaskWire.RegisterAsync(client, database);
            (spaceId, memberId) = (owner.SpaceId, member.AccountId);

            await TaskWire.AddMemberAsync(database, spaceId, memberId, "Member");

            // The anchor: the Space is served. The Member has not asked yet, so it is not present.
            Assert.Equal(HttpStatusCode.OK, (await owner.Session.GetAsync(SpaceRoutes.StatusesOf(spaceId))).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldTheDeletionAsync(holder, held, spaceId, cancellationToken);

            var racing = member.Session.GetAsync(SpaceRoutes.StatusesOf(spaceId));

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.True(answer?.StatusCode == HttpStatusCode.NotFound, $"A request racing the deletion answered {answer?.StatusCode}: {answer?.Body}");
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(answer!));
        Assert.Contains("CrossSpace", await TaskWire.RefusalKindsAsync(database, memberId, spaceId));
    }

    /// <summary>
    /// RACE-S3 (AC13, Q4 (a)) - a Project creation queues behind an uncommitted deletion on the Space's Projects-per-Space
    /// lock; once the deletion commits, it must answer the boundary <c>404</c>, never a <c>500</c> from
    /// <c>FK_Project_Space_SpaceId</c>, and no Project exists.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "FR-17")]
    public async Task A_Project_creation_queued_behind_a_Space_deletion_answers_404_never_500()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        var spaceId = Guid.Empty;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await TaskWire.RegisterAsync(client, database);
            spaceId = owner.SpaceId;

            Assert.Equal(HttpStatusCode.OK, (await owner.Session.GetAsync(ProjectRoutes.ListIn(spaceId))).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldTheDeletionAsync(holder, held, spaceId, cancellationToken);

            var racing = owner.Session.SendWithHeadersAsync(
                HttpMethod.Post,
                ProjectRoutes.ListIn(spaceId),
                JsonContent.Create(new { name = "Too late " + Suffix() }, options: JsonOptions),
                NoKey);

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.True(answer?.StatusCode == HttpStatusCode.NotFound, $"A Project creation racing the deletion answered {answer?.StatusCode}: {answer?.Body}");
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(answer!));
        Assert.Equal(0, (await SpaceRowCountsAsync(database, spaceId)).Tables["Project"]);
    }

    /// <summary>
    /// RACE-S4 (AC13, Q4 (a)) - a Task creation in a Project of the Space queues behind an uncommitted deletion on its
    /// insert's foreign key to the cascaded <c>Project</c> row; once the deletion commits, it must answer the boundary
    /// <c>404</c>, never a <c>500</c> from <c>FK_Task_Project_SpaceId_ProjectId</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "FR-19")]
    public async Task A_Task_creation_queued_behind_a_Space_deletion_answers_404_never_500()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        var spaceId = Guid.Empty;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await TaskWire.RegisterAsync(client, database);
            spaceId = owner.SpaceId;

            var projectId = await TaskWire.CreateProjectAsync(owner, "Emptied " + Suffix());

            Assert.Equal(HttpStatusCode.OK, (await owner.Session.GetAsync(ProjectRoutes.ListIn(spaceId))).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldTheDeletionAsync(holder, held, spaceId, cancellationToken);

            var racing = TaskWire.CreateTaskAsync(owner, spaceId, projectId, "Too late");

            await TaskWire.WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);
            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.True(answer?.StatusCode == HttpStatusCode.NotFound, $"A Task creation racing the deletion answered {answer?.StatusCode}: {answer?.Body}");
        Assert.Equal(ProblemTypes.NotAvailable, ProblemType(answer!));
        Assert.Equal(0, (await SpaceRowCountsAsync(database, spaceId)).Tables["Task"]);
    }

    /// <summary>
    /// RACE-S5 (AC10, AC13, Q4 (a)) - the other direction: the test <i>is</i> a Project creation that has taken the
    /// Projects-per-Space lock and counted; the Owner's deletion is sent and must queue on that lock; the test inserts its
    /// Project and commits; the deletion answers <c>204</c> and takes the new Project with it. A deletion that skipped the
    /// lock commits under the creation, whose insert then meets 547 - reported as exactly that. (The lock is "optional" in
    /// story Task 3; this case is what makes it a decision: if it is not taken, record the reason and keep this case red.)
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-34")]
    [Trait("Requirement", "FR-7")]
    public async Task A_deletion_waits_for_a_Project_creation_that_holds_the_Projects_lock_and_takes_the_new_Project_with_it()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        var spaceId = Guid.Empty;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await TaskWire.RegisterAsync(client, database);
            spaceId = owner.SpaceId;

            Assert.Equal(HttpStatusCode.OK, (await owner.Session.GetAsync(ProjectRoutes.ListIn(spaceId))).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(spaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            // The creation, up to its insert: the bound's lock, then its count.
            await DeleteWire.HoldProjectsPerSpaceAsync(holder, held, cancellationToken);
            _ = await RunAsync(holder, held, "SELECT COUNT(*) FROM dbo.[Project] WHERE SpaceId = @space", spaceId, cancellationToken);

            var racing = DeleteSpaceOverWireAsync(owner.Session, spaceId, Guid.NewGuid().ToString("D"));

            await LabelWire.WaitUntilBlockedOrAnsweredAsync(observer, 1, racing, cancellationToken);

            var metForeignKey = false;

            try
            {
                _ = await RunAsync(
                    holder,
                    held,
                    "INSERT dbo.[Project] (Id, SpaceId, Name, CreatedAt) VALUES (NEWID(), @space, N'Created by the test', SYSDATETIMEOFFSET())",
                    spaceId,
                    cancellationToken);
            }
            catch (SqlException exception) when (exception.Number == 547)
            {
                metForeignKey = true;
            }

            Assert.False(
                metForeignKey,
                "The deletion did not wait on the Projects-per-Space lock: it committed under a creation that had counted, "
                + "whose insert then met the foreign key (547) - a 500 in the product.");

            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.True(answer?.StatusCode == HttpStatusCode.NoContent, $"{answer?.StatusCode} {answer?.Body}");
        Assert.Equal(0, (await SpaceRowCountsAsync(database, spaceId)).Tables["Project"]);
        Assert.Empty(await SpaceNamesOfAsync(database, spaceId));
    }

    /// <summary>
    /// RACE-S6 (AC13, <c>deferred-work.md:360</c>) - <c>ActiveSpaceNameReader</c>, given a context resolved for a Space that
    /// was deleted after the resolution, answers no name (so <c>GET /api/v1/spaces/{id}</c> can answer the boundary
    /// <c>404</c>) instead of throwing from <c>SingleAsync</c>, which the Host answers <c>500</c>. Anchored: the same reader
    /// reads the Space's name before the deletion. Driven directly - see the class remarks for why there is no wire race.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-2")]
    public async Task The_name_reader_answers_no_name_for_a_Space_deleted_after_it_was_resolved()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        var address = RandomAddress();
        var spaceId = Guid.NewGuid();
        var spaceName = "Read then gone " + Suffix();
        var accountId = Guid.Empty;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            _ = await SignedInAsync(client, address);
            accountId = await AccountIdOfAsync(database, address);
        });

        await SeedNamedSpaceAsync(database, spaceId, spaceName, accountId, Role.Owner);

        var scope = new SpaceScope();
        scope.Resolve(new ActiveSpaceContext(accountId, spaceId, Role.Owner));

        await using var context = database.CreateContext(scope);
        var reader = new ActiveSpaceNameReader(context, scope);

        Assert.Equal(spaceName, await reader.ReadAsync(cancellationToken));

        await DeleteSpaceInSqlAsync(database, spaceId, commit: true);

        var name = await reader.ReadAsync(cancellationToken);

        Assert.Null(name);
    }

    /// <summary>
    /// The deletion Q4 (a) recommends, played by the test in its open transaction: the Space's Projects-per-Space lock, then
    /// one <c>DELETE</c> of the <c>Space</c> under its own key. Left uncommitted for the caller.
    /// </summary>
    private static async Task HoldTheDeletionAsync(SqlConnection holder, DbTransaction held, Guid spaceId, CancellationToken cancellationToken)
    {
        await DeleteWire.HoldProjectsPerSpaceAsync(holder, held, cancellationToken);

        Assert.Equal(1, await RunAsync(holder, held, "DELETE FROM dbo.[Space] WHERE Id = @space", spaceId, cancellationToken));
    }

    /// <summary>
    /// Runs one statement taking the Space as <c>@space</c> in the caller's open transaction, and answers its scalar for a
    /// query or its affected-row count otherwise.
    /// </summary>
    private static async Task<int> RunAsync(SqlConnection connection, DbTransaction transaction, string sql, Guid spaceId, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
#pragma warning disable CA2100 // Review SQL queries - constant statements written by this class; the value is a parameter.
        command.CommandText = sql;
#pragma warning restore CA2100
        command.Parameters.Add(new SqlParameter("@space", spaceId));

        if (sql.StartsWith("SELECT", StringComparison.Ordinal))
        {
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
