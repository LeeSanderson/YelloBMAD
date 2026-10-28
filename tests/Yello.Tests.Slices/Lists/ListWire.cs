using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Contracts;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using Yello.Tests.Slices.Tasks;

namespace Yello.Tests.Slices.Lists;

/// <summary>
/// The wire and SQL helpers story 2.10's List View cases share: the List route and its query (open questions 1 (a) and 4
/// (a)), reading a page as (Project, Statuses, true total, Tasks, cursor), walking a list to its end, seeding a Project
/// with ties and <c>NULL</c>s on every sort column, the expected order of every sort computed <b>in SQL</b>, never with
/// .NET's <see cref="Guid"/> comparison, and the logical reads and plans of what one request ran. Written by the story
/// 2.10 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Contract members</b>: <see cref="PageSize"/> is <c>ListPage.Size</c> and the two problem types are
/// <c>ProblemTypes</c>' entries. The three sort codes stay literal constants, because <c>[InlineData]</c> arguments must
/// be compile-time constants and <c>ListSorts</c>' codes are properties (S2339); a code that drifted from
/// <c>ListSorts</c> is refused <c>400 invalid-list-query</c> by every case here, and <c>ListViewGateTests</c> pins the
/// codes themselves. The query parameter names are Task 4 (a)'s: <c>sort</c>, <c>status</c>
/// and <c>label</c> (repeatable), <c>dueFrom</c>, <c>dueTo</c>, <c>dueNone</c> and <c>after</c>. Swap each for the
/// contract member and change nothing else; a different decision is recorded in the Debug Log.
/// </para>
/// <para>
/// <b>Seed constraints checked</b> (<see cref="SeedListAsync"/>, <see cref="SetDueDatesAsync"/>,
/// <see cref="ApplyLabelAsync"/>, <see cref="BackfillFirstLabelNamesAsync"/>):
/// </para>
/// <list type="bullet">
/// <item><description><c>dbo.[Task]</c> (<c>20261025094743_Projects</c>, story 2.2's Task migration,
/// <c>20261025143020_TaskDueDate</c>): every non-nullable column is supplied - <c>Id</c> (<c>ValueGeneratedNever</c>,
/// <c>NEWID()</c>), <c>SpaceId</c>, <c>ProjectId</c>, <c>StatusId</c>, <c>Title</c> (at most 200), <c>PositionKey</c>
/// (<c>varchar(128)</c> BIN2), <c>CreatedAt</c>; <c>DueDate</c> is nullable. The composite FK <c>(SpaceId, ProjectId)</c>
/// holds because the Project is one the product created in that Space; the composite FK <c>(SpaceId, StatusId)</c>
/// because each Status is read from that Space's own <c>StatusDefinition</c> rows; the unique index
/// <c>IX_Task_SpaceId_ProjectId_StatusId_PositionKey</c> because every key is <c>0</c> plus a distinct 5-digit ordinal;
/// the <c>BLOCK AFTER INSERT/UPDATE</c> predicates because each statement runs under the Space's session key.
/// <c>DueDate</c> values are instants at offset zero inside 2.3's 2000-2100 window.</description></item>
/// <item><description><c>dbo.[TaskLabel]</c> (<c>20261025175001_LabelTables</c>): <c>(SpaceId, TaskId, LabelId,
/// AppliedAt)</c> all supplied; <c>PK_TaskLabel (SpaceId, TaskId, LabelId)</c> holds because each statement applies one
/// Label once per Task; <c>FK_TaskLabel_Task_SpaceId_TaskId</c> to <c>AK_Task_SpaceId_Id</c> because every row is read
/// from <c>dbo.[Task]</c> in the same statement; <c>FK_TaskLabel_Label_SpaceId_LabelId</c> to <c>AK_Label_SpaceId_Id</c>
/// because the Labels were defined through the product in that Space.</description></item>
/// <item><description><c>dbo.[Task].FirstLabelName</c> (story Task 2 (a), <b>not yet in the schema</b>): written only by
/// <see cref="BackfillFirstLabelNamesAsync"/>, in the migration backfill's own shape (Task 2 (c)), for the 5,000-row
/// cost cases whose Labels are seeded by SQL. Every correctness case applies its Labels through the product's Labels
/// edit, so the product's own maintenance (Task 2 (d)) is what those cases sort on.</description></item>
/// </list>
/// </remarks>
internal static class ListWire
{
    /// <summary>
    /// Q2 (a)'s page size, one named server constant.
    /// </summary>
    public static int PageSize => Application.Lists.ListPage.Size;

    /// <summary>
    /// The Status sort code (Q7 (a): effective order, then <c>TaskId</c>). Swap for <c>ListSorts.Status</c>.
    /// </summary>
    public const string SortStatus = "status";

    /// <summary>
    /// The due-date sort code (Q5 (a): ascending, <c>NULL</c> last). Swap for <c>ListSorts.DueDate</c>.
    /// </summary>
    public const string SortDueDate = "due-date";

    /// <summary>
    /// The Label sort code (Q3 (a): the first Label's name, <c>NULL</c> last). Swap for <c>ListSorts.Label</c>.
    /// </summary>
    public const string SortLabel = "label";

    /// <summary>
    /// Task 4 (c)'s refusal of an unknown sort, a malformed id or a malformed or out-of-range due bound.
    /// </summary>
    public static string InvalidListQuery => ProblemTypes.InvalidListQuery;

    /// <summary>
    /// Task 4 (c)'s refusal of a cursor this route did not issue for this sort.
    /// </summary>
    public static string InvalidListCursor => ProblemTypes.InvalidListCursor;

    /// <summary>
    /// Every offered sort, for the cases that run over all three.
    /// </summary>
    public static readonly string[] Sorts = [SortStatus, SortDueDate, SortLabel];

    /// <summary>
    /// The List path of one Project, as a client calls it, with <paramref name="query"/> and an optional cursor.
    /// Every value is escaped; repeatable parameters repeat.
    /// </summary>
    public static string PathOf(Guid spaceId, Guid projectId, ListQuery query, string? after = null)
    {
        var parameters = new List<string> { "sort=" + Uri.EscapeDataString(query.Sort) };

        parameters.AddRange(query.StatusIds.Select(id => "status=" + id.ToString("D")));
        parameters.AddRange(query.LabelIds.Select(id => "label=" + id.ToString("D")));

        if (query.DueFrom is { } from)
        {
            parameters.Add("dueFrom=" + Uri.EscapeDataString(from.ToString("O", CultureInfo.InvariantCulture)));
        }

        if (query.DueTo is { } to)
        {
            parameters.Add("dueTo=" + Uri.EscapeDataString(to.ToString("O", CultureInfo.InvariantCulture)));
        }

        if (query.DueNone)
        {
            parameters.Add("dueNone=true");
        }

        if (after is not null)
        {
            parameters.Add("after=" + Uri.EscapeDataString(after));
        }

        return $"/api/v1/spaces/{spaceId:D}/projects/{projectId:D}/list?{string.Join('&', parameters)}";
    }

    public static Task<WireResponse> PageAsync(Actor actor, Guid spaceId, Guid projectId, ListQuery query, string? after = null) =>
        actor.Session.GetAsync(PathOf(spaceId, projectId, query, after));

    /// <summary>
    /// A List answer read as a <see cref="ListPage"/>, failing the case unless it is a <c>200</c>.
    /// </summary>
    public static ListPage PageOf(WireResponse answer)
    {
        Assert.True(answer.StatusCode == HttpStatusCode.OK, $"{answer.StatusCode} {answer.Body}");

        using var document = JsonDocument.Parse(answer.Body);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("nextAfter", out var cursor), $"The List page carries no `nextAfter` (Q1 (a)): {answer.Body}");

        var tasks = root.GetProperty("tasks").EnumerateArray()
            .Select(task => new ListedTask(
                task.GetProperty("id").GetGuid(),
                task.GetProperty("title").GetString() ?? string.Empty,
                task.GetProperty("statusId").GetGuid(),
                task.TryGetProperty("dueDate", out var due) && due.ValueKind != JsonValueKind.Null ? due.GetDateTimeOffset() : null,
                task.TryGetProperty("labelIds", out var labels) ? [.. labels.EnumerateArray().Select(label => label.GetGuid())] : []))
            .ToList();

        return new ListPage(
            root.GetProperty("total").GetInt32(),
            [.. root.GetProperty("statuses").EnumerateArray().Select(status => status.GetProperty("id").GetGuid())],
            tasks,
            cursor.ValueKind == JsonValueKind.Null ? null : cursor.GetString());
    }

    /// <summary>
    /// Reads the first page of <paramref name="query"/> and follows its cursor until it is <c>null</c>, answering every
    /// page. Fails if a page is refused or the walk does not end within <paramref name="limit"/> pages.
    /// </summary>
    public static async Task<IReadOnlyList<ListPage>> WalkAsync(Actor actor, Guid spaceId, Guid projectId, ListQuery query, int limit = 1_000)
    {
        var pages = new List<ListPage> { PageOf(await PageAsync(actor, spaceId, projectId, query)) };

        while (pages[^1].NextAfter is { } after)
        {
            Assert.True(pages.Count < limit, $"The List's cursor had not reached null after {limit} pages.");

            pages.Add(PageOf(await PageAsync(actor, spaceId, projectId, query, after)));
        }

        return pages;
    }

    /// <summary>
    /// <paramref name="count"/> Tasks in one Project, spread over the Space's Statuses by ordinal (<c>N % statuses</c>), in
    /// one set-based statement under the Space's key. Due dates are left <c>NULL</c>; see <see cref="SetDueDatesAsync"/>.
    /// See the class remarks for every constraint this satisfies.
    /// </summary>
    public static async Task SeedListAsync(RegistrationDatabase database, Guid spaceId, Guid projectId, IReadOnlyList<Guid> statusIds, int count)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            WITH Numbers AS (
                SELECT TOP (@count) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS N
                FROM sys.all_objects a CROSS JOIN sys.all_objects b)
            INSERT dbo.[Task] (Id, SpaceId, ProjectId, StatusId, Title, PositionKey, CreatedAt)
            SELECT NEWID(), @space, @project,
                   CASE N % 3 WHEN 0 THEN @s0 WHEN 1 THEN @s1 ELSE @s2 END,
                   CONCAT(N'Listed ', N), CONCAT('0', RIGHT(CONCAT('00000', N), 5)), SYSDATETIMEOFFSET()
            FROM Numbers
            """;
        command.Parameters.Add(new SqlParameter("@count", count));
        command.Parameters.Add(new SqlParameter("@space", spaceId));
        command.Parameters.Add(new SqlParameter("@project", projectId));
        command.Parameters.Add(new SqlParameter("@s0", statusIds[0]));
        command.Parameters.Add(new SqlParameter("@s1", statusIds[1 % statusIds.Count]));
        command.Parameters.Add(new SqlParameter("@s2", statusIds[2 % statusIds.Count]));

        Assert.Equal(count, await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Gives the Project's Tasks due dates with heavy ties: a Task whose title ordinal <c>N</c> satisfies
    /// <c>(N / 3) % nullEvery = 0</c> keeps <c>NULL</c> (about a third at 3, spread over every Status, which
    /// <see cref="SeedListAsync"/> assigns by <c>N % 3</c>), and every other Task is due on one of
    /// <paramref name="distinctDays"/> days from 2026-11-02, at midnight UTC. One statement under the Space's key.
    /// </summary>
    public static Task SetDueDatesAsync(RegistrationDatabase database, Guid spaceId, Guid projectId, int distinctDays, int nullEvery = 3) =>
        ExecuteForSpaceAsync(
            database,
            spaceId,
            """
            UPDATE t SET DueDate = CASE WHEN (o.N / 3) % @nullEvery = 0 THEN NULL
                                        ELSE TODATETIMEOFFSET(DATEADD(day, o.N % @days, CAST('2026-11-02T00:00:00' AS datetime2)), 0) END
            FROM dbo.[Task] t CROSS APPLY (SELECT CAST(SUBSTRING(t.Title, 8, 10) AS int) AS N) o
            WHERE t.ProjectId = @project AND t.Title LIKE N'Listed %'
            """,
            ("@nullEvery", nullEvery),
            ("@days", distinctDays),
            ("@project", projectId));

    /// <summary>
    /// The same due date on every one of <paramref name="taskIds"/> - a run of ties a page boundary lands inside.
    /// </summary>
    public static async Task SetDueDateAsync(RegistrationDatabase database, Guid spaceId, IEnumerable<Guid> taskIds, DateTimeOffset? dueDate)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);

        foreach (var taskId in taskIds)
        {
            await using var command = connection.CreateCommand();

            command.CommandText = "UPDATE dbo.[Task] SET DueDate = @due WHERE Id = @id";
            command.Parameters.Add(new SqlParameter("@due", System.Data.SqlDbType.DateTimeOffset) { Value = dueDate.HasValue ? dueDate.Value : DBNull.Value });
            command.Parameters.Add(new SqlParameter("@id", taskId));

            Assert.Equal(1, await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        }
    }

    /// <summary>
    /// Sets exactly <paramref name="labelIds"/> on a Task through the product's Labels edit (story 2.4 Q1 (a): the Task
    /// <c>PATCH</c> with <c>labelIds</c>), failing the case unless it is admitted - so the product maintains the Task's
    /// Label sort key itself (Task 2 (d)).
    /// </summary>
    public static async Task SetLabelsAsync(Actor actor, Guid spaceId, Guid taskId, params Guid[] labelIds)
    {
        var answer = await TaskEditWire.EditTaskAsync(actor, spaceId, taskId, LabelWire.ApplyBody(labelIds));

        Assert.True(answer.StatusCode == HttpStatusCode.OK, $"Setting a Task's Labels through the product: {answer.StatusCode} {answer.Body}");
    }

    /// <summary>
    /// Applies <paramref name="labelId"/> to every Task of the Project whose title ordinal <c>N</c> satisfies
    /// <c>N % modulus = remainder</c>, in one set-based statement under the Space's key - the 5,000-row cases only. See the
    /// class remarks; follow with <see cref="BackfillFirstLabelNamesAsync"/>.
    /// </summary>
    public static Task ApplyLabelAsync(RegistrationDatabase database, Guid spaceId, Guid projectId, Guid labelId, int modulus, int remainder) =>
        ExecuteForSpaceAsync(
            database,
            spaceId,
            """
            INSERT dbo.[TaskLabel] (SpaceId, TaskId, LabelId, AppliedAt)
            SELECT t.SpaceId, t.Id, @label, SYSDATETIMEOFFSET()
            FROM dbo.[Task] t
            WHERE t.ProjectId = @project AND t.Title LIKE N'Listed %' AND CAST(SUBSTRING(t.Title, 8, 10) AS int) % @modulus = @remainder
            """,
            ("@label", labelId),
            ("@project", projectId),
            ("@modulus", modulus),
            ("@remainder", remainder));

    /// <summary>
    /// The migration backfill's statement (story Task 2 (c)), run under the Space's key for the Tasks whose Labels a
    /// 5,000-row case seeded by SQL: each Task's <c>FirstLabelName</c> is its first Label's name in chip order (name, then
    /// id). Reads a column that does not exist until Task 2 lands - every caller is skipped until then.
    /// </summary>
    public static Task BackfillFirstLabelNamesAsync(RegistrationDatabase database, Guid spaceId, Guid projectId) =>
        ExecuteForSpaceAsync(
            database,
            spaceId,
            """
            UPDATE t SET FirstLabelName = (
                SELECT TOP (1) l.Name FROM dbo.[TaskLabel] tl
                JOIN dbo.[Label] l ON l.SpaceId = tl.SpaceId AND l.Id = tl.LabelId
                WHERE tl.SpaceId = t.SpaceId AND tl.TaskId = t.Id
                ORDER BY l.Name, l.Id)
            FROM dbo.[Task] t WHERE t.ProjectId = @project
            """,
            ("@project", projectId));

    /// <summary>
    /// The Project's Task ids in the order <paramref name="sort"/> must serve them, computed by SQL Server under the Space's
    /// key - never by .NET's <see cref="Guid"/> comparison, which does not order a <c>uniqueidentifier</c> as SQL Server
    /// does (spine <c>:268</c>). <paramref name="filter"/> is a SQL predicate over <c>t</c> (a constant written by the case;
    /// empty for none). The Label key is computed from <c>TaskLabel</c> and <c>Label</c> directly, not read from the
    /// product's <c>FirstLabelName</c>, so a key the product failed to maintain shows as a misordered walk.
    /// </summary>
    public static async Task<IReadOnlyList<Guid>> ExpectedOrderAsync(RegistrationDatabase database, Guid spaceId, Guid projectId, string sort, string filter = "")
    {
        var order = sort switch
        {
            SortStatus => "s.Position, s.Id, t.Id",
            SortDueDate => "CASE WHEN t.DueDate IS NULL THEN 1 ELSE 0 END, t.DueDate, t.Id",
            SortLabel => "CASE WHEN f.Name IS NULL THEN 1 ELSE 0 END, f.Name, t.Id",
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Not an offered sort."),
        };

        var sql = new StringBuilder(
            """
            SELECT t.Id FROM dbo.[Task] t
            JOIN dbo.[StatusDefinition] s ON s.SpaceId = t.SpaceId AND s.Id = t.StatusId
            OUTER APPLY (SELECT TOP (1) l.Name FROM dbo.[TaskLabel] tl
                         JOIN dbo.[Label] l ON l.SpaceId = tl.SpaceId AND l.Id = tl.LabelId
                         WHERE tl.SpaceId = t.SpaceId AND tl.TaskId = t.Id ORDER BY l.Name, l.Id) f
            WHERE t.ProjectId = @project
            """);

        if (filter.Length > 0)
        {
            sql.Append(" AND (").Append(filter).Append(')');
        }

        sql.Append(" ORDER BY ").Append(order);

        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Review SQL queries - constant predicates written by the story 2.10 cases; values are parameters.
        command.CommandText = sql.ToString();
#pragma warning restore CA2100
        command.Parameters.Add(new SqlParameter("@project", projectId));

        var ids = new List<Guid>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    /// <summary>
    /// One Task's <c>FirstLabelName</c> as stored (Task 2 (a); <c>null</c> for <c>NULL</c>), under the Space's key.
    /// </summary>
    public static async Task<string?> FirstLabelNameAsync(RegistrationDatabase database, Guid spaceId, Guid taskId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT FirstLabelName FROM dbo.[Task] WHERE Id = @id";
        command.Parameters.Add(new SqlParameter("@id", taskId));

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken), $"No Task {taskId} in the Space.");

        return await reader.IsDBNullAsync(0, TestContext.Current.CancellationToken) ? null : reader.GetString(0);
    }

    /// <summary>
    /// The first name, in chip order (name, then id, in the column's own collation), of the Labels a Task carries now, by
    /// SQL under the Space's key - <c>null</c> when it carries none. What <see cref="FirstLabelNameAsync"/> must equal.
    /// </summary>
    public static async Task<string?> FirstAppliedLabelNameAsync(RegistrationDatabase database, Guid spaceId, Guid taskId)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT TOP (1) l.Name FROM dbo.[TaskLabel] tl
            JOIN dbo.[Label] l ON l.SpaceId = tl.SpaceId AND l.Id = tl.LabelId
            WHERE tl.TaskId = @id ORDER BY l.Name, l.Id
            """;
        command.Parameters.Add(new SqlParameter("@id", taskId));

        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken) as string;
    }

    /// <summary>
    /// The logical reads <paramref name="request"/> caused in this class's database: the growth of
    /// <c>total_logical_reads</c> summed over every cached statement in it that reads <c>dbo.[Task]</c>, measured on a
    /// connection with no Space context. Deterministic where a duration is not (AC10, 2.9 PG-11): the same statements serve
    /// the first page and the last, so their reads compare the work of one seek at two depths, and an <c>OFFSET</c> reads
    /// every row it skips. Run the request once to warm its plans before measuring (2.9: a first execution carries
    /// compilation's reads). Nothing else runs in this database while a class's case runs. An automatic statistics update
    /// the request happens to trigger (<c>SELECT StatMan(…) FROM … [Task]</c>) is the engine's housekeeping, not the
    /// request's read, and is left out: it once added 13,000 reads to one side of a comparison.
    /// </summary>
    public static async Task<long> LogicalReadsOfAsync(RegistrationDatabase database, Func<Task> request)
    {
        var before = await TaskStatementReadsAsync(database);

        await request();

        return await TaskStatementReadsAsync(database) - before;
    }

    /// <summary>
    /// Every cached plan in this class's database that names <paramref name="indexName"/>, as showplan XML - the plans of
    /// the statements a List request ran.
    /// </summary>
    public static async Task<IReadOnlyList<string>> PlansNamingAsync(RegistrationDatabase database, string indexName)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT DISTINCT CAST(qp.query_plan AS nvarchar(max))
            FROM sys.dm_exec_query_stats qs
            CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
            CROSS APPLY sys.dm_exec_query_plan(qs.plan_handle) qp
            CROSS APPLY sys.dm_exec_plan_attributes(qs.plan_handle) pa
            WHERE pa.attribute = N'dbid' AND CAST(pa.value AS int) = DB_ID()
              AND st.text NOT LIKE N'%dm_exec_query_stats%'
              AND CAST(qp.query_plan AS nvarchar(max)) LIKE N'%' + @index + N'%'
            """;
        command.Parameters.Add(new SqlParameter("@index", indexName));

        var plans = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            plans.Add(reader.GetString(0));
        }

        return plans;
    }

    /// <summary>
    /// Whether a showplan converts <paramref name="column"/> implicitly - a parameter of another type compared with the
    /// column, which loses the seek (Task 3 (d), "parameter types"; 2.9 PG-12).
    /// </summary>
    public static bool ConvertsImplicitly(string plan, string column) =>
        Regex.IsMatch(
            plan,
            "<Convert [^>]*Implicit=\"1\"[^>]*>(?:(?!</Convert>).)*?Column=\"" + Regex.Escape(column) + "\"",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

    private static async Task<long> TaskStatementReadsAsync(RegistrationDatabase database)
    {
        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText =
            """
            SELECT COALESCE(SUM(qs.total_logical_reads), 0)
            FROM sys.dm_exec_query_stats qs
            CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
            CROSS APPLY sys.dm_exec_plan_attributes(qs.plan_handle) pa
            WHERE pa.attribute = N'dbid' AND CAST(pa.value AS int) = DB_ID()
              AND st.text LIKE N'%Task]%'
              AND st.text NOT LIKE N'%dm_exec_query_stats%'
              AND st.text NOT LIKE N'%StatMan%'
            """;

        return Convert.ToInt64(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), CultureInfo.InvariantCulture);
    }

    private static async Task ExecuteForSpaceAsync(RegistrationDatabase database, Guid spaceId, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);

        _ = await TaskWire.ExecuteAsync(connection, sql, parameters);
    }
}

/// <summary>
/// What a List request asks for (open question 4 (a)): a sort, any-of Status and Label filters, and a due-date choice -
/// a half-open <c>[DueFrom, DueTo)</c> range, or <see cref="DueNone"/> for "No due date", or neither for "Any".
/// </summary>
/// <param name="Sort">The sort code.</param>
/// <param name="StatusIds">Statuses, any of which a Task may be in; empty for every Status.</param>
/// <param name="LabelIds">Labels, any of which a Task may carry; empty for any Labels or none.</param>
/// <param name="DueFrom">The range's inclusive start, or <c>null</c>.</param>
/// <param name="DueTo">The range's exclusive end, or <c>null</c>.</param>
/// <param name="DueNone">Only Tasks with no due date.</param>
internal sealed record ListQuery(
    string Sort,
    IReadOnlyList<Guid> StatusIds,
    IReadOnlyList<Guid> LabelIds,
    DateTimeOffset? DueFrom = null,
    DateTimeOffset? DueTo = null,
    bool DueNone = false)
{
    /// <summary>
    /// <paramref name="sort"/>, unfiltered.
    /// </summary>
    public static ListQuery By(string sort) => new(sort, [], []);
}

/// <summary>
/// One List page as served.
/// </summary>
/// <param name="Total">The true number of the Project's Tasks matching the filter (AC11: a separate <c>COUNT</c>).</param>
/// <param name="StatusIds">The Project's effective Statuses, in effective order (Q1 (a)).</param>
/// <param name="Tasks">The page's Tasks, in served order.</param>
/// <param name="NextAfter">The opaque cursor, or <c>null</c> when nothing follows.</param>
internal sealed record ListPage(int Total, IReadOnlyList<Guid> StatusIds, IReadOnlyList<ListedTask> Tasks, string? NextAfter);

/// <summary>
/// One row of a List page - a <c>TaskResponse</c> (AC22: the same Task the Board serves).
/// </summary>
/// <param name="Id">Its id.</param>
/// <param name="Title">Its title.</param>
/// <param name="StatusId">Its Status.</param>
/// <param name="DueDate">Its due date, or <c>null</c>.</param>
/// <param name="LabelIds">Its Labels, in chip order.</param>
internal sealed record ListedTask(Guid Id, string Title, Guid StatusId, DateTimeOffset? DueDate, IReadOnlyList<Guid> LabelIds);
