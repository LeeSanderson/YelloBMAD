using System.Data.Common;
using System.Net;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.TaskEditWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Story 2.3 Task 6 over the wire: a Status change is a second writer that places position keys, so two
/// placements into one column - an edit and a creation, or two edits - must not compute the same key. AC9;
/// AD-15, <c>deferred-work.md:492</c>. Written red by the story 2.3 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>The interleaving is the test's</b> (TESTING-CONVENTIONS "A race is controlled, never hoped for"): the
/// test opens a connection under the Space's key, takes the destination column's range itself
/// (<c>WITH (UPDLOCK, HOLDLOCK)</c> over <c>ProjectId = @p AND StatusId = @s</c>, a subset of the
/// Project-wide range the edit and the creation both take, so either queues behind it), sends both requests,
/// waits until both are blocked on any <c>LCK_M_%</c>
/// (<see cref="TaskWire.WaitUntilBlockedOnALockAsync"/>), then commits. Both Accounts read the Board first, so
/// neither writes its <c>SpacePresence</c> row inside the race (the 2.2 ATDD finding).
/// </para>
/// <para>
/// <b>What a missing lock looks like:</b> both placements read the same last key, compute the same
/// <c>PositionKey.After(last)</c>, and the unique index <c>(SpaceId, ProjectId, StatusId, PositionKey)</c>
/// refuses the second as a <c>500</c>. The dev phase measured that a column range would also serialise an
/// edit against a creation, and chose the Project-wide range for one lock order (<c>TaskStore</c>'s remarks).
/// These two cases catch the hint's removal only by timing; the case that holds the column and inserts under
/// it, <c>A_Status_edit_reads_the_last_key_of_its_new_column_only_under_the_lock</c>, catches it every run.
/// </para>
/// </remarks>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// AC9 (a) - an edit moving a Task into the first column and a creation into that same column, both
    /// queued on the held column range, are both admitted (<c>200</c>, <c>201</c>) with distinct keys, each
    /// after the column's previous last key.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-15")]
    [Trait("Requirement", "AR-21")]
    public async Task An_edit_and_a_creation_into_one_column_take_distinct_keys()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var projectId = Guid.Empty;
        var moved = Guid.Empty;
        var lastBefore = string.Empty;
        WireResponse[] answers = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var member = await RegisterAsync(client, database);
            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            projectId = await CreateProjectAsync(owner, "Racing");
            var statuses = await StatusesAsync(database, owner.SpaceId);

            _ = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Already first"));
            moved = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Coming back"));
            Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, moved, Body(new { statusId = statuses[1].Id }))).StatusCode);

            lastBefore = LastKey(await TaskRowsAsync(database, owner.SpaceId), statuses[0].Id);

            // Presence first, for both racing Accounts.
            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, projectId)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(member, owner.SpaceId, projectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldColumnAsync(holder, held, projectId, statuses[0].Id, cancellationToken);

            var racing = Task.WhenAll(
                EditTaskAsync(owner, owner.SpaceId, moved, Body(new { statusId = statuses[0].Id })),
                CreateTaskAsync(member, owner.SpaceId, projectId, "Created in the race"));

            await WaitUntilBlockedOnALockAsync(observer, 2, racing, cancellationToken);

            await held.CommitAsync(cancellationToken);

            answers = await racing;
        });

        Assert.NotNull(owner);
        Assert.True(answers[0].StatusCode == HttpStatusCode.OK, $"{answers[0].StatusCode} {answers[0].Body}");
        Assert.True(answers[1].StatusCode == HttpStatusCode.Created, $"{answers[1].StatusCode} {answers[1].Body}");

        AssertDistinctKeysAfter(await TaskRowsAsync(database, owner.SpaceId), [moved, IdOf(answers[1])], lastBefore);
    }

    /// <summary>
    /// AC9 (b) - two edits by two Accounts, each moving a different Task into the third column, both queued on
    /// the held column range, are both admitted with distinct keys after the column's previous last key.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-15")]
    [Trait("Requirement", "AR-21")]
    public async Task Two_edits_into_one_column_take_distinct_keys()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var first = Guid.Empty;
        var second = Guid.Empty;
        var lastBefore = string.Empty;
        WireResponse[] answers = [];

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var member = await RegisterAsync(client, database);
            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            var projectId = await CreateProjectAsync(owner, "Racing edits");
            var statuses = await StatusesAsync(database, owner.SpaceId);

            var resident = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Already done"));
            Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, resident, Body(new { statusId = statuses[2].Id }))).StatusCode);

            first = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Finishing one"));
            second = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Finishing two"));

            lastBefore = LastKey(await TaskRowsAsync(database, owner.SpaceId), statuses[2].Id);

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, projectId)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(member, owner.SpaceId, projectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldColumnAsync(holder, held, projectId, statuses[2].Id, cancellationToken);

            var racing = Task.WhenAll(
                EditTaskAsync(owner, owner.SpaceId, first, Body(new { statusId = statuses[2].Id })),
                EditTaskAsync(member, owner.SpaceId, second, Body(new { statusId = statuses[2].Id })));

            await WaitUntilBlockedOnALockAsync(observer, 2, racing, cancellationToken);

            await held.CommitAsync(cancellationToken);

            answers = await racing;
        });

        Assert.NotNull(owner);
        foreach (var answer in answers)
        {
            Assert.True(answer.StatusCode == HttpStatusCode.OK, $"{answer.StatusCode} {answer.Body}");
        }

        AssertDistinctKeysAfter(await TaskRowsAsync(database, owner.SpaceId), [first, second], lastBefore);
    }

    /// <summary>
    /// AC9, Task 6 - a Status edit reads its new column's last key only once the lock is its own: the test holds
    /// the column, the edit queues, the test places a Task at the key after the column's last (what a creation
    /// that has already read the last key would write) and commits, and the edit is admitted with a key after
    /// the test's. An edit that read the last key without the lock would compute the test's key and collide on
    /// the unique index - a <c>500</c>.
    /// </summary>
    /// <remarks>
    /// Added by the dev phase: the two scaffolded race cases catch the hint's removal only by timing (one run in
    /// three), because the edit's Project-wide read is serialised anyway by another writer's exclusive key
    /// locks. This case makes the interleaving the test's.
    /// </remarks>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-15")]
    [Trait("Requirement", "AR-21")]
    public async Task A_Status_edit_reads_the_last_key_of_its_new_column_only_under_the_lock()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        Actor? owner = null;
        var moved = Guid.Empty;
        var placed = Guid.NewGuid();
        var lastBefore = string.Empty;
        WireResponse? answer = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var projectId = await CreateProjectAsync(owner, "Placed under the lock");
            var statuses = await StatusesAsync(database, owner.SpaceId);
            var resident = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Already done"));

            Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, resident, Body(new { statusId = statuses[2].Id }))).StatusCode);

            moved = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Finishing"));
            lastBefore = LastKey(await TaskRowsAsync(database, owner.SpaceId), statuses[2].Id);

            Assert.Equal(HttpStatusCode.OK, (await BoardAsync(owner, owner.SpaceId, projectId)).StatusCode);

            await using var holder = await database.OpenForSpaceAsync(owner.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);

            await HoldColumnAsync(holder, held, projectId, statuses[2].Id, cancellationToken);

            var racing = EditTaskAsync(owner, owner.SpaceId, moved, Body(new { statusId = statuses[2].Id }));

            await WaitUntilBlockedOnALockAsync(observer, 1, racing, cancellationToken);

            await using DbCommand place = holder.CreateCommand();

            place.Transaction = held;
            place.CommandText =
                """
                INSERT dbo.[Task] (Id, SpaceId, ProjectId, StatusId, Title, PositionKey, CreatedAt)
                VALUES (@id, @space, @project, @status, N'Placed by the test', @key, SYSDATETIMEOFFSET())
                """;
            place.Parameters.Add(new SqlParameter("@id", placed));
            place.Parameters.Add(new SqlParameter("@space", owner.SpaceId));
            place.Parameters.Add(new SqlParameter("@project", projectId));
            place.Parameters.Add(new SqlParameter("@status", statuses[2].Id));
            place.Parameters.Add(new SqlParameter("@key", Yello.Domain.Tasks.PositionKey.After(lastBefore)));

            Assert.Equal(1, await place.ExecuteNonQueryAsync(cancellationToken));

            await held.CommitAsync(cancellationToken);

            answer = await racing;
        });

        Assert.NotNull(owner);
        Assert.True(answer?.StatusCode == HttpStatusCode.OK, $"{answer?.StatusCode} {answer?.Body}");

        AssertDistinctKeysAfter(await TaskRowsAsync(database, owner.SpaceId), [moved, placed], lastBefore);
    }

    /// <summary>
    /// Takes the destination column's key range in the test's own transaction - the statement Task 6 has the
    /// edit run before it places a key.
    /// </summary>
    private static async Task HoldColumnAsync(SqlConnection holder, DbTransaction held, Guid projectId, Guid statusId, CancellationToken cancellationToken)
    {
        await using DbCommand column = holder.CreateCommand();

        column.Transaction = held;
        column.CommandText = "SELECT PositionKey FROM dbo.[Task] WITH (UPDLOCK, HOLDLOCK) WHERE ProjectId = @project AND StatusId = @status";
        column.Parameters.Add(new SqlParameter("@project", projectId));
        column.Parameters.Add(new SqlParameter("@status", statusId));

        await column.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string LastKey(IReadOnlyList<TaskRow> rows, Guid statusId) =>
        rows.Where(row => row.StatusId == statusId).Select(row => row.PositionKey).Max(StringComparer.Ordinal) ?? string.Empty;

    /// <summary>
    /// The placed Tasks' keys are distinct, each sorts after the column's previous last key, and no two Tasks
    /// in any one column share a key.
    /// </summary>
    private static void AssertDistinctKeysAfter(IReadOnlyList<TaskRow> rows, Guid[] placed, string lastBefore)
    {
        var keys = rows.Where(row => placed.Contains(row.Id)).Select(row => row.PositionKey).ToList();

        Assert.Equal(placed.Length, keys.Count);
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        Assert.All(keys, key => Assert.True(string.CompareOrdinal(key, lastBefore) > 0, $"{key} is not after {lastBefore}."));
        Assert.All(
            rows.GroupBy(row => row.StatusId),
            column => Assert.Equal(column.Count(), column.Select(row => row.PositionKey).Distinct(StringComparer.Ordinal).Count()));
    }
}
