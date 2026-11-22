using System.Net;
using Xunit;
using Yello.Contracts;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;
using Yello.Contracts.Tasks;
using Yello.Merge;
using Yello.Tests.Shared;
using static Yello.Tests.Slices.Tasks.TaskDescriptionSliceWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Tasks;

/// <summary>
/// A Task's description over the wire: a batch of merge changes appends one row per newly admitted change and re-projects in
/// the same request; each change is admitted or refused on its own; a resend appends nothing; the answer carries what the
/// client lacks and never text; the read serves the projection as a column and the state an editor bootstraps from; a Viewer
/// is refused before the handler; deleting the Task, its Project or its Space removes the log, and moving it keeps it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The write path carries changes, never text.</b> Every batch here is made by a replica of the real strategy
/// (<see cref="TaskDescriptionSliceWire"/>), and the routes, problem types and member names are the stand-ins in
/// <see cref="TaskDescriptionWire"/>.
/// </para>
/// <para>
/// <b>Never assert a merge order the requirement does not fix.</b> Where two editors write concurrently, the projection is
/// compared with the text the log rebuilds to, and each editor's text is asserted present, never its position.
/// </para>
/// </remarks>
public sealed partial class TaskEndpointTests
{
    private const string NoWritePath = "the description change route is not mapped";

    private const string NoReadPath = "the description read route is not mapped";

    private static readonly string[] ChangeAnswerMembers = ["missing", "refused", "version"];

    private static readonly string[] ReadAnswerMembers = ["state", "text", "version"];

    /// <summary>
    /// An Owner's batch of two changes answers <c>200</c> with nothing refused: two <c>Change</c> rows in order, each carrying
    /// the writing Account, the name captured at write time and a UTC time; the projection holds the merged text, read in the
    /// next request; and the answer carries no text.
    /// </summary>
    /// <param name="surface">The surface the batch is sent on.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC1, AC2, AC3 - " + NoWritePath)]
    [InlineData(IsolationSurface.Session)]
    [InlineData(IsolationSurface.ApiToken)]
    [Trait("Requirement", "AR-19")]
    [Trait("Requirement", "AD-13")]
    [Trait("Requirement", "AD-11")]
    [Trait("Requirement", "FR-31")]
    public async Task A_batch_appends_one_row_per_change_and_the_projection_holds_the_merged_text(IsolationSurface surface)
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        WireResponse? answer = null;
        WireResponse? read = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var token = await IssueTokenAsync(owner, owner.SpaceId);
            var editor = NewEditor();

            taskId = await TaskAsync(owner);
            answer = await SendChangesOnAsync(surface, owner, token, taskId, BatchOf(editor, editor.Insert(0, "Hello"), editor.Insert(5, " world")));
            read = await ReadAsync(owner, owner.SpaceId, taskId);
        });

        Assert.NotNull(owner);
        AssertAnswered(answer, refused: 0);
        Assert.Equal(ChangeAnswerMembers, TaskDescriptionWire.MembersOf(answer!.Body));
        Assert.DoesNotContain("Hello", answer.Body, StringComparison.Ordinal);

        var rows = await RowsAsync(database, owner.SpaceId, taskId);

        Assert.Equal([1L, 2L], rows.Select(row => row.Sequence));
        Assert.All(rows, row =>
        {
            Assert.Equal(ChangeKind, row.Kind);
            Assert.Equal(owner.AccountId, row.AuthorAccountId);
            Assert.False(string.IsNullOrWhiteSpace(row.AuthorName));
            Assert.Equal(TimeSpan.Zero, row.RecordedAt.Offset);
        });

        Assert.Equal("Hello world", await DescriptionAsync(database, owner.SpaceId, taskId));
        Assert.Equal("Hello world", TaskDescriptionWire.TextIn(read!.Body));
    }

    /// <summary>
    /// A change already held appends no row: the same batch sent twice, and one change twice inside one batch, each answer
    /// <c>200</c> with nothing refused, and the log still holds one row per distinct change.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 - " + NoWritePath)]
    [Trait("Requirement", "AR-19")]
    [Trait("Requirement", "FR-33")]
    public async Task A_resent_change_appends_no_row()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        var answers = new List<WireResponse>();

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var editor = NewEditor();
            var first = editor.Insert(0, "once");
            var second = editor.Insert(4, " only");

            taskId = await TaskAsync(owner);
            answers.Add(await SendChangesAsync(owner, owner.SpaceId, taskId, BatchOf(editor, first, second)));
            answers.Add(await SendChangesAsync(owner, owner.SpaceId, taskId, BatchOf(editor, first, second)));
            answers.Add(await SendChangesAsync(owner, owner.SpaceId, taskId, BatchOf(editor, second, second)));
        });

        Assert.NotNull(owner);
        Assert.All(answers, answer => AssertAnswered(answer, refused: 0));
        Assert.Equal(2, (await RowsAsync(database, owner.SpaceId, taskId)).Count);
        Assert.Equal("once only", await DescriptionAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// Each change is admitted or refused on its own: in one batch, a truncated change and a change of an unsupported format
    /// are refused and write nothing, while the two well-formed changes beside them are appended and projected.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3, AD-11 - " + NoWritePath)]
    [Trait("Requirement", "AD-11")]
    [Trait("Requirement", "AR-20")]
    [Trait("Requirement", "AR-19")]
    public async Task A_refused_change_writes_nothing_and_the_changes_beside_it_are_kept()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        WireResponse? answer = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var first = NewEditor();
            var second = NewEditor();
            var kept = first.Insert(0, "kept");
            var truncated = first.Insert(4, " lost").Encoded.ToArray()[..^1];
            var unsupported = second.Insert(0, "never").Encoded.ToArray();
            var alongside = NewEditor().Insert(0, "also");

            unsupported[0] = byte.CreateTruncating(unsupported[0] + 1);
            taskId = await TaskAsync(owner);
            answer = await SendChangesAsync(
                owner,
                owner.SpaceId,
                taskId,
                TaskDescriptionWire.Body([kept.Encoded.ToArray(), truncated, unsupported, alongside.Encoded.ToArray()], first.Version.Encoded.ToArray()));
        });

        Assert.NotNull(owner);
        AssertAnswered(answer, refused: 2);

        var rows = await RowsAsync(database, owner.SpaceId, taskId);
        var projection = await DescriptionAsync(database, owner.SpaceId, taskId);

        Assert.Equal(2, rows.Count);
        Assert.Equal(RebuiltText(rows), projection);
        Assert.Contains("kept", projection, StringComparison.Ordinal);
        Assert.Contains("also", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("lost", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("never", projection, StringComparison.Ordinal);
    }

    /// <summary>
    /// A batch whose every change is refused still answers <c>200</c>, lists every refusal so the client can withdraw them,
    /// and writes nothing: no row, and the projection is still empty.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3 - " + NoWritePath)]
    [Trait("Requirement", "AD-11")]
    [Trait("Requirement", "AR-20")]
    public async Task A_batch_whose_every_change_is_refused_answers_200_and_writes_nothing()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        WireResponse? answer = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var editor = NewEditor();
            var first = editor.Insert(0, "gone").Encoded.ToArray()[..^1];
            var second = editor.Insert(0, "too").Encoded.ToArray()[..^1];

            taskId = await TaskAsync(owner);
            answer = await SendChangesAsync(owner, owner.SpaceId, taskId, TaskDescriptionWire.Body([first, second], editor.Version.Encoded.ToArray()));
        });

        Assert.NotNull(owner);
        AssertAnswered(answer, refused: 2);
        Assert.Empty(await RowsAsync(database, owner.SpaceId, taskId));
        Assert.Equal(string.Empty, await DescriptionAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// The answer carries the changes the client's version lacks: a second editor that never saw the first's text writes
    /// its own, and applying what its answer says it lacks gives it the projection. Its answer names no text member.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3, AD-11 - " + NoWritePath)]
    [Trait("Requirement", "AD-11")]
    [Trait("Requirement", "FR-31")]
    [Trait("Requirement", "FR-33")]
    public async Task The_answer_carries_what_the_clients_version_lacks_and_applying_it_gives_the_projection()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        ITextReplica? late = null;
        WireResponse? answer = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var early = NewEditor();

            late = NewEditor();
            taskId = await TaskAsync(owner);
            AssertAnswered(await SendChangesAsync(owner, owner.SpaceId, taskId, BatchOf(early, early.Insert(0, "alpha"))), refused: 0);
            answer = await SendChangesAsync(owner, owner.SpaceId, taskId, BatchOf(late, late.Insert(0, "beta")));
        });

        Assert.NotNull(owner);
        Assert.NotNull(late);
        AssertAnswered(answer, refused: 0);
        Assert.Equal(ChangeAnswerMembers, TaskDescriptionWire.MembersOf(answer!.Body));

        var missing = TaskDescriptionWire.MissingIn(answer.Body);

        Assert.Single(missing);

        foreach (var bytes in missing)
        {
            var change = Strategy.DecodeChange(bytes);

            Assert.Equal(ChangeAdmission.Admitted, change.Admission);
            Assert.Equal(ChangeAdmission.Admitted, late.Apply(change.Value!));
        }

        Assert.Equal(await DescriptionAsync(database, owner.SpaceId, taskId), late.Text);
        Assert.Contains("alpha", late.Text, StringComparison.Ordinal);
        Assert.Contains("beta", late.Text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A body that is not a change batch - not JSON, not an object, no changes, a change that is not base64, a version
    /// missing, or whole text in place of changes - is refused <c>400</c> with the batch's own problem type, and nothing is
    /// written.
    /// </summary>
    /// <param name="json">The body.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC3, AD-11 - " + NoWritePath)]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"changes":"AQ==","version":"AQ=="}""")]
    [InlineData("""{"changes":["not base64!"],"version":"AQ=="}""")]
    [InlineData("""{"changes":["AQ=="]}""")]
    [InlineData("""{"text":"whole text is never truth"}""")]
    [Trait("Requirement", "AD-11")]
    [Trait("Requirement", "AR-20")]
    [Trait("Priority", "P1")]
    public async Task A_body_that_is_not_a_change_batch_is_refused_400_and_nothing_is_written(string json)
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        WireResponse? answer = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            taskId = await TaskAsync(owner);
            answer = await SendChangesAsync(owner, owner.SpaceId, taskId, TaskDescriptionWire.RawBody(json));
        });

        Assert.NotNull(owner);
        Assert.NotNull(answer);
        Assert.True(answer.StatusCode == HttpStatusCode.BadRequest, $"{answer.StatusCode} {answer.Body}");
        Assert.Equal(TaskDescriptionWire.InvalidChangesType, Problem(answer)?.Type);
        Assert.Empty(await RowsAsync(database, owner.SpaceId, taskId));
        Assert.Equal(string.Empty, await DescriptionAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// The transport cap: a batch of one more change than the cap, and a body over the byte cap, are each refused <c>400</c>
    /// with the cap's problem type and write nothing. Anchor: a batch of exactly the cap is admitted.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3, Q9 - " + NoWritePath)]
    [Trait("Requirement", "AD-11")]
    [Trait("Requirement", "NFR-8")]
    [Trait("Priority", "P1")]
    public async Task A_batch_over_the_cap_is_refused_400_and_nothing_is_written()
    {
        var database = Available();
        Actor? owner = null;
        var (tooMany, tooLarge, atTheCap) = (Guid.Empty, Guid.Empty, Guid.Empty);
        var answers = new Dictionary<string, WireResponse>(StringComparer.Ordinal);

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            tooMany = await TaskAsync(owner, "Too many");
            tooLarge = await TaskAsync(owner, "Too large");
            atTheCap = await TaskAsync(owner, "At the cap");

            answers["many"] = await SendChangesAsync(owner, owner.SpaceId, tooMany, Keystrokes(TaskDescriptionWire.MaximumChangesPerBatch + 1));
            answers["cap"] = await SendChangesAsync(owner, owner.SpaceId, atTheCap, Keystrokes(TaskDescriptionWire.MaximumChangesPerBatch));

            var large = NewEditor();

            answers["large"] = await SendChangesAsync(owner, owner.SpaceId, tooLarge, BatchOf(large, large.Insert(0, new string('x', TaskDescriptionWire.MaximumBodyBytes))));
        });

        Assert.NotNull(owner);

        foreach (var name in new[] { "many", "large" })
        {
            Assert.True(answers[name].StatusCode == HttpStatusCode.BadRequest, $"{name}: {answers[name].StatusCode} {answers[name].Body}");
            Assert.Equal(TaskDescriptionWire.BatchTooLargeType, Problem(answers[name])?.Type);
        }

        Assert.Empty(await RowsAsync(database, owner.SpaceId, tooMany));
        Assert.Empty(await RowsAsync(database, owner.SpaceId, tooLarge));
        AssertAnswered(answers["cap"], refused: 0);
        Assert.Equal(TaskDescriptionWire.MaximumChangesPerBatch, (await RowsAsync(database, owner.SpaceId, atTheCap)).Count);
    }

    /// <summary>
    /// A Viewer is refused <c>403 insufficient-role</c> naming the capability, on a Session and on a Token, before the
    /// handler: no row, no projection. The same Viewer reads the description. Anchor: a Member's identical batch is admitted.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3, AC8, X-9 - " + NoWritePath)]
    [Trait("Requirement", "FR-16")]
    [Trait("Requirement", "FR-31")]
    [Trait("Requirement", "AD-1")]
    public async Task A_Viewer_is_refused_on_both_surfaces_and_writes_nothing_and_still_reads_the_description()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        var refusals = new List<WireResponse>();
        WireResponse? memberAnswer = null;
        WireResponse? viewerRead = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);

            var viewer = await RegisterAsync(client, database);
            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, viewer.AccountId, "Viewer");
            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");

            var viewerToken = await IssueTokenAsync(viewer, owner.SpaceId);

            taskId = await TaskAsync(owner);

            var editor = NewEditor();
            var change = editor.Insert(0, "viewer text");

            refusals.Add(await SendChangesAsync(viewer, owner.SpaceId, taskId, BatchOf(editor, change)));
            refusals.Add(await SendChangesWithTokenAsync(viewer, viewerToken, owner.SpaceId, taskId, BatchOf(editor, change)));
            viewerRead = await ReadAsync(viewer, owner.SpaceId, taskId);
            var memberEditor = NewEditor();

            memberAnswer = await SendChangesAsync(member, owner.SpaceId, taskId, BatchOf(memberEditor, memberEditor.Insert(0, "member text")));
        });

        Assert.NotNull(owner);
        Assert.All(refusals, refusal =>
        {
            Assert.True(refusal.StatusCode == HttpStatusCode.Forbidden, $"{refusal.StatusCode} {refusal.Body}");
            Assert.Equal(ProblemTypes.InsufficientRole, Problem(refusal)?.Type);
            Assert.Equal(TaskDescriptionWire.EditCapabilityCode, Problem(refusal)?.Capability);
        });

        Assert.Equal(HttpStatusCode.OK, viewerRead?.StatusCode);
        AssertAnswered(memberAnswer, refused: 0);

        var rows = await RowsAsync(database, owner.SpaceId, taskId);

        Assert.Single(rows);
        Assert.DoesNotContain("viewer text", await DescriptionAsync(database, owner.SpaceId, taskId), StringComparison.Ordinal);
    }

    /// <summary>
    /// A Task that is not in the Space is the floored not-found on both routes, byte-identical to a random id's.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3, AC4 - " + NoWritePath)]
    [Trait("Requirement", "AD-3")]
    [Trait("Requirement", "AD-20")]
    public async Task An_unknown_Task_answers_the_not_found_on_both_routes()
    {
        var database = Available();
        var answers = new List<WireResponse>();

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var editor = NewEditor();
            var missing = Guid.NewGuid();

            answers.Add(await SendChangesAsync(owner, owner.SpaceId, missing, BatchOf(editor, editor.Insert(0, "nowhere"))));
            answers.Add(await ReadAsync(owner, owner.SpaceId, missing));
        });

        Assert.All(answers, answer =>
        {
            Assert.True(answer.StatusCode == HttpStatusCode.NotFound, $"{answer.StatusCode} {answer.Body}");
            Assert.Equal(ProblemTypes.NotAvailable, Problem(answer)?.Type);
        });
    }

    /// <summary>
    /// The read answers the projection read as a column and a state that loads to the same text, with the server's version:
    /// exactly <c>{ state, text, version }</c>. A Task never described reads as the empty text with a state that loads empty.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC4, Q3 - " + NoReadPath)]
    [Trait("Requirement", "AR-19")]
    [Trait("Requirement", "AD-13")]
    public async Task The_read_answers_the_projection_and_a_state_that_rebuilds_it()
    {
        var database = Available();
        WireResponse? described = null;
        WireResponse? blank = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var editor = NewEditor();
            var taskId = await TaskAsync(owner);
            var untouched = await TaskAsync(owner, "Never described");

            AssertAnswered(await SendChangesAsync(owner, owner.SpaceId, taskId, BatchOf(editor, editor.Insert(0, "line one\nline two 😀"))), refused: 0);
            described = await ReadAsync(owner, owner.SpaceId, taskId);
            blank = await ReadAsync(owner, owner.SpaceId, untouched);
        });

        Assert.Equal(HttpStatusCode.OK, described?.StatusCode);
        Assert.Equal(ReadAnswerMembers, TaskDescriptionWire.MembersOf(described!.Body));
        Assert.Equal("line one\nline two 😀", TaskDescriptionWire.TextIn(described.Body));
        Assert.Equal("line one\nline two 😀", EditorFrom(described).Text);

        Assert.Equal(HttpStatusCode.OK, blank?.StatusCode);
        Assert.Equal(string.Empty, TaskDescriptionWire.TextIn(blank!.Body));
        Assert.Equal(string.Empty, EditorFrom(blank).Text);
    }

    /// <summary>
    /// A title edit never reaches the projection, and the Task answer gains no description member: Board and List pages carry
    /// no description bytes.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC2, AC4 - " + NoWritePath)]
    [Trait("Requirement", "AD-13")]
    [Trait("Requirement", "NFR-5")]
    public async Task A_title_edit_leaves_the_projection_alone_and_the_Task_answer_carries_no_description()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        WireResponse? edited = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var editor = NewEditor();

            taskId = await TaskAsync(owner);
            AssertAnswered(await SendChangesAsync(owner, owner.SpaceId, taskId, BatchOf(editor, editor.Insert(0, "stays put"))), refused: 0);
            edited = await TaskEditWire.EditTaskAsync(owner, owner.SpaceId, taskId, TaskEditWire.Body(new { title = "Renamed" }));
        });

        Assert.NotNull(owner);
        Assert.Equal(HttpStatusCode.OK, edited?.StatusCode);
        Assert.DoesNotContain("description", edited!.Body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stays put", edited.Body, StringComparison.Ordinal);
        Assert.Equal("stays put", await DescriptionAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// Deleting the Task, its Project or its Space removes its log by the composite cascade, with no store change. Anchor:
    /// the log held rows before.
    /// </summary>
    /// <param name="what">What is deleted.</param>
    /// <returns>A task.</returns>
    [Theory(Skip = "red: AC1 - " + NoWritePath)]
    [InlineData("task")]
    [InlineData("project")]
    [InlineData("space")]
    [Trait("Requirement", "AR-19")]
    [Trait("Requirement", "FR-41")]
    [Trait("Priority", "P1")]
    public async Task Deleting_the_Task_its_Project_or_its_Space_removes_its_log(string what)
    {
        var database = Available();
        Actor? owner = null;
        var before = 0;
        WireResponse? deleted = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, $"Doomed {what}");
            var taskId = IdOf(await CreateTaskAsync(owner, owner.SpaceId, projectId, "Doomed"));
            var editor = NewEditor();

            AssertAnswered(await SendChangesAsync(owner, owner.SpaceId, taskId, BatchOf(editor, editor.Insert(0, "short-lived"))), refused: 0);
            before = await RowCountInSpaceAsync(database, owner.SpaceId);

            var path = what switch
            {
                "task" => TaskRoutes.TaskOf(owner.SpaceId, taskId),
                "project" => ProjectRoutes.ProjectOf(owner.SpaceId, projectId),
                _ => SpaceRoutes.ActiveOf(owner.SpaceId),
            };

            deleted = await owner.Session.SendWithHeadersAsync(HttpMethod.Delete, path, content: null, NoHeaders);
        });

        Assert.NotNull(owner);
        Assert.Equal(1, before);
        Assert.True(deleted?.StatusCode == HttpStatusCode.NoContent, $"{deleted?.StatusCode} {deleted?.Body}");
        Assert.Equal(0, await RowCountInSpaceAsync(database, owner.SpaceId));
    }

    /// <summary>
    /// Moving a Task to another Project of its Space keeps its log and its projection unchanged.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1, AC2 - " + NoWritePath)]
    [Trait("Requirement", "AR-19")]
    [Trait("Requirement", "FR-41")]
    [Trait("Priority", "P1")]
    public async Task Moving_the_Task_to_another_Project_keeps_its_log_and_its_projection()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        IReadOnlyList<DescriptionRow> before = [];
        WireResponse? moved = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var editor = NewEditor();

            taskId = await TaskAsync(owner);
            AssertAnswered(await SendChangesAsync(owner, owner.SpaceId, taskId, BatchOf(editor, editor.Insert(0, "travels along"))), refused: 0);
            before = await RowsAsync(database, owner.SpaceId, taskId);

            var destination = await CreateProjectAsync(owner, "Destination");

            moved = await owner.Session.SendWithHeadersAsync(HttpMethod.Post, TaskRoutes.ProjectMoveOf(owner.SpaceId, taskId), ProjectMoveWire.Body(destination), NoHeaders);
        });

        Assert.NotNull(owner);
        Assert.True(moved?.StatusCode == HttpStatusCode.OK, $"{moved?.StatusCode} {moved?.Body}");
        Assert.Equal(before, await RowsAsync(database, owner.SpaceId, taskId), DescriptionRowComparer.Instance);
        Assert.Equal("travels along", await DescriptionAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// A replayed <c>Idempotency-Key</c> answers the original batch's answer and appends nothing again.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3, AD-18 - " + NoWritePath)]
    [Trait("Requirement", "AD-18")]
    [Trait("Priority", "P1")]
    public async Task A_replayed_key_answers_the_original_and_appends_nothing()
    {
        var database = Available();
        Actor? owner = null;
        var taskId = Guid.Empty;
        WireResponse? first = null;
        WireResponse? replay = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            var editor = NewEditor();
            var change = editor.Insert(0, "replayed");
            var key = Guid.NewGuid().ToString("N");

            taskId = await TaskAsync(owner);
            first = await SendChangesAsync(owner, owner.SpaceId, taskId, BatchOf(editor, change), key);
            replay = await SendChangesAsync(owner, owner.SpaceId, taskId, BatchOf(editor, change), key);
        });

        Assert.NotNull(owner);
        AssertAnswered(first, refused: 0);
        Assert.Equal(first!.Body, replay?.Body);
        Assert.Single(await RowsAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// Account erasure keeps every change row the Account wrote: its author becomes null and the name captured at write time
    /// stays, so the record that a Membership changed the description at a time survives.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC1, AC6, FR-3, Q4 - " + NoWritePath)]
    [Trait("Requirement", "FR-3")]
    [Trait("Requirement", "SM-5")]
    [Trait("Requirement", "UX-DR34")]
    [Trait("Priority", "P1")]
    public async Task Erasing_an_author_keeps_their_rows_and_the_name_captured_when_they_wrote()
    {
        var database = Available();
        Actor? owner = null;
        Actor? author = null;
        var taskId = Guid.Empty;
        IReadOnlyList<DescriptionRow> before = [];
        WireResponse? erased = null;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            owner = await RegisterAsync(client, database);
            author = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, author.AccountId, "Member");

            var editor = NewEditor();

            taskId = await TaskAsync(owner);
            AssertAnswered(await SendChangesAsync(author, owner.SpaceId, taskId, BatchOf(editor, editor.Insert(0, "outlives me"))), refused: 0);
            before = await RowsAsync(database, owner.SpaceId, taskId);
            erased = await AccountDeletionWire.DeleteAsync(author.Session);
        });

        Assert.NotNull(owner);
        Assert.NotNull(author);
        Assert.True(erased?.StatusCode == HttpStatusCode.NoContent, $"{erased?.StatusCode} {erased?.Body}");

        var row = Assert.Single(before);
        var after = Assert.Single(await RowsAsync(database, owner.SpaceId, taskId));

        Assert.Equal(author.AccountId, row.AuthorAccountId);
        Assert.Null(after.AuthorAccountId);
        Assert.Equal(row.AuthorName, after.AuthorName);
        Assert.Equal(row.RecordedAt, after.RecordedAt);
        Assert.Equal(row.Payload, after.Payload);
        Assert.Equal("outlives me", await DescriptionAsync(database, owner.SpaceId, taskId));
    }

    /// <summary>
    /// Nothing in the request or log path writes a description, a payload or a title to the Host's log (NFR-6, I-17): the
    /// Host's own log for a write and a read holds neither the text, nor the change's base64, nor the Task's title. Anchor:
    /// the log holds the request-finished line of the change route.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC3, NFR-6 - " + NoWritePath)]
    [Trait("Requirement", "NFR-6")]
    [Trait("Requirement", "AD-11")]
    public async Task Neither_the_write_nor_the_read_logs_the_description_its_payload_or_the_title()
    {
        var database = Available();
        var text = $"secret description {Guid.NewGuid():N}";
        var title = $"secret title {Guid.NewGuid():N}";
        var payload = string.Empty;

        var log = await HostProcess.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var editor = NewEditor();
            var change = editor.Insert(0, text);
            var taskId = await TaskAsync(owner, title);

            payload = Convert.ToBase64String(change.Encoded.Span);
            AssertAnswered(await SendChangesAsync(owner, owner.SpaceId, taskId, BatchOf(editor, change)), refused: 0);
            Assert.Equal(HttpStatusCode.OK, (await ReadAsync(owner, owner.SpaceId, taskId)).StatusCode);
        });

        Assert.Contains("/description/changes", log, StringComparison.Ordinal);
        Assert.DoesNotContain(text, log, StringComparison.Ordinal);
        Assert.DoesNotContain(payload, log, StringComparison.Ordinal);
        Assert.DoesNotContain(title, log, StringComparison.Ordinal);
    }

    /// <summary>
    /// <paramref name="count"/> one-character inserts typed one after another, as a batch from one editor.
    /// </summary>
    private static HttpContent Keystrokes(int count)
    {
        var editor = NewEditor();
        var changes = new List<TextChange>();

        for (var typed = 0; typed < count; typed++)
        {
            changes.Add(editor.Insert(typed, "k"));
        }

        return BatchOf(editor, [.. changes]);
    }

    /// <summary>
    /// Log rows compared by value, the payload by its bytes.
    /// </summary>
    private sealed class DescriptionRowComparer : IEqualityComparer<DescriptionRow>
    {
        public static readonly DescriptionRowComparer Instance = new();

        public bool Equals(DescriptionRow? x, DescriptionRow? y) =>
            x is not null && y is not null
            && (x.Id, x.Sequence, x.Kind, x.AuthorAccountId, x.AuthorName, x.RecordedAt) == (y.Id, y.Sequence, y.Kind, y.AuthorAccountId, y.AuthorName, y.RecordedAt)
            && x.Payload.AsSpan().SequenceEqual(y.Payload);

        public int GetHashCode(DescriptionRow obj) => obj.Id.GetHashCode();
    }
}
