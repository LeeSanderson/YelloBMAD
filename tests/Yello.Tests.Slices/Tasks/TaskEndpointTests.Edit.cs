using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts;
using Yello.Domain;
using Yello.Domain.Memberships;
using Yello.Domain.Tasks;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.TaskEditWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// Story 2.3 over the wire: read one Task and edit its title, Status and due date, on both surfaces, through
/// the pipeline that already resolves the Space, checks <c>manage-tasks</c> and replays keys. AC1, AC3, AC4,
/// AC7, AC9, AC11; FR-20, AR-20, AR-34, AD-3, AD-11, AD-15, AD-17. Written red by the story 2.3 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffolded against the recommended options</b> (story 2.3 open questions): Q1 (a) one <c>PATCH</c> with
/// merge-patch semantics - an absent member is unchanged, <c>dueDate: null</c> clears, <c>title: null</c> and
/// <c>statusId: null</c> are refused, a body naming no member is refused; Q3 (a) a Status change appends after
/// the new column's last key; Q4 (a) any instant in 2000-2100, stored and answered in UTC; Q5 (a) <c>400</c>
/// with a stable type per field; Q13 (a) a real Status id from another Space stands in for AC4's
/// sibling-Project Status until 6.1 adds deltas. The route and the two new types come from
/// <see cref="TaskEditScaffold"/> until the contracts hold them.
/// </para>
/// <para>
/// <b>The same class and database as 2.2's Task cases</b> (one more partial file): no new engine, no new
/// fixture. The one 5,000-row seed is the <c>ConsultsBound</c> case (story 2.3 Task 12's plant).
/// </para>
/// </remarks>
public sealed partial class TaskEndpointTests
{
    private static readonly string[] EditedTaskProperties = ["createdAt", "dueDate", "id", "positionKey", "statusId", "title"];

    private static readonly string[] AlreadyDoneTitles = ["Already done one", "Already done two"];

    /// <summary>
    /// AC1, Q1 (a) - an Owner edits a Task's title on either surface: <c>200</c>, the body is exactly the Task
    /// (<c>{createdAt, dueDate, id, positionKey, statusId, title}</c>) with the title trimmed, and the Status,
    /// due date and position key - absent from the request - are unchanged; the row agrees.
    /// </summary>
    /// <param name="surface">The surface the edit is sent on.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC1 - the Task PATCH route does not exist yet (Task 4, Task 5; Q1)")]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "FR-20")]
    [Trait("Requirement", "AR-20")]
    [Trait("Requirement", "AD-11")]
    [Trait("Requirement", "AD-4")]
    public async Task Editing_a_Tasks_title_answers_200_and_leaves_what_was_not_sent(IsolationSurface surface)
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        TaskRow? before = null;
        WireResponse? edited = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var token = await IssueTokenAsync(owner, owner.SpaceId);

            (_, taskId) = await ProjectWithTaskAsync(owner, "Draft the brief");
            before = await RowAsync(database, owner.SpaceId, taskId);
            edited = await EditTaskOnAsync(surface, owner, token, taskId, Body(new { title = "  Send the brief  " }));
        });

        Assert.NotNull(owner);
        Assert.NotNull(before);
        Assert.NotNull(edited);
        Assert.True(edited.StatusCode == HttpStatusCode.OK, $"{edited.StatusCode} {edited.Body}");

        using var document = JsonDocument.Parse(edited.Body);
        var root = document.RootElement;

        Assert.Equal(EditedTaskProperties, root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(taskId, root.GetProperty("id").GetGuid());
        Assert.Equal("Send the brief", root.GetProperty("title").GetString());
        Assert.Equal(before.StatusId, root.GetProperty("statusId").GetGuid());
        Assert.Equal(before.PositionKey, root.GetProperty("positionKey").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("dueDate").ValueKind);

        var after = await RowAsync(database, owner.SpaceId, taskId);

        Assert.Equal(before with { Title = "Send the brief" }, after);
    }

    /// <summary>
    /// AC1, AC7, Q1 (a), Q4 (a) - a due date sent with a non-zero offset is stored and answered as the same
    /// instant in UTC, ISO 8601 with its offset on the wire; the title and Status, absent from the request, are
    /// unchanged.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC7 - no DueDate column and no Task PATCH route yet (Task 2, Task 3, Task 5; Q4)")]
    [Trait("Requirement", "FR-20")]
    [Trait("Requirement", "AR-34")]
    public async Task A_due_date_sent_with_an_offset_is_stored_and_answered_as_the_same_instant_in_UTC()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        TaskRow? before = null;
        WireResponse? edited = null;
        var sent = new DateTimeOffset(2026, 11, 3, 9, 30, 0, TimeSpan.FromHours(5.5));

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            (_, taskId) = await ProjectWithTaskAsync(owner, "Book the venue");
            before = await RowAsync(database, owner.SpaceId, taskId);
            edited = await EditTaskAsync(owner, owner.SpaceId, taskId, RawBody("""{"dueDate":"2026-11-03T09:30:00+05:30"}"""));
        });

        Assert.NotNull(owner);
        Assert.NotNull(edited);
        Assert.True(edited.StatusCode == HttpStatusCode.OK, $"{edited.StatusCode} {edited.Body}");

        using var document = JsonDocument.Parse(edited.Body);
        var wire = document.RootElement.GetProperty("dueDate").GetString();

        // The exact serialised form: System.Text.Json's default for a UTC DateTimeOffset (Q4 records it).
        Assert.Equal("2026-11-03T04:00:00+00:00", wire);
        Assert.Equal(sent, DateTimeOffset.Parse(wire!, System.Globalization.CultureInfo.InvariantCulture));

        var stored = await DueDateAsync(database, owner.SpaceId, taskId);

        Assert.Equal(sent, stored);
        Assert.Equal(TimeSpan.Zero, stored!.Value.Offset);
        Assert.Equal(before, await RowAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// AC7, Q1 (a) - <c>dueDate: null</c> clears the due date: <c>NULL</c> is stored and the wire carries
    /// <c>"dueDate": null</c>. Anchor: the due date was set first, so the clear is about something.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC7 - no DueDate column and no Task PATCH route yet (Task 3, Task 5; Q1)")]
    [Trait("Requirement", "FR-20")]
    public async Task Sending_a_null_due_date_clears_it()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        DateTimeOffset? whileSet = null;
        WireResponse? cleared = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            (_, taskId) = await ProjectWithTaskAsync(owner, "Renew the lease");

            Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, taskId, RawBody("""{"dueDate":"2026-12-01T00:00:00+00:00"}"""))).StatusCode);
            whileSet = await DueDateAsync(database, owner.SpaceId, taskId);
            cleared = await EditTaskAsync(owner, owner.SpaceId, taskId, RawBody("""{"dueDate":null}"""));
        });

        Assert.NotNull(owner);
        Assert.NotNull(whileSet);
        Assert.Equal(HttpStatusCode.OK, cleared?.StatusCode);

        using var document = JsonDocument.Parse(cleared!.Body);

        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("dueDate").ValueKind);
        Assert.Null(await DueDateAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// AC4, AC9, Q3 (a), AD-15 - a Status in the Project's effective set is admitted, and the Task takes a fresh
    /// key after the last key of its new column: only the edited Task's key is written, the other Tasks' keys are
    /// untouched, and its old key is not reused.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - no Task PATCH route and no key placement on a Status change yet (Task 5, Task 6; Q3)")]
    [Trait("Requirement", "FR-20")]
    [Trait("Requirement", "AD-15")]
    [Trait("Requirement", "AD-16")]
    public async Task A_Status_change_appends_the_Task_after_the_last_key_of_its_new_column_and_writes_only_its_key()
    {
        var database = Available();
        Actor? owner = null;
        var moved = Guid.Empty;
        IReadOnlyList<StatusRow> statuses = [];
        IReadOnlyList<TaskRow> before = [];
        WireResponse? edited = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Columns");

            statuses = await StatusesAsync(database, owner.SpaceId);

            // Two Tasks already in the destination column, so "after the last" is about a real last key.
            foreach (var title in AlreadyDoneTitles)
            {
                var id = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, title));
                Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, id, Body(new { statusId = statuses[2].Id }))).StatusCode);
            }

            moved = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Moving across"));
            _ = await CreateTaskAsync(owner, owner.SpaceId, projectId, "Staying behind");

            before = await TaskRowsAsync(database, owner.SpaceId);
            edited = await EditTaskAsync(owner, owner.SpaceId, moved, Body(new { statusId = statuses[2].Id }));
        });

        Assert.NotNull(owner);
        Assert.True(edited?.StatusCode == HttpStatusCode.OK, $"{edited?.StatusCode} {edited?.Body}");

        var after = await TaskRowsAsync(database, owner.SpaceId);
        var oldRow = before.Single(row => row.Id == moved);
        var newRow = after.Single(row => row.Id == moved);
        var lastInDestination = before.Where(row => row.StatusId == statuses[2].Id).Select(row => row.PositionKey).Max(StringComparer.Ordinal)!;

        Assert.Equal(statuses[2].Id, newRow.StatusId);
        Assert.True(string.CompareOrdinal(newRow.PositionKey, lastInDestination) > 0, $"{newRow.PositionKey} is not after {lastInDestination}.");
        Assert.NotEqual(oldRow.PositionKey, newRow.PositionKey);
        Assert.Equal(
            before.Where(row => row.Id != moved).OrderBy(row => row.Id),
            after.Where(row => row.Id != moved).OrderBy(row => row.Id));

        using var document = JsonDocument.Parse(edited!.Body);

        Assert.Equal(newRow.PositionKey, document.RootElement.GetProperty("positionKey").GetString());
    }

    /// <summary>
    /// AC9, Q3 (a) - setting the Status a Task already holds is admitted and writes no key: the position key is
    /// unchanged.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC9 - no Task PATCH route yet (Task 5; Q3)")]
    [Trait("Requirement", "AD-15")]
    [Trait("Priority", "P1")]
    public async Task Setting_the_Status_a_Task_already_holds_keeps_its_key()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        TaskRow? before = null;
        WireResponse? edited = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            (_, taskId) = await ProjectWithTaskAsync(owner, "Stay put");
            before = await RowAsync(database, owner.SpaceId, taskId);
            edited = await EditTaskAsync(owner, owner.SpaceId, taskId, Body(new { statusId = before!.StatusId }));
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.OK, edited?.StatusCode);
        Assert.Equal(before, await RowAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// AC4, AC11, Q5 (a), Q13 (a), I-13 - a Status outside the Task's Project's effective set is refused
    /// <c>400 invalid-task-status</c> whether it is a random id or another Space's real Status id, the two
    /// refusals are byte-identical, nothing is changed, no refusal row is written, and neither body echoes the
    /// Status id sent. The foreign Status must be refused before any write, so the composite FK
    /// <c>(SpaceId, StatusId)</c> is never what refuses it (that would be a <c>500</c>). Anchor: the Owner's own
    /// third Status is admitted afterwards.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC4 - no Task PATCH route and no effective-set check yet (Task 2, Task 5; Q5, Q13)")]
    [Trait("Requirement", "FR-20")]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "AR-34")]
    public async Task A_Status_outside_the_effective_set_is_refused_400_identically_for_a_random_and_a_foreign_id()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        var randomStatus = Guid.NewGuid();
        var foreignStatus = Guid.Empty;
        TaskRow? before = null;
        TaskRow? afterRefusals = null;
        WireResponse? random = null;
        WireResponse? foreign = null;
        WireResponse? anchor = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var other = await RegisterAsync(client, database);

            foreignStatus = (await StatusesAsync(database, other.SpaceId))[2].Id;
            (_, taskId) = await ProjectWithTaskAsync(owner, "Check the Status");
            before = await RowAsync(database, owner.SpaceId, taskId);

            random = await EditTaskAsync(owner, owner.SpaceId, taskId, Body(new { statusId = randomStatus }));
            foreign = await EditTaskAsync(owner, owner.SpaceId, taskId, Body(new { statusId = foreignStatus }));
            afterRefusals = await RowAsync(database, owner.SpaceId, taskId);
            anchor = await EditTaskAsync(owner, owner.SpaceId, taskId, Body(new { statusId = (await StatusesAsync(database, owner.SpaceId))[2].Id }));
        });

        Assert.NotNull(owner);
        Assert.NotNull(random);
        Assert.NotNull(foreign);
        Assert.True(random.StatusCode == HttpStatusCode.BadRequest, $"{random.StatusCode} {random.Body}");
        Assert.Equal(TaskEditScaffold.InvalidTaskStatus, Problem(random)?.Type);
        Assert.Equal(random.StatusCode, foreign.StatusCode);
        Assert.Equal(random.ContentType, foreign.ContentType);
        Assert.Equal(random.Body, foreign.Body);
        Assert.DoesNotContain(randomStatus.ToString("D"), random.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(foreignStatus.ToString("D"), foreign.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, afterRefusals);
        Assert.Empty(await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId));
        Assert.Equal(HttpStatusCode.OK, anchor?.StatusCode);
    }

    /// <summary>
    /// AC11, Q1 (a) - an edit whose title is empty, blank after trimming or longer than 200 characters, or
    /// present as <c>null</c>, is refused <c>400 invalid-task-title</c> and nothing is changed. Anchor: exactly
    /// 200 characters is admitted.
    /// </summary>
    /// <param name="titleJson">The title member's JSON value.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC11 - no Task PATCH route yet (Task 5; Q1)")]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    [InlineData("null")]
    [InlineData("LONG")]
    [Trait("Priority", "P1")]
    public async Task An_invalid_title_in_an_edit_is_refused_400_and_nothing_is_changed(string titleJson)
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        TaskRow? before = null;
        TaskRow? afterRefusal = null;
        WireResponse? refused = null;
        WireResponse? anchor = null;
        var value = titleJson == "LONG" ? $"\"{new string('t', TaskTitle.MaxLength + 1)}\"" : titleJson;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            (_, taskId) = await ProjectWithTaskAsync(owner, "Keep this title");
            before = await RowAsync(database, owner.SpaceId, taskId);
            refused = await EditTaskAsync(owner, owner.SpaceId, taskId, RawBody($$"""{"title":{{value}}}"""));
            afterRefusal = await RowAsync(database, owner.SpaceId, taskId);
            anchor = await EditTaskAsync(owner, owner.SpaceId, taskId, Body(new { title = new string('t', TaskTitle.MaxLength) }));
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.BadRequest, refused?.StatusCode);
        Assert.Equal(ProblemTypes.InvalidTaskTitle, Problem(refused!)?.Type);
        Assert.Equal(before, afterRefusal);
        Assert.Equal(HttpStatusCode.OK, anchor?.StatusCode);
    }

    /// <summary>
    /// AC11, Q4 (a) - a due date before 2000 or after 2100, or one that is not an ISO 8601 instant, is refused
    /// <c>400 invalid-due-date</c>, never a <c>500</c>, and nothing is changed. Anchor: the first instant of 2000
    /// is admitted.
    /// </summary>
    /// <param name="dueDateJson">The dueDate member's JSON value.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC11 - no due-date rule and no Task PATCH route yet (Task 2, Task 5; Q4)")]
    [InlineData("\"1999-12-31T23:59:59+00:00\"")]
    [InlineData("\"2101-01-01T00:00:00+00:00\"")]
    [InlineData("\"next Tuesday\"")]
    [InlineData("12")]
    [Trait("Requirement", "AR-34")]
    [Trait("Priority", "P1")]
    public async Task An_invalid_due_date_is_refused_400_and_nothing_is_changed(string dueDateJson)
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        DateTimeOffset? afterRefusal = DateTimeOffset.MinValue;
        WireResponse? refused = null;
        WireResponse? anchor = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            (_, taskId) = await ProjectWithTaskAsync(owner, "Dated");
            refused = await EditTaskAsync(owner, owner.SpaceId, taskId, RawBody($$"""{"dueDate":{{dueDateJson}}}"""));
            afterRefusal = await DueDateAsync(database, owner.SpaceId, taskId);
            anchor = await EditTaskAsync(owner, owner.SpaceId, taskId, Body(new { dueDate = TaskEditScaffold.EarliestDueDate }));
        });

        Assert.NotNull(owner);
        Assert.True(refused?.StatusCode == HttpStatusCode.BadRequest, $"{refused?.StatusCode} {refused?.Body}");
        Assert.Equal(TaskEditScaffold.InvalidDueDate, Problem(refused!)?.Type);
        Assert.Null(afterRefusal);
        Assert.Equal(HttpStatusCode.OK, anchor?.StatusCode);
    }

    /// <summary>
    /// AC11, Q1 (a) - a body that cannot be read, a body naming no member this route reads, and
    /// <c>statusId: null</c> are each a <c>400</c>, never a <c>500</c>, and change nothing.
    /// </summary>
    /// <param name="body">The raw body.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC11 - no Task PATCH route yet (Task 5; Q1)")]
    [InlineData("{\"title\":")]
    [InlineData("{}")]
    [InlineData("{\"description\":\"no such member here\"}")]
    [InlineData("{\"statusId\":null}")]
    [Trait("Priority", "P1")]
    public async Task An_unreadable_or_empty_edit_is_refused_400_and_never_500(string body)
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        TaskRow? before = null;
        WireResponse? refused = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            (_, taskId) = await ProjectWithTaskAsync(owner, "Untouched");
            before = await RowAsync(database, owner.SpaceId, taskId);
            refused = await EditTaskAsync(owner, owner.SpaceId, taskId, RawBody(body));
        });

        Assert.NotNull(owner);
        Assert.True(refused?.StatusCode == HttpStatusCode.BadRequest, $"{refused?.StatusCode} {refused?.Body}");
        Assert.Equal(before, await RowAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// AC3, C-6, V-1 - a Viewer added to an Owner's Space is refused <c>403 insufficient-role</c> naming
    /// <c>manage-tasks</c> on its Session and on its Token, each recorded once as <c>InsufficientRole</c>, and the
    /// Task is unchanged, whatever any interface offered. Anchor: a Member of the same Space edits it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 - no Task PATCH route yet (Task 5)")]
    [Trait("Requirement", "AR-11")]
    [Trait("Requirement", "AD-20")]
    [Trait("Requirement", "AD-4")]
    public async Task A_Viewer_is_refused_403_on_edit_on_both_surfaces_and_nothing_is_changed()
    {
        var database = Available();
        Actor? owner = null;
        Actor? viewer = null;
        var taskId = Guid.Empty;
        WireResponse? bySession = null;
        WireResponse? byToken = null;
        TaskRow? afterRefusals = null;
        WireResponse? anchor = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            viewer = await RegisterAsync(client, database);
            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, viewer.AccountId, "Viewer");
            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            (_, taskId) = await ProjectWithTaskAsync(owner, "Owner's Task");
            var token = await IssueTokenAsync(viewer, owner.SpaceId);

            bySession = await EditTaskAsync(viewer, owner.SpaceId, taskId, Body(new { title = "Viewer's attempt" }));
            byToken = await EditTaskWithTokenAsync(viewer, token, owner.SpaceId, taskId, Body(new { title = "Viewer's script" }));
            afterRefusals = await RowAsync(database, owner.SpaceId, taskId);
            anchor = await EditTaskAsync(member, owner.SpaceId, taskId, Body(new { title = "Member's edit" }));
        });

        Assert.NotNull(owner);
        Assert.NotNull(viewer);

        foreach (var refused in new[] { bySession!, byToken! })
        {
            Assert.True(refused.StatusCode == HttpStatusCode.Forbidden, $"{refused.StatusCode} {refused.Body}");
            Assert.Equal(ProblemTypes.InsufficientRole, Problem(refused)?.Type);
            Assert.Equal(StableCode.Of(Capability.ManageTasks), Problem(refused)?.Capability);
        }

        Assert.Equal("Owner's Task", afterRefusals?.Title);
        Assert.Equal([InsufficientRoleKind, InsufficientRoleKind], await RefusalKindsAsync(database, viewer.AccountId, owner.SpaceId));
        Assert.Equal(HttpStatusCode.OK, anchor?.StatusCode);
        Assert.Equal("Member's edit", (await RowAsync(database, owner.SpaceId, taskId))?.Title);
    }

    /// <summary>
    /// AC8, AD-3, AD-20 - a Task id that matches nothing in the active Space answers the boundary's
    /// <c>404 not-available</c> on <c>GET</c> and on <c>PATCH</c>, byte-identical to a Space that does not exist,
    /// and records nothing. Anchor: the Owner's real Task is read.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC8 - the Task GET and PATCH routes do not exist yet (Task 4, Task 5)")]
    [Trait("Requirement", "AD-20")]
    public async Task An_unknown_Task_id_answers_the_boundary_404_on_read_and_edit_and_records_nothing()
    {
        var database = Available();
        Actor? owner = null;
        var answers = new List<(WireResponse InSpace, WireResponse AbsentSpace)>();
        IReadOnlyList<string> refusals = [];
        WireResponse? anchor = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var (_, taskId) = await ProjectWithTaskAsync(owner, "Present");

            answers.Add((await ViewTaskAsync(owner, owner.SpaceId, Guid.NewGuid()), await ViewTaskAsync(owner, Guid.NewGuid(), Guid.NewGuid())));
            answers.Add((
                await EditTaskAsync(owner, owner.SpaceId, Guid.NewGuid(), Body(new { title = "Nowhere" })),
                await EditTaskAsync(owner, Guid.NewGuid(), Guid.NewGuid(), Body(new { title = "Nowhere" }))));

            refusals = await RefusalKindsAsync(database, owner.AccountId, owner.SpaceId);
            anchor = await ViewTaskAsync(owner, owner.SpaceId, taskId);
        });

        Assert.NotNull(owner);
        Assert.Equal(2, answers.Count);

        foreach (var (inSpace, absentSpace) in answers)
        {
            Assert.Equal(HttpStatusCode.NotFound, inSpace.StatusCode);
            Assert.Equal(absentSpace.StatusCode, inSpace.StatusCode);
            Assert.Equal(absentSpace.ContentType, inSpace.ContentType);
            Assert.Equal(absentSpace.Body, inSpace.Body);
            Assert.Equal(absentSpace.HeaderNames, inSpace.HeaderNames);
            Assert.Equal(ProblemTypes.NotAvailable, Problem(inSpace)?.Type);
        }

        Assert.Empty(refusals);
        Assert.Equal(HttpStatusCode.OK, anchor?.StatusCode);
        Assert.Equal(["Present"], (await TaskRowsAsync(database, owner.SpaceId)).Select(row => row.Title));
    }

    /// <summary>
    /// AC3, AC7 - <c>GET</c> answers the Task with its due date; a Viewer may read it on its Session and on its
    /// Token (<c>read-projects-and-tasks</c>), and both answers are the Owner's, byte for byte.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 - the Task GET route does not exist yet (Task 4, Task 5)")]
    [Trait("Requirement", "FR-20")]
    [Trait("Requirement", "AD-4")]
    public async Task A_Task_is_read_with_its_due_date_and_a_Viewer_reads_what_the_Owner_reads()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? byOwner = null;
        WireResponse? byViewerSession = null;
        WireResponse? byViewerToken = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var viewer = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, viewer.AccountId, "Viewer");

            var (_, taskId) = await ProjectWithTaskAsync(owner, "Read me");
            Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, taskId, RawBody("""{"dueDate":"2026-11-03T00:00:00+00:00"}"""))).StatusCode);

            var token = await IssueTokenAsync(viewer, owner.SpaceId);

            byOwner = await ViewTaskAsync(owner, owner.SpaceId, taskId);
            byViewerSession = await ViewTaskAsync(viewer, owner.SpaceId, taskId);
            byViewerToken = await ViewTaskWithTokenAsync(viewer, token, owner.SpaceId, taskId);
        });

        Assert.NotNull(owner);
        Assert.NotNull(byOwner);
        Assert.True(byOwner.StatusCode == HttpStatusCode.OK, $"{byOwner.StatusCode} {byOwner.Body}");

        using var document = JsonDocument.Parse(byOwner.Body);

        Assert.Equal(EditedTaskProperties, document.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("2026-11-03T00:00:00+00:00", document.RootElement.GetProperty("dueDate").GetString());
        Assert.Equal(byOwner.Body, byViewerSession?.Body);
        Assert.Equal(byOwner.Body, byViewerToken?.Body);
    }

    /// <summary>
    /// AC4, S-2, AD-17 - after a run of edits (Status changes there and back, a refused foreign Status, title and
    /// due-date edits), no Task in the Space holds a Status outside its Project's effective set. Anchored: the
    /// Space holds Tasks in every column.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC4 - no Task PATCH route yet (Task 5, Task 10 S-2)")]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "AR-24")]
    public async Task After_a_run_of_edits_no_Task_holds_a_Status_outside_its_effective_set()
    {
        var database = Available();
        Actor? owner = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var other = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Churn");
            var statuses = await StatusesAsync(database, owner.SpaceId);
            var foreign = (await StatusesAsync(database, other.SpaceId))[1].Id;

            for (var index = 0; index < 6; index++)
            {
                var id = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, $"Churn {index}"));

                Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, id, Body(new { statusId = statuses[index % 3].Id, title = $"Churned {index}" }))).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, (await EditTaskAsync(owner, owner.SpaceId, id, Body(new { statusId = foreign }))).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await EditTaskAsync(owner, owner.SpaceId, id, Body(new { dueDate = new DateTimeOffset(2026, 12, 1 + index, 0, 0, 0, TimeSpan.Zero) }))).StatusCode);
            }
        });

        Assert.NotNull(owner);

        var rows = await TaskRowsAsync(database, owner.SpaceId);

        Assert.Equal(3, rows.Select(row => row.StatusId).Distinct().Count());
        Assert.Equal(0, await TasksOutsideTheirEffectiveSetAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// Task 5, Task 12 - an edit adds no Task, so it is admitted in a Project that already holds the
    /// Tasks-per-Project limit: the edit route must not consult the bound. Anchor: a creation there is refused
    /// <c>409</c>, so the Project is full.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1 - no Task PATCH route yet; this is the ConsultsBound plant's case (Task 5, Task 12)")]
    [Trait("Requirement", "AR-30")]
    [Trait("Requirement", "AD-25")]
    public async Task An_edit_is_admitted_in_a_Project_that_holds_the_Task_limit()
    {
        var database = Available();
        Actor? owner = null;
        WireResponse? creation = null;
        WireResponse? edit = null;

        await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Full");
            var taskId = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "The one to edit"));

            var first = (await StatusesAsync(database, owner.SpaceId))[0];
            await SeedTasksAsync(database, owner.SpaceId, projectId, first.Id, Yello.Domain.Bounds.BoundRegistry.For(Yello.Domain.Bounds.Bound.TasksPerProject).Limit - 1);

            creation = await CreateTaskAsync(owner, owner.SpaceId, projectId, "One too many");
            edit = await EditTaskAsync(owner, owner.SpaceId, taskId, Body(new { title = "Edited while full" }));
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.Conflict, creation?.StatusCode);
        Assert.True(edit?.StatusCode == HttpStatusCode.OK, $"{edit?.StatusCode} {edit?.Body}");
    }
}
