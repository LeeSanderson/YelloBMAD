using System.Data.Common;
using System.Net;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Tasks;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Tasks;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Boards;

/// <summary>
/// Story 3.4 AC10 (<c>deferred-work.md:606</c>; Q6 (a): beside PG-16, PG-17 and PG-19) - no lock a request takes reaches
/// another Space's keys. Written red by the story 3.4 ATDD run; green since its dev phase.
/// </summary>
/// <remarks>
/// <para>
/// <b>The shape is PG-19's.</b> The test holds one Space's write open on a <c>WireDatabase</c>-shaped connection under that
/// Space's own key, in the product's own locking shape. The anchor is that Space's own competing request, which must queue
/// on <c>LCK_M_%</c> (the hold is real and the product takes the lock). The case is the other Space's request naming the
/// held id, or touching the same table: it must answer while the hold stands. Waiting on the held side is the failure;
/// nothing is timed. The held transaction is rolled back in a <c>finally</c>.
/// </para>
/// <para>
/// <b>Red before story 3.4 for the reason the ledger gave.</b> <c>LabelStore.DeleteAsync</c> and
/// <c>TaskStore.ReplaceAsync</c> took <c>UPDLOCK, HOLDLOCK</c> on the named Label before any plain read,
/// <c>LabelStore.DeleteAsync</c>'s <c>TaskLabel</c> range read named no Space, and <c>ProjectStore.DeleteAsync</c> took its
/// point lock with no prior existence read. Each now reads plainly first, and the in-use check runs under the Space-named
/// <c>LabelApplications</c> lock with no range lock. Plant 6 restored each old read in turn, and its case went red.
/// </para>
/// <para>
/// <b>Seed data.</b> Accounts, Spaces, Projects, Tasks and Labels go through the product. The only SQL writes are the
/// held side's: <see cref="LabelWire.InsertApplicationAsync"/> inserts one <c>TaskLabel</c> row under B's key for B's
/// own Task and B's own Label (<c>FK_TaskLabel_Task_SpaceId_TaskId</c> and <c>FK_TaskLabel_Label_SpaceId_LabelId</c>
/// hold: both are B's rows in B's Space; the key <c>(SpaceId, TaskId, LabelId)</c> is new), and the application lock
/// <c>Yello/LabelApplications/{spaceId:D}/{labelId:D}</c> that the product's Label application takes (AC10). Everything is
/// rolled back.
/// </para>
/// </remarks>
public sealed partial class BoardPagingTests
{
    /// <summary>
    /// AC10 - a Label deletion in Space A naming Space B's Label answers <c>404 not-available</c> while B's own deletion of
    /// that Label is held; B's own deletion queues on the hold (the anchor).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-1")]
    public async Task A_Label_deletion_naming_another_Spaces_Label_does_not_wait_on_that_Spaces_hold()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        WireResponse? fromA = null;
        WireResponse? inB = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var spaceA = await RegisterAsync(client, database);
            var spaceB = await RegisterAsync(client, database);
            var labelOfB = await LabelWire.SeedAsync(spaceB, spaceB.SpaceId, "B's Label");

            await using var holder = await database.OpenForSpaceAsync(spaceB.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);
            Task<WireResponse>? deletingInB = null;

            try
            {
                await HoldLabelRowAsync(holder, held, labelOfB, cancellationToken);

                deletingInB = LabelWire.DeleteAsync(spaceB, spaceB.SpaceId, labelOfB);

                await WaitUntilBlockedOnALockAsync(observer, 1, deletingInB, cancellationToken);

                var naming = LabelWire.DeleteAsync(spaceA, spaceA.SpaceId, labelOfB);

                await WaitUntilBlockedByTheHolderOrAnsweredAsync(observer, holder, held, 2, naming, cancellationToken);
                Assert.True(naming.IsCompleted, $"Space A's deletion naming B's Label waited on B's held Label (AC10). Waiting: {await WaitsAsync(observer, cancellationToken)}");
                fromA = await naming;
            }
            finally
            {
                await held.RollbackAsync(CancellationToken.None);
            }

            inB = await deletingInB;
        });

        Assert.True(fromA?.StatusCode == HttpStatusCode.NotFound, $"{fromA?.StatusCode} {fromA?.Body}");
        Assert.Equal(ProblemTypes.NotAvailable, Problem(fromA!)?.Type);
        Assert.True(inB?.StatusCode == HttpStatusCode.NoContent, $"{inB?.StatusCode} {inB?.Body}");
    }

    /// <summary>
    /// AC10 - Space A deletes its own unapplied Label while Space B's application of B's Label is held uncommitted: A's
    /// in-use check reaches no <c>TaskLabel</c> key of B's, so A answers <c>204</c> while B is held. B's own deletion of
    /// the Label being applied queues on the hold (the anchor).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-1")]
    public async Task A_Label_deletion_does_not_wait_on_another_Spaces_held_Label_application()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        WireResponse? inA = null;
        WireResponse? inB = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var spaceA = await RegisterAsync(client, database);
            var spaceB = await RegisterAsync(client, database);
            var labelOfA = await LabelWire.SeedAsync(spaceA, spaceA.SpaceId, "A's unapplied Label");
            var projectOfB = await CreateProjectAsync(spaceB, "B's Project");
            var taskOfB = IdOf(await CreateTaskAsync(spaceB, spaceB.SpaceId, projectOfB, "B's Task"));
            var labelOfB = await LabelWire.SeedAsync(spaceB, spaceB.SpaceId, "B's Label");

            await using var holder = await database.OpenForSpaceAsync(spaceB.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);
            Task<WireResponse>? deletingInB = null;

            try
            {
                await TakeLabelApplicationsLockAsync(holder, held, spaceB.SpaceId, labelOfB, cancellationToken);
                await LabelWire.InsertApplicationAsync(holder, held, spaceB.SpaceId, taskOfB, labelOfB, cancellationToken);

                deletingInB = LabelWire.DeleteAsync(spaceB, spaceB.SpaceId, labelOfB);

                await WaitUntilBlockedOnALockAsync(observer, 1, deletingInB, cancellationToken);

                var deletingInA = LabelWire.DeleteAsync(spaceA, spaceA.SpaceId, labelOfA);

                await WaitUntilBlockedByTheHolderOrAnsweredAsync(observer, holder, held, 2, deletingInA, cancellationToken);
                Assert.True(deletingInA.IsCompleted, $"Space A's Label deletion waited on B's held application (AC10, :606). Waiting: {await WaitsAsync(observer, cancellationToken)}");
                inA = await deletingInA;
            }
            finally
            {
                await held.RollbackAsync(CancellationToken.None);
            }

            inB = await deletingInB;
        });

        Assert.True(inA?.StatusCode == HttpStatusCode.NoContent, $"{inA?.StatusCode} {inA?.Body}");
        Assert.True(inB?.StatusCode == HttpStatusCode.NoContent, $"{inB?.StatusCode} {inB?.Body}");
    }

    /// <summary>
    /// AC10 - a Labels edit of a Task in Space A naming Space B's Label answers <c>400 invalid-task-labels</c> while B's
    /// own application of that Label is held; B's own edit queues on the hold (the anchor).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-1")]
    public async Task A_Label_application_naming_another_Spaces_Label_does_not_wait_on_that_Spaces_hold()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        WireResponse? fromA = null;
        WireResponse? inB = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var spaceA = await RegisterAsync(client, database);
            var spaceB = await RegisterAsync(client, database);
            var taskOfA = IdOf(await CreateTaskAsync(spaceA, spaceA.SpaceId, await CreateProjectAsync(spaceA, "A's Project"), "A's Task"));
            var taskOfB = IdOf(await CreateTaskAsync(spaceB, spaceB.SpaceId, await CreateProjectAsync(spaceB, "B's Project"), "B's Task"));
            var labelOfB = await LabelWire.SeedAsync(spaceB, spaceB.SpaceId, "B's Label");

            await using var holder = await database.OpenForSpaceAsync(spaceB.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);
            Task<WireResponse>? applyingInB = null;

            try
            {
                await HoldLabelRowAsync(holder, held, labelOfB, cancellationToken);

                applyingInB = ApplyAsync(spaceB, taskOfB, labelOfB);

                await WaitUntilBlockedOnALockAsync(observer, 1, applyingInB, cancellationToken);

                var naming = ApplyAsync(spaceA, taskOfA, labelOfB);

                await WaitUntilBlockedByTheHolderOrAnsweredAsync(observer, holder, held, 2, naming, cancellationToken);
                Assert.True(naming.IsCompleted, $"Space A's Labels edit naming B's Label waited on B's held Label (AC10). Waiting: {await WaitsAsync(observer, cancellationToken)}");
                fromA = await naming;
            }
            finally
            {
                await held.RollbackAsync(CancellationToken.None);
            }

            inB = await applyingInB;
        });

        Assert.True(fromA?.StatusCode == HttpStatusCode.BadRequest, $"{fromA?.StatusCode} {fromA?.Body}");
        Assert.Equal(ProblemTypes.InvalidTaskLabels, Problem(fromA!)?.Type);
        Assert.True(inB?.StatusCode == HttpStatusCode.OK, $"{inB?.StatusCode} {inB?.Body}");
    }

    /// <summary>
    /// AC10 - a Project deletion in Space A naming Space B's Project answers <c>404 not-available</c> while B's own
    /// deletion of that Project is held; B's own deletion queues on the hold (the anchor).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "NFR-1")]
    public async Task A_Project_deletion_naming_another_Spaces_Project_does_not_wait_on_that_Spaces_hold()
    {
        var database = Available();
        var cancellationToken = TestContext.Current.CancellationToken;
        WireResponse? fromA = null;
        WireResponse? inB = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var spaceA = await RegisterAsync(client, database);
            var spaceB = await RegisterAsync(client, database);
            var projectOfB = await CreateProjectAsync(spaceB, "B's Project");

            await using var holder = await database.OpenForSpaceAsync(spaceB.SpaceId, cancellationToken);
            await using var observer = await database.OpenWithoutSpaceContextAsync(cancellationToken);
            await using var held = await holder.BeginTransactionAsync(cancellationToken);
            Task<WireResponse>? deletingInB = null;

            try
            {
                await DeleteWire.HoldProjectRowAsync(holder, held, projectOfB, cancellationToken);

                deletingInB = DeleteWire.DeleteProjectAsync(spaceB, spaceB.SpaceId, projectOfB);

                await WaitUntilBlockedOnALockAsync(observer, 1, deletingInB, cancellationToken);

                var naming = DeleteWire.DeleteProjectAsync(spaceA, spaceA.SpaceId, projectOfB);

                await WaitUntilBlockedByTheHolderOrAnsweredAsync(observer, holder, held, 2, naming, cancellationToken);
                Assert.True(naming.IsCompleted, $"Space A's deletion naming B's Project waited on B's held Project (AC10). Waiting: {await WaitsAsync(observer, cancellationToken)}");
                fromA = await naming;
            }
            finally
            {
                await held.RollbackAsync(CancellationToken.None);
            }

            inB = await deletingInB;
        });

        Assert.True(fromA?.StatusCode == HttpStatusCode.NotFound, $"{fromA?.StatusCode} {fromA?.Body}");
        Assert.Equal(ProblemTypes.NotAvailable, Problem(fromA!)?.Type);
        Assert.True(inB?.StatusCode == HttpStatusCode.NoContent, $"{inB?.StatusCode} {inB?.Body}");
    }

    private static Task<WireResponse> ApplyAsync(Actor actor, Guid taskId, Guid labelId) =>
        actor.Session.SendWithHeadersAsync(HttpMethod.Patch, TaskRoutes.TaskOf(actor.SpaceId, taskId), LabelWire.ApplyBody(labelId), NoHeaders);

    /// <summary>
    /// The product's own Label point lock (<c>LabelStore</c>'s locking read), taken in the caller's open transaction.
    /// </summary>
    private static async Task HoldLabelRowAsync(SqlConnection connection, DbTransaction transaction, Guid labelId, CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();

        command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM dbo.[Label] WITH (UPDLOCK, HOLDLOCK, INDEX(1)) WHERE Id = @label";
        command.Parameters.Add(new SqlParameter("@label", labelId));

        Assert.Equal(1, Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// AC10's application lock for one Label's applications, named for the Label's own Space, in the caller's open
    /// transaction - what the fixed <c>TaskStore.ReplaceAsync</c> takes before it applies the Label.
    /// </summary>
    private static async Task TakeLabelApplicationsLockAsync(SqlConnection connection, DbTransaction transaction, Guid spaceId, Guid labelId, CancellationToken cancellationToken)
    {
        await using DbCommand take = connection.CreateCommand();

        take.Transaction = transaction;
        // sp_getapplock reports a refusal through its return code, not an exception (story 3.4 review), so the code is read
        // and a hold that was not granted fails here rather than leaving the anchor to pass on the row lock alone.
        take.CommandText = "DECLARE @granted int; EXEC @granted = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction'; SELECT @granted;";
        take.Parameters.Add(new SqlParameter("@resource", Yello.Infrastructure.Persistence.ApplicationLocks.LabelApplications(spaceId, labelId)));

        var granted = Convert.ToInt32(await take.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture);

        Assert.True(granted >= 0, $"sp_getapplock answered {granted}: the test's hold of the Label's applications lock was not granted.");
    }
}
