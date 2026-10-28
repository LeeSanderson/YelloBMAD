using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;
using Yello.Contracts.Boards;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using Yello.Tests.Slices.Tasks;

namespace Yello.Tests.Slices.Boards;

/// <summary>
/// The wire and SQL helpers story 2.9's paging cases share: the column-page route (open question 1 (a)), reading a page
/// or a Board column as (Tasks, true total, cursor), walking a column to its end, applying Labels to every Task of a
/// Project in one statement, and reading the page seek's own logical reads and plan from the engine's plan cache. Written
/// by the story 2.9 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scaffold constants until the contract exists.</b> <see cref="ColumnRoute"/>, <see cref="InvalidBoardCursor"/>,
/// <see cref="TaskPositionFull"/> and <see cref="PageSize"/> are the story's recommended names (Q1 (a), Task 2, Q11 (a),
/// AR-22). When Task 2 adds <c>BoardRoutes</c>' page route, <c>ProblemTypes</c>' two entries and the page-size constant,
/// replace each with the contract member and change nothing else. The wire member is <c>nextAfter</c> (Q1 (a)).
/// </para>
/// <para>
/// <b>Seed constraints checked</b> for <see cref="ApplyLabelsToEveryTaskAsync"/> against
/// <c>20261025175001_LabelTables.cs</c>: <c>dbo.[TaskLabel]</c> is <c>(SpaceId, TaskId, LabelId, AppliedAt)</c>, all
/// non-nullable and all supplied; its key <c>PK_TaskLabel (SpaceId, TaskId, LabelId)</c> holds because the Labels are
/// distinct; <c>FK_TaskLabel_Task_SpaceId_TaskId</c> to <c>AK_Task_SpaceId_Id</c> holds because every Task row is read
/// from <c>dbo.[Task]</c> in the same statement under the Space's key (the FILTER predicate admits only that Space's
/// rows); <c>FK_TaskLabel_Label_SpaceId_LabelId</c> to <c>AK_Label_SpaceId_Id</c> holds because the Labels were defined
/// through the product in that Space; the <c>BLOCK AFTER INSERT</c> predicate holds because the statement runs under the
/// Space's session key. Tasks come from <see cref="TaskWire.SeedTasksAsync"/>, whose constraints <see cref="TaskWire"/>
/// states.
/// </para>
/// </remarks>
internal static partial class BoardPageWire
{
    /// <summary>
    /// AR-22's initial page size - an assumption, "the seek shape is the invariant, the page size is not" (spine
    /// <c>:259</c>). Task 2 states it once as <c>BoardPage.Size</c>.
    /// </summary>
    public const int PageSize = 50;

    /// <summary>
    /// The column-page route's template, as story 2.9 Q1 (a) shapes it. Task 2 adds it to <c>BoardRoutes</c>.
    /// </summary>
    public const string ColumnRoute = "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/board/columns/{statusId:guid}";

    /// <summary>
    /// The refusal of a malformed cursor (AC9, Task 2). Task 2 adds it to <c>ProblemTypes</c>.
    /// </summary>
    public const string InvalidBoardCursor = "urn:yello:problem:invalid-board-cursor";

    /// <summary>
    /// The refusal of a move into a gap no key of at most 128 characters fits (AC18, Q11 (a)). Task 10 adds it to
    /// <c>ProblemTypes</c>.
    /// </summary>
    public const string TaskPositionFull = "urn:yello:problem:task-position-full";

    /// <summary>
    /// One column's page path, as a client calls it: the Board's path, then <c>/columns/{statusId}</c>, then the cursor (the
    /// last loaded Task's position key) when there is one.
    /// </summary>
    public static string ColumnOf(Guid spaceId, Guid projectId, Guid statusId, string? after = null) =>
        BoardRoutes.BoardOf(spaceId, projectId) + $"/columns/{statusId:D}" + (after is null ? string.Empty : "?after=" + Uri.EscapeDataString(after));

    public static Task<WireResponse> PageAsync(Actor actor, Guid spaceId, Guid projectId, Guid statusId, string? after = null) =>
        actor.Session.GetAsync(ColumnOf(spaceId, projectId, statusId, after));

    /// <summary>
    /// A column-page answer read as a <see cref="ColumnPage"/>, failing the case unless it is a <c>200</c>.
    /// </summary>
    public static ColumnPage PageOf(WireResponse answer)
    {
        Assert.True(answer.StatusCode == System.Net.HttpStatusCode.OK, $"{answer.StatusCode} {answer.Body}");

        using var document = JsonDocument.Parse(answer.Body);

        return Read(document.RootElement);
    }

    /// <summary>
    /// Every column of a Board answer, in served order, each read as a <see cref="ColumnPage"/> - the Board's first page
    /// per column (AC9).
    /// </summary>
    public static IReadOnlyList<ColumnPage> ColumnsOf(WireResponse board)
    {
        Assert.True(board.StatusCode == System.Net.HttpStatusCode.OK, $"{board.StatusCode} {board.Body}");

        using var document = JsonDocument.Parse(board.Body);

        return [.. document.RootElement.GetProperty("columns").EnumerateArray().Select(Read)];
    }

    /// <summary>
    /// Follows a column's cursor from <paramref name="first"/> until it is <c>null</c>, answering every page including the
    /// first. Fails if a page is refused or the walk does not end within <paramref name="limit"/> pages.
    /// </summary>
    public static async Task<IReadOnlyList<ColumnPage>> WalkAsync(Actor actor, Guid spaceId, Guid projectId, ColumnPage first, int limit = 1_000)
    {
        var pages = new List<ColumnPage> { first };

        while (pages[^1].NextAfter is { } after)
        {
            Assert.True(pages.Count < limit, $"The column's cursor had not reached null after {limit} pages.");

            pages.Add(PageOf(await PageAsync(actor, spaceId, projectId, first.StatusId, after)));
        }

        return pages;
    }

    /// <summary>
    /// Defines <paramref name="labels"/> Labels through the product and applies all of them to every Task of
    /// <paramref name="projectId"/> in one set-based statement under the Space's key. See the class remarks for every
    /// constraint this satisfies. Answers the Label ids.
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> ApplyLabelsToEveryTaskAsync(RegistrationDatabase database, Actor owner, Guid projectId, int labels = 3)
    {
        var ids = new List<Guid>();

        for (var index = 0; index < labels; index++)
        {
            ids.Add(await LabelWire.SeedAsync(owner, owner.SpaceId, $"Scale {index} {Guid.NewGuid():N}"[..20]));
        }

        await using var connection = await database.OpenForSpaceAsync(owner.SpaceId, TestContext.Current.CancellationToken);

        foreach (var labelId in ids)
        {
            await TaskWire.ExecuteAsync(
                connection,
                "INSERT dbo.[TaskLabel] (SpaceId, TaskId, LabelId, AppliedAt) SELECT t.SpaceId, t.Id, @label, SYSDATETIMEOFFSET() FROM dbo.[Task] t WHERE t.ProjectId = @project",
                ("@label", labelId),
                ("@project", projectId));
        }

        return ids;
    }

    /// <summary>
    /// The logical reads of the last execution of the product's page seek in this class's database - the statement whose
    /// text carries <c>[PositionKey] &gt; @</c> (AC1: <c>AND PositionKey &gt; @last ORDER BY PositionKey</c>) - read from
    /// <c>sys.dm_exec_query_stats</c> on a connection with no Space context. Fails, naming the shape it looked for, when no
    /// such statement has run.
    /// </summary>
    /// <remarks>
    /// Deterministic where a duration is not (story Task 3): the same parameterised plan serves every page, so
    /// <c>last_logical_reads</c> after page 2 and after page 100 compare the work of one seek at two depths. An
    /// <c>OFFSET</c> implementation reads every row it skips, and the deep page's reads grow with its depth.
    /// </remarks>
    public static async Task<long> LastLogicalReadsOfThePageSeekAsync(RegistrationDatabase database)
    {
        var (reads, _) = await PageSeekStatisticsAsync(database);

        return reads;
    }

    /// <summary>
    /// The cached showplan XML of the product's page seek (see <see cref="LastLogicalReadsOfThePageSeekAsync"/>).
    /// </summary>
    public static async Task<string> PlanOfThePageSeekAsync(RegistrationDatabase database)
    {
        var (_, plan) = await PageSeekStatisticsAsync(database);

        return plan;
    }

    /// <summary>
    /// Whether a showplan converts the position key implicitly - an <c>nvarchar</c> parameter compared with the
    /// <c>varchar</c> BIN2 column, which loses the seek (story Task 2 (b)).
    /// </summary>
    public static bool ConvertsThePositionKeyImplicitly(string plan) => ImplicitKeyConversion.IsMatch(plan);

    private static async Task<(long Reads, string Plan)> PageSeekStatisticsAsync(RegistrationDatabase database)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT TOP (1) qs.last_logical_reads, CAST(qp.query_plan AS nvarchar(max))
            FROM sys.dm_exec_query_stats qs
            CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
            CROSS APPLY sys.dm_exec_query_plan(qs.plan_handle) qp
            CROSS APPLY sys.dm_exec_plan_attributes(qs.plan_handle) pa
            WHERE pa.attribute = N'dbid' AND CAST(pa.value AS int) = DB_ID()
              AND st.text LIKE N'%PositionKey] > @%'
              AND st.text LIKE N'%FROM %Task%'
              AND st.text NOT LIKE N'%dm_exec_query_stats%'
            ORDER BY qs.last_execution_time DESC
            """;

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        Assert.True(
            await reader.ReadAsync(TestContext.Current.CancellationToken),
            "No statement carrying `[PositionKey] > @` ran in this database: the column page is not read by a keyset seek on the position key (AC1).");

        return (reader.GetInt64(0), await reader.IsDBNullAsync(1, TestContext.Current.CancellationToken) ? string.Empty : reader.GetString(1));
    }

    private static ColumnPage Read(JsonElement column)
    {
        Assert.True(column.TryGetProperty("nextAfter", out var cursor), $"The column carries no `nextAfter` (AC9, Q1 (a)): {column}");

        var tasks = column.GetProperty("tasks").EnumerateArray()
            .Select(task => new PagedTask(
                task.GetProperty("id").GetGuid(),
                task.GetProperty("positionKey").GetString() ?? string.Empty,
                task.GetProperty("statusId").GetGuid(),
                task.TryGetProperty("labelIds", out var labels) ? [.. labels.EnumerateArray().Select(label => label.GetGuid())] : []))
            .ToList();

        return new ColumnPage(
            column.GetProperty("statusId").GetGuid(),
            column.GetProperty("total").GetInt32(),
            tasks,
            cursor.ValueKind == JsonValueKind.Null ? null : cursor.GetString());
    }

    [GeneratedRegex("""<Convert [^>]*Implicit="1"[^>]*>(?:(?!</Convert>).)*?Column="PositionKey""", RegexOptions.Singleline, 5000)]
    private static partial Regex ImplicitKeyConversion { get; }
}

/// <summary>
/// One column page as served: its Status, its true total, its Tasks in served order, and its cursor.
/// </summary>
/// <param name="StatusId">The column's Status.</param>
/// <param name="Total">The true number of the Project's Tasks in that Status (AC4: a separate <c>COUNT</c>).</param>
/// <param name="Tasks">The page's Tasks, in served order.</param>
/// <param name="NextAfter">The cursor - the last Task's position key - or <c>null</c> when no Task follows (AC9).</param>
internal sealed record ColumnPage(Guid StatusId, int Total, IReadOnlyList<PagedTask> Tasks, string? NextAfter);

/// <summary>
/// One Task of a page.
/// </summary>
/// <param name="Id">Its id.</param>
/// <param name="PositionKey">Its position key, readable (FR-35).</param>
/// <param name="StatusId">Its Status.</param>
/// <param name="LabelIds">Its Labels, as served.</param>
internal sealed record PagedTask(Guid Id, string PositionKey, Guid StatusId, IReadOnlyList<Guid> LabelIds);
