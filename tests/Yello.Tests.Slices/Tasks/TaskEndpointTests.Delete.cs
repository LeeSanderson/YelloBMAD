using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Domain;
using Yello.Domain.Bounds;
using Yello.Domain.Memberships;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.DeleteWire;
using static Yello.Tests.Slices.Tasks.LabelWire;
using static Yello.Tests.Slices.Tasks.TaskEditWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Story 2.5 over the wire: deleting a Task. FR-23 (the request-path clause only - its session clause is story 7.4's),
/// AC3, AC7; AD-3, AD-15, AD-20, AR-10, AR-11, NFR-7, PRD §6.2. Written red by the story 2.5 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Activated by story 2.5's dev phase</b> once the route was mapped (story Task 5). The two schema cases at the end -
/// the cascade 2.2 already declared and the absence of any soft-delete column - ran from the ATDD run on as standing
/// gates (AC3's "no trash").
/// </para>
/// <para>
/// <b>Decisions these cases rest on</b> (story Open questions, recommended options): Q4 - an absent, already deleted or
/// foreign id is the byte-identical <c>404 not-available</c>, never a <c>410</c> or a <c>204</c>; Q6 - no
/// <c>BEFORE DELETE</c> predicate, so the FILTER predicate and the query filter alone carry the in-Space not-found.
/// Labels are per Space and survive a Task's deletion; only the Task's applications go.
/// </para>
/// </remarks>
public sealed partial class TaskEndpointTests
{
    /// <summary>
    /// AC7, AC3, AD-15 - an Owner, Admin or Member deletes a Task on either surface: <c>204</c> with no body; the Task
    /// reads <c>404</c>; the Board no longer lists it and its column's true total dropped by one; its <c>TaskLabel</c>
    /// rows are gone while the Label itself still lists and its other application stays; every other Task keeps its
    /// Status and its position key exactly (no sibling renumbered); and nothing is recorded as a refusal.
    /// </summary>
    /// <param name="role">The deleter's Role in the Owner's Space.</param>
    /// <param name="surface">The surface the deletion is sent on.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData("Owner", IsolationSurface.Session)]
    [InlineData("Admin", IsolationSurface.ApiToken)]
    [InlineData("Member", IsolationSurface.Session)]
    [InlineData("Member", IsolationSurface.ApiToken)]
    [Trait("Requirement", "FR-23")]
    [Trait("Requirement", "AD-15")]
    [Trait("Requirement", "AD-4")]
    public async Task An_Owner_Admin_or_Member_deletes_a_Task_and_only_it_and_its_applications_go(string role, IsolationSurface surface)
    {
        var database = Available();
        Actor? owner = null;
        Actor? deleter = null;
        var doomed = Guid.Empty;
        var kept = Guid.Empty;
        var labelId = Guid.Empty;
        IReadOnlyList<TaskRow> before = [];
        IReadOnlyList<(Guid StatusId, int Total, IReadOnlyList<Guid> TaskIds)> columnsBefore = [];
        IReadOnlyList<(Guid StatusId, int Total, IReadOnlyList<Guid> TaskIds)> columnsAfter = [];
        WireResponse? deleted = null;
        WireResponse? read = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            deleter = owner;

            if (role != "Owner")
            {
                deleter = await RegisterAsync(client, database);
                await AddMemberAsync(database, owner.SpaceId, deleter.AccountId, role);
            }

            var token = await IssueTokenAsync(deleter, owner.SpaceId);
            var projectId = await CreateProjectAsync(owner, "Tidying up");

            _ = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Before it"));
            doomed = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Going"));
            kept = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "After it"));
            labelId = await SeedAsync(owner, owner.SpaceId, "Applied twice");

            Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, doomed, ApplyBody(labelId))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, kept, ApplyBody(labelId))).StatusCode);

            before = await TaskRowsAsync(database, owner.SpaceId);
            columnsBefore = Columns(await BoardAsync(owner, owner.SpaceId, projectId));

            deleted = await DeleteTaskOnAsync(surface, deleter, token, owner.SpaceId, doomed);
            read = await ViewTaskAsync(owner, owner.SpaceId, doomed);
            columnsAfter = Columns(await BoardAsync(owner, owner.SpaceId, projectId));
        });

        Assert.NotNull(owner);
        Assert.NotNull(deleter);
        Assert.True(deleted?.StatusCode == HttpStatusCode.NoContent, $"{deleted?.StatusCode} {deleted?.Body}");
        Assert.Equal(string.Empty, deleted!.Body);
        Assert.Equal(HttpStatusCode.NotFound, read?.StatusCode);
        Assert.Equal(ProblemTypes.NotAvailable, Problem(read!)?.Type);

        // The Board: the Task is listed nowhere, and its own column's true total dropped by exactly one.
        var home = columnsBefore.Single(column => column.TaskIds.Contains(doomed));

        Assert.DoesNotContain(columnsAfter, column => column.TaskIds.Contains(doomed));
        Assert.Equal(home.Total - 1, columnsAfter.Single(column => column.StatusId == home.StatusId).Total);

        // AD-15: every survivor keeps its Status and its key exactly - nothing renumbered, nothing re-placed.
        Assert.Equal(
            before.Where(row => row.Id != doomed).Select(row => (row.Id, row.StatusId, row.PositionKey)),
            (await TaskRowsAsync(database, owner.SpaceId)).Select(row => (row.Id, row.StatusId, row.PositionKey)));

        // The applications of the deleted Task are gone; the Label and its other application stay.
        Assert.Empty(await AppliedToAsync(database, owner.SpaceId, doomed));
        Assert.Equal([labelId], await AppliedToAsync(database, owner.SpaceId, kept));
        Assert.Contains(labelId, (await LabelRowsAsync(database, owner.SpaceId)).Select(row => row.Id));
        Assert.Empty(await RefusalKindsAsync(database, deleter.AccountId, owner.SpaceId));
    }

    /// <summary>
    /// AC7, AC1 (the API half of "a Viewer has no delete"), V-1, C-6, NFR-7 - a Viewer of an Owner's Space is refused
    /// <c>403 insufficient-role</c> naming <c>manage-tasks</c> on its Session and on its Token, each recorded once as
    /// <c>InsufficientRole</c>, and the Task is untouched. Anchor: a Member deletes it (<c>204</c>).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-23")]
    [Trait("Requirement", "AR-11")]
    [Trait("Requirement", "NFR-7")]
    public async Task A_Viewer_is_refused_403_manage_tasks_on_both_surfaces_and_the_Task_stays()
    {
        var database = Available();
        Actor? owner = null;
        Actor? viewer = null;
        var taskId = Guid.Empty;
        WireResponse? bySession = null;
        WireResponse? byToken = null;
        IReadOnlyList<TaskRow> afterRefusals = [];
        WireResponse? anchor = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            viewer = await RegisterAsync(client, database);
            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, viewer.AccountId, "Viewer");
            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            (_, taskId) = await ProjectWithTaskAsync(owner, "Viewer cannot remove");
            var token = await IssueTokenAsync(viewer, owner.SpaceId);

            bySession = await DeleteTaskAsync(viewer, owner.SpaceId, taskId);
            byToken = await DeleteTaskWithTokenAsync(viewer, token, owner.SpaceId, taskId);
            afterRefusals = await TaskRowsAsync(database, owner.SpaceId);
            anchor = await DeleteTaskAsync(member, owner.SpaceId, taskId);
        });

        Assert.NotNull(owner);
        Assert.NotNull(viewer);

        foreach (var refused in new[] { bySession!, byToken! })
        {
            Assert.True(refused.StatusCode == HttpStatusCode.Forbidden, $"{refused.StatusCode} {refused.Body}");
            Assert.Equal(ProblemTypes.InsufficientRole, Problem(refused)?.Type);
            Assert.Equal(StableCode.Of(Capability.ManageTasks), Problem(refused)?.Capability);
        }

        Assert.Equal([InsufficientRoleKind, InsufficientRoleKind], await RefusalKindsAsync(database, viewer.AccountId, owner.SpaceId));
        Assert.Equal([taskId], afterRefusals.Select(row => row.Id));
        Assert.Equal(HttpStatusCode.NoContent, anchor?.StatusCode);
    }

    /// <summary>
    /// AC7, AD-3, AD-20, Q4 - a random id, an id already deleted, and a real Task id from another Space, each sent in
    /// one's own Space, answer the byte-identical <c>404 not-available</c> of a Space that does not exist, on either
    /// surface, and record nothing; the other Space's Task is still there. Never a <c>410</c>, never a <c>204</c> for
    /// "already gone" (that would tell a caller the id once existed).
    /// </summary>
    /// <param name="surface">The surface every request is sent on.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "AD-20")]
    [Trait("Requirement", "AR-10")]
    public async Task An_absent_deleted_or_foreign_Task_answers_the_identical_404_and_records_nothing(IsolationSurface surface)
    {
        var database = Available();
        Actor? owner = null;
        Actor? stranger = null;
        var foreign = Guid.Empty;
        var answers = new List<WireResponse>();
        WireResponse? anchor = null;
        WireResponse? first = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            stranger = await RegisterAsync(client, database);

            var token = await IssueTokenAsync(owner, owner.SpaceId);
            var (_, own) = await ProjectWithTaskAsync(owner, "Deleted once");

            (_, foreign) = await ProjectWithTaskAsync(stranger, "Someone else's");

            first = await DeleteTaskOnAsync(surface, owner, token, owner.SpaceId, own);

            answers.Add(await DeleteTaskOnAsync(surface, owner, token, owner.SpaceId, Guid.NewGuid()));
            answers.Add(await DeleteTaskOnAsync(surface, owner, token, owner.SpaceId, own));
            answers.Add(await DeleteTaskOnAsync(surface, owner, token, owner.SpaceId, foreign));

            // Anchor: a Space that does not exist - the boundary's own 404 (recorded, but against no Space of the owner's).
            anchor = await DeleteTaskOnAsync(surface, owner, token, Guid.NewGuid(), Guid.NewGuid());
        });

        Assert.NotNull(owner);
        Assert.NotNull(stranger);
        Assert.Equal(HttpStatusCode.NoContent, first?.StatusCode);
        Assert.NotNull(anchor);
        Assert.Equal(HttpStatusCode.NotFound, anchor.StatusCode);

        Assert.All(answers, answer =>
        {
            Assert.Equal(anchor.StatusCode, answer.StatusCode);
            Assert.Equal(anchor.ContentType, answer.ContentType);
            Assert.Equal(anchor.Body, answer.Body);
            Assert.Equal(anchor.HeaderNames, answer.HeaderNames);
            Assert.Equal(ProblemTypes.NotAvailable, Problem(answer)?.Type);
        });

        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
        Assert.Contains(foreign, (await TaskRowsAsync(database, stranger.SpaceId)).Select(row => row.Id));
    }

    /// <summary>
    /// AC7, story Task 7 - a Label whose only application was on a deleted Task can now be deleted (<c>204</c>), closing
    /// 2.4's <c>409 label-in-use</c> loop. Anchor: before the Task is deleted the Label's deletion is refused
    /// <c>409</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-22")]
    [Trait("Requirement", "FR-23")]
    public async Task A_Label_whose_only_application_was_on_a_deleted_Task_can_then_be_deleted()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? whileApplied = null;
        WireResponse? taskDeleted = null;
        WireResponse? labelDeleted = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var (_, taskId) = await ProjectWithTaskAsync(owner, "Carrier");
            var labelId = await SeedAsync(owner, owner.SpaceId, "Only here");

            Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, taskId, ApplyBody(labelId))).StatusCode);

            whileApplied = await LabelWire.DeleteAsync(owner, owner.SpaceId, labelId);
            taskDeleted = await DeleteTaskAsync(owner, owner.SpaceId, taskId);
            labelDeleted = await LabelWire.DeleteAsync(owner, owner.SpaceId, labelId);
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.Conflict, whileApplied?.StatusCode);
        Assert.Equal(ProblemTypes.LabelInUse, Problem(whileApplied!)?.Type);
        Assert.Equal(HttpStatusCode.NoContent, taskDeleted?.StatusCode);
        Assert.True(labelDeleted?.StatusCode == HttpStatusCode.NoContent, $"{labelDeleted?.StatusCode} {labelDeleted?.Body}");
        Assert.Empty(await LabelRowsAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// AC7, AR-30, AD-25, story Task 7 - deleting a Task frees the Tasks-per-Project bound: at 5,000 the next creation is
    /// refused <c>409 bound-reached</c>; one Task is deleted; the next creation is <c>201</c>. Seeds 4,999 by one
    /// statement (2.2's <see cref="TaskWire.SeedTasksAsync"/>), never 4,999 requests.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "AD-25")]
    [Trait("Priority", "P1")]
    public async Task Deleting_a_Task_from_a_full_Project_lets_the_next_creation_through()
    {
        var database = Available();
        Actor? owner = null;
        var projectId = Guid.Empty;
        WireResponse? refused = null;
        WireResponse? deleted = null;
        WireResponse? admitted = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            projectId = await CreateProjectAsync(owner, "Full, then not");

            var first = (await StatusesAsync(database, owner.SpaceId))[0];
            var limit = BoundRegistry.For(Bound.TasksPerProject).Limit;

            await SeedTasksAsync(database, owner.SpaceId, projectId, first.Id, limit - 1);

            var last = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "The 5,000th"));

            refused = await CreateTaskAsync(owner, owner.SpaceId, projectId, "Refused while full");
            deleted = await DeleteTaskAsync(owner, owner.SpaceId, last);
            admitted = await CreateTaskAsync(owner, owner.SpaceId, projectId, "Admitted after a deletion");
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.Conflict, refused?.StatusCode);
        Assert.Equal(ProblemTypes.BoundReached, Problem(refused!)?.Type);
        Assert.Equal(HttpStatusCode.NoContent, deleted?.StatusCode);
        Assert.True(admitted?.StatusCode == HttpStatusCode.Created, $"{admitted?.StatusCode} {admitted?.Body}");
        Assert.Equal(BoundRegistry.For(Bound.TasksPerProject).Limit, await TaskCountAsync(database, owner.SpaceId, projectId));
    }

    /// <summary>
    /// AC3, AR-8, story Task 7 (schema) - a Task reaches its Project only through the composite key carrying
    /// <c>SpaceId</c>, and that key cascades: deleting a Project removes its Tasks in the deleting statement, and the
    /// cascade cannot leave the Space. Green today - 2.2 declared it - and load-bearing from this story on (Q6: no
    /// <c>BEFORE DELETE</c> predicate is needed because of it). A standing gate.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AR-8")]
    [Trait("Requirement", "FR-17")]
    [Trait("Priority", "P1")]
    public async Task A_Task_reaches_its_Project_through_a_key_carrying_SpaceId_that_cascades()
    {
        var database = Available();

        Assert.Contains("Project|CASCADE|SpaceId>SpaceId,ProjectId>Id", await ForeignKeysOfAsync(database, "Task"));
        Assert.Contains("Task|CASCADE|SpaceId>SpaceId,TaskId>Id", await ForeignKeysOfAsync(database, "TaskLabel"));
    }

    /// <summary>
    /// AC3, PRD §6.2, <c>ARCHITECTURE-SPINE.md:431</c> - no trash: neither <c>Project</c>, <c>Task</c> nor <c>TaskLabel</c>
    /// carries a soft-delete or archive column (<c>Deleted…</c>, <c>IsDeleted</c>, <c>Archived…</c>, <c>Trashed…</c>,
    /// <c>Restore…</c>), so a deletion can only be a deletion. Green today; a standing gate that a soft delete must
    /// argue with. Anchor: the query sees each table's real columns.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-17")]
    [Trait("Requirement", "FR-23")]
    [Trait("Priority", "P1")]
    public async Task No_Project_Task_or_TaskLabel_column_could_hold_a_deletion_back()
    {
        var database = Available();

        var columns = await CatalogueAsync(
            database,
            """
            SELECT OBJECT_NAME(c.object_id) + N'.' + c.name
            FROM sys.columns c
            WHERE c.object_id IN (OBJECT_ID(N'dbo.[Project]'), OBJECT_ID(N'dbo.[Task]'), OBJECT_ID(N'dbo.[TaskLabel]'))
            """);

        // Anchor: the catalogue really is read - each table's key columns are there.
        Assert.Contains("Project.Id", columns);
        Assert.Contains("Task.ProjectId", columns);
        Assert.Contains("TaskLabel.TaskId", columns);

        string[] forbidden = ["deleted", "archiv", "trash", "restor", "removed"];

        Assert.DoesNotContain(columns, column => Array.Exists(forbidden, word => column.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }
}
