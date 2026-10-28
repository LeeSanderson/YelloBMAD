using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Xunit;
using Yello.Contracts.Accounts;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using Yello.Tests.Slices.Tasks;
using static Yello.Tests.Slices.Boards.BoardPageWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Boards;

/// <summary>
/// Story 2.9's Weekly stage: the local LOAD at the NFR-8 envelope realisable today (test design P-1, R3, R18; Q16 (a): a
/// .NET harness over <see cref="RunningHost"/>, no new dependency) and the cold-start characterisation (P-11, AR-40b).
/// AC16, AC8 (P-11 half); NFR-5, NFR-8. Written red by the story 2.9 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>The envelope today</b>: one Project of 5,000 Tasks with three Labels each, 50 Sessions of one Account
/// (multi-member Spaces arrive in Epic 4), and the Space at its Projects-per-Space bound of 50 - the 5,000-Task Project,
/// a churn Project the writes go to (the 5,000 one is at its Tasks-per-Project bound), and 48 of 100 Tasks each
/// (<c>deferred-work.md:548</c>: the Space home listing aggregates all of them).
/// </para>
/// <para>
/// <b>A fixed number of iterations, never a duration</b> (AD-10: no timer touches the database, not in tests). Eleven
/// operation classes in a fixed rotation over the 50 Sessions, 100 samples each, every one measured by the Host's own
/// request-finished figure one request at a time (<see cref="RunningHost.ServerDurationOfAsync"/>) - so the concurrency
/// is the Sessions' interleaving through one in-process Host, not parallel load, and the report says so (Q16's cost).
/// </para>
/// <para>
/// <b>Weekly and local only</b> (<c>Tier=Weekly</c>; R13: never Azure - exhausting the free grant pauses the environment).
/// <see cref="BoardScaleCollection"/> keeps it alone in the process.
/// </para>
/// </remarks>
/// <param name="fixture">This class's migrated database on the shared engine.</param>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Tier", "Weekly")]
[Trait("Requirement", "NFR-5")]
[Trait("Requirement", "NFR-8")]
[Collection(nameof(BoardScaleCollection))]
public sealed class EnvelopeLoadTests(MigratedDatabaseFixture fixture) : IClassFixture<MigratedDatabaseFixture>
{
    private const int Scale = 5_000;
    private const int Sessions = 50;
    private const int SamplesPerClass = 100;
    private const int SmallProjects = 48;
    private const int TasksPerSmallProject = 100;
    private const double ReadBudgetMilliseconds = 300;
    private const double WriteBudgetMilliseconds = 500;

    private static readonly string[] Reads = ["board", "page-first", "page-last", "listing", "board-token", "404-boundary", "404-in-space"];
    private static readonly string[] Writes = ["create", "edit", "move", "delete"];

    /// <summary>
    /// P-1 (AC16) - at the envelope, the p95 of every operation class, server-side, is at or under 300 ms for a read and
    /// 500 ms for a write; the report states each class's p95 and the measurement condition. Records alongside: the two
    /// <c>404</c> paths' p95 (<c>deferred-work.md:488</c>), the Token-authenticated read (<c>:382</c>), position-key
    /// lengths per column (<c>:557</c>) and the <c>Task</c> clustered index's fragmentation (<c>:219</c>).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC16 - no column-page route until story Task 2; Weekly tier (Q13), excluded from the PR stage once Task 11 adds the filter")]
    public async Task At_the_envelope_every_operation_class_meets_its_p95_budget()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        var database = fixture.Database!;

        await using var host = await RunningHost.StartAsync(fixture.ConnectionString!, Configured);

        var address = $"load-{Guid.NewGuid():N}@yello.test";
        var owner = await RegisterWithAddressAsync(host.Client, database, address);
        var sessions = new List<Actor> { owner };

        for (var index = 1; index < Sessions; index++)
        {
            sessions.Add(await SignInAgainAsync(host.Client, owner, address));
        }

        var statuses = await StatusesAsync(database, owner.SpaceId);
        var big = await CreateProjectAsync(owner, "The envelope");
        var churn = await CreateProjectAsync(owner, "Churn");

        await SeedTasksAsync(database, owner.SpaceId, big, statuses[0].Id, Scale);
        _ = await ApplyLabelsToEveryTaskAsync(database, owner, big);
        await SeedTasksAsync(database, owner.SpaceId, churn, statuses[0].Id, TasksPerSmallProject);

        for (var index = 0; index < SmallProjects; index++)
        {
            await SeedTasksAsync(database, owner.SpaceId, await CreateProjectAsync(owner, $"Small {index:D2}"), statuses[0].Id, TasksPerSmallProject);
        }

        var token = await IssueTokenAsync(owner, owner.SpaceId);
        var bigTasks = (await TaskRowsAsync(database, owner.SpaceId)).Where(row => row.ProjectId == big).Select(row => row.Id).ToList();
        var foreignSpace = (await RegisterAsync(host.Client, database)).SpaceId;
        var created = new Queue<Guid>();
        var samples = Reads.Concat(Writes).ToDictionary(name => name, _ => new List<double>(SamplesPerClass), StringComparer.Ordinal);
        var classes = Reads.Concat(Writes).ToArray();
        var picker = new Random(29);

        async Task<double> RunAsync(string operation, Actor actor)
        {
            return await host.ServerDurationOfAsync(async () =>
            {
                var (answer, expected) = operation switch
                {
                    "board" => (await BoardAsync(actor, actor.SpaceId, big), HttpStatusCode.OK),
                    "page-first" => (await PageAsync(actor, actor.SpaceId, big, statuses[0].Id), HttpStatusCode.OK),
                    "page-last" => (await PageAsync(actor, actor.SpaceId, big, statuses[0].Id, "004950"), HttpStatusCode.OK),
                    "listing" => (await actor.Session.GetAsync(Yello.Contracts.Projects.ProjectRoutes.ListIn(actor.SpaceId)), HttpStatusCode.OK),
                    "board-token" => (await actor.Session.GetWithTokenAsync(Yello.Contracts.Boards.BoardRoutes.BoardOf(actor.SpaceId, big), token), HttpStatusCode.OK),
                    "404-boundary" => (await BoardAsync(actor, foreignSpace, big), HttpStatusCode.NotFound),
                    "404-in-space" => (await BoardAsync(actor, actor.SpaceId, Guid.NewGuid()), HttpStatusCode.NotFound),
                    "create" => (await CreateTaskAsync(actor, actor.SpaceId, churn, $"Load {Guid.NewGuid():N}"), HttpStatusCode.Created),
                    "edit" => (await TaskEditWire.EditTaskAsync(actor, actor.SpaceId, bigTasks[picker.Next(bigTasks.Count)], TaskEditWire.Body(new { title = $"Edited {Guid.NewGuid():N}" })), HttpStatusCode.OK),
                    "move" => (await MoveAnyAsync(actor), HttpStatusCode.OK),
                    "delete" => (await DeleteWire.DeleteTaskAsync(actor, actor.SpaceId, created.Dequeue()), HttpStatusCode.NoContent),
                    _ => throw new InvalidOperationException(operation),
                };

                Assert.True(answer.StatusCode == expected, $"{operation}: {answer.StatusCode} {answer.Body}");

                if (operation == "create")
                {
                    created.Enqueue(IdOf(answer));
                }
            });
        }

        Task<WireResponse> MoveAnyAsync(Actor actor)
        {
            var moved = bigTasks[picker.Next(bigTasks.Count)];
            var after = bigTasks[picker.Next(bigTasks.Count)];

            return MoveWire.MoveTaskAsync(actor, actor.SpaceId, moved, statuses[0].Id, after == moved ? null : after);
        }

        // Warm-up: one of each class, not sampled.
        foreach (var operation in classes)
        {
            _ = await RunAsync(operation, owner);
        }

        // A fixed rotation, never a duration: create (7) always precedes delete (10) within a cycle of eleven.
        for (var step = 0; step < classes.Length * SamplesPerClass; step++)
        {
            var operation = classes[step % classes.Length];

            samples[operation].Add(await RunAsync(operation, sessions[step % Sessions]));
        }

        var report = await ReportAsync(database, owner.SpaceId, samples.Select(entry => string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{entry.Key}: n={entry.Value.Count}, p50={Percentile.NearestRank(entry.Value, 50):F1} ms, p95={Percentile.NearestRank(entry.Value, 95):F1} ms, max={entry.Value.Max():F1} ms")));

        TestContext.Current.TestOutputHelper?.WriteLine(report);

        Assert.All(Reads, read => Assert.True(Percentile.NearestRank(samples[read], 95) <= ReadBudgetMilliseconds, report));
        Assert.All(Writes, write => Assert.True(Percentile.NearestRank(samples[write], 95) <= WriteBudgetMilliseconds, report));
    }

    /// <summary>
    /// P-11 (AC8's local half, AR-40b; <c>deferred-work.md:451</c>) - the cold path characterised and recorded, asserting
    /// nothing about time: for three fresh Host processes on the same warm database, the time from process start to the
    /// first Space-scoped response (key-ring load, EF model build, first query). What a local run cannot show is stated in
    /// the output: Azure SQL's resume from auto-pause (up to about a minute) and Container Apps' scale from zero.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC8 - the cold-start record and AR-40b's decision are story Task 11; Weekly tier (Q13)")]
    [Trait("Priority", "P2")]
    [Trait("Requirement", "AR-40")]
    public async Task The_cold_path_to_the_first_Space_scoped_response_is_characterised_and_recorded()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        var database = fixture.Database!;
        var address = $"cold-{Guid.NewGuid():N}@yello.test";
        Guid spaceId;

        await using (var first = await RunningHost.StartAsync(fixture.ConnectionString!, Configured))
        {
            spaceId = (await RegisterWithAddressAsync(first.Client, database, address)).SpaceId;
        }

        var figures = new List<double>();

        for (var host = 0; host < 3; host++)
        {
            var stopwatch = Stopwatch.StartNew();

            await using var fresh = await RunningHost.StartAsync(fixture.ConnectionString!, Configured);

            var listening = stopwatch.Elapsed.TotalMilliseconds;
            var session = new WireSession(fresh.Client);

            await session.FetchAntiforgeryTokenAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await session.SignInAsync(address, Password)).StatusCode);

            var answer = await session.GetAsync(SpaceRoutes.ActiveOf(spaceId));

            stopwatch.Stop();

            Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
            figures.Add(stopwatch.Elapsed.TotalMilliseconds);
            TestContext.Current.TestOutputHelper?.WriteLine(string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"Host {host + 1}: listening after {listening:F0} ms; first Space-scoped response after {stopwatch.Elapsed.TotalMilliseconds:F0} ms."));
        }

        TestContext.Current.TestOutputHelper?.WriteLine(
            "Local cold path only: a fresh process on a warm, local engine. Not shown here - Azure SQL serverless resume from "
            + "auto-pause (up to about a minute) and Container Apps' scale from zero; the deploy-target observation is "
            + "operations P6 (Lee). NFR-5 is measured warm (AR-40b, story Q9).");

        Assert.Equal(3, figures.Count);
    }

    private static async Task<Actor> RegisterWithAddressAsync(HttpClient client, RegistrationDatabase database, string address)
    {
        var session = new WireSession(client);

        await session.FetchAntiforgeryTokenAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await session.RegisterAsync(new RegisterAccountRequest("Lo-" + Guid.NewGuid().ToString("N")[..8], address, Password))).StatusCode);

        return await SignInAsync(session, database, address);
    }

    private static async Task<Actor> SignInAgainAsync(HttpClient client, Actor owner, string address)
    {
        var session = new WireSession(client);

        await session.FetchAntiforgeryTokenAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await session.SignInAsync(address, Password)).StatusCode);
        await session.FetchAntiforgeryTokenAsync();

        return owner with { Session = session };
    }

    private static async Task<Actor> SignInAsync(WireSession session, RegistrationDatabase database, string address)
    {
        Assert.Equal(HttpStatusCode.NoContent, (await session.SignInAsync(address, Password)).StatusCode);
        await session.FetchAntiforgeryTokenAsync();

        var spaces = await session.GetAsync(SpaceRoutes.Directory);

        using var document = JsonDocument.Parse(spaces.Body);
        var spaceId = document.RootElement.EnumerateArray().Single().GetProperty("id").GetGuid();

        await using var connection = await database.OpenWithoutSpaceContextAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        command.CommandText = "SELECT Id FROM dbo.[Account] WHERE EmailAddress = @address";
        command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@address", address));

        var accountId = Assert.IsType<Guid>(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        return new Actor(session, accountId, spaceId);
    }

    private static async Task<string> ReportAsync(RegistrationDatabase database, Guid spaceId, IEnumerable<string> sampleLines)
    {
        var lines = new List<string>
        {
            "Measurement condition: local SQL Server engine (Testcontainers, 2 GB cap), MAXDOP = 1, READ_COMMITTED_SNAPSHOT on, "
            + "warm, one in-process Host, requests measured one at a time by the Host's request-finished figure across 50 Sessions "
            + "- not production evidence (R18).",
        };

        lines.AddRange(sampleLines);


        // Key lengths are read under the Space's key: the FILTER predicate hides every Task from a connection without one.
        await using (var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken))
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT CONCAT(StatusId, ': max ', MAX(LEN(PositionKey)), ', avg ', AVG(LEN(PositionKey))) FROM dbo.[Task] GROUP BY StatusId";

            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                lines.Add("Position-key length by Status (:557) - " + reader.GetString(0));
            }
        }

        lines.AddRange(await CatalogueAsync(
            database,
            "SELECT CONCAT('Task clustered index fragmentation (:219): ', CAST(avg_fragmentation_in_percent AS decimal(5, 1)), '%, ', page_count, ' pages') FROM sys.dm_db_index_physical_stats(DB_ID(), OBJECT_ID(N'dbo.Task'), 1, NULL, 'LIMITED')"));

        return string.Join(Environment.NewLine, lines);
    }
}
