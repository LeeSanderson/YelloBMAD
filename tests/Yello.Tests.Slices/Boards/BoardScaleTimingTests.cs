using System.Net;
using Xunit;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using static Yello.Tests.Slices.Boards.BoardPageWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Boards;

/// <summary>
/// Story 2.9's Nightly measurements of the Board at 5,000 Tasks (test design P-2, R4): the first paint is one bounded
/// seek per column, and the last page of a column meets the 300 ms p95 server-side read budget as the first does,
/// measured after paging to the end. AC2, AC1 (timed half); FR-28, NFR-5, NFR-8, AD-29. Written red by the story 2.9
/// ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nightly, never the PR stage</b> (<c>Tier=Nightly</c>, story Q13 (a); <c>test-design-qa.md:419-424</c>). The PR
/// stage's proof of depth independence is deterministic - <c>BoardPagingTests</c>' PG-11 compares logical reads - and
/// this case adds the budget.
/// </para>
/// <para>
/// <b>Server-side, warm, local, and said so.</b> Every sample is the Host's own request-finished figure
/// (<see cref="RunningHost.ServerDurationOfAsync"/>), samples are interleaved (first page, last page, Board) after a
/// warm-up, and the p95 is the nearest rank of at least 100 (<see cref="Percentile.NearestRank"/>). Depth independence
/// reuses <see cref="DurationIndistinguishability.CompareMeasuredAsync"/> unchanged (medians, 20%). The output states the
/// condition: a local engine with <c>MAXDOP = 1</c>, warm - never production evidence (R18).
/// </para>
/// <para>
/// <b>Alone</b>: <see cref="BoardScaleCollection"/> disables parallelisation, so nothing else in the process competes with
/// a timed request (CLAUDE.md; the 2.2 and 2.7 lessons). One database of its own on the suite's one engine.
/// </para>
/// </remarks>
/// <param name="fixture">This class's migrated database on the shared engine.</param>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Tier", "Nightly")]
[Trait("Requirement", "NFR-5")]
[Trait("Requirement", "NFR-8")]
[Trait("Requirement", "AD-29")]
[Collection(nameof(BoardScaleCollection))]
public sealed class BoardScaleTimingTests(MigratedDatabaseFixture fixture) : IClassFixture<MigratedDatabaseFixture>
{
    private const int Scale = 5_000;
    private const int Samples = 100;
    private const int WarmUp = 5;
    private const double ReadBudgetMilliseconds = 300;

    /// <summary>
    /// P-2 (AC2) - at 5,000 Tasks with three Labels each, the Board read (first paint), a column's first page and its last
    /// page each hold a server-side p95 at or under 300 ms over 100 interleaved samples, and the last page is
    /// indistinguishable from the first in duration.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Explicit = true)]
    public async Task The_last_page_of_a_5000_Task_column_meets_the_read_budget_as_the_first_does()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        var database = fixture.Database!;

        await using var host = await RunningHost.StartAsync(fixture.ConnectionString!, Configured);

        var owner = await RegisterAsync(host.Client, database);
        var projectId = await CreateProjectAsync(owner, "At the envelope");
        var statusId = (await StatusesAsync(database, owner.SpaceId))[0].Id;

        await SeedTasksAsync(database, owner.SpaceId, projectId, statusId, Scale);
        _ = await ApplyLabelsToEveryTaskAsync(database, owner, projectId);

        // Paged to the end first (P-2: "measured after paging to the end").
        var walked = await WalkAsync(owner, owner.SpaceId, projectId, ColumnsOf(await BoardAsync(owner, owner.SpaceId, projectId))[0]);

        Assert.Equal(Scale / PageSize, walked.Count);

        var lastCursor = walked[^2].NextAfter;
        var firstPage = new List<double>(Samples);
        var lastPage = new List<double>(Samples);
        var board = new List<double>(Samples);

        Task<double> FirstAsync() => host.ServerDurationOfAsync(() => ExpectAsync(PageAsync(owner, owner.SpaceId, projectId, statusId)));
        Task<double> LastAsync() => host.ServerDurationOfAsync(() => ExpectAsync(PageAsync(owner, owner.SpaceId, projectId, statusId, lastCursor)));
        Task<double> BoardReadAsync() => host.ServerDurationOfAsync(() => ExpectAsync(BoardAsync(owner, owner.SpaceId, projectId)));

        for (var round = 0; round < WarmUp; round++)
        {
            _ = await FirstAsync();
            _ = await LastAsync();
            _ = await BoardReadAsync();
        }

        for (var round = 0; round < Samples; round++)
        {
            firstPage.Add(await FirstAsync());
            lastPage.Add(await LastAsync());
            board.Add(await BoardReadAsync());
        }

        var verdict = await DurationIndistinguishability.CompareMeasuredAsync(FirstAsync, LastAsync);

        var report = Report(("Board read (first paint)", board), ("column page 1", firstPage), ("column page 100", lastPage));

        TestContext.Current.TestOutputHelper?.WriteLine(report);
        TestContext.Current.TestOutputHelper?.WriteLine($"Depth: {"page 1 against page 100: " + verdict.Describe()}");

        Assert.All(
            new[] { board, firstPage, lastPage },
            samples => Assert.True(Percentile.NearestRank(samples, 95) <= ReadBudgetMilliseconds, report));
        Assert.True(verdict.AreIndistinguishable, "page 1 against page 100: " + verdict.Describe());
    }

    private static async Task ExpectAsync(Task<WireResponse> request)
    {
        var answer = await request;

        Assert.True(answer.StatusCode == HttpStatusCode.OK, $"{answer.StatusCode} {answer.Body}");
    }

    private static string Report(params (string Operation, List<double> Samples)[] classes) =>
        "Measurement condition: local SQL Server engine (Testcontainers), MAXDOP = 1, warm, server-side request-finished "
        + "figures, one in-process Host - not production evidence (R18)." + Environment.NewLine
        + string.Join(
            Environment.NewLine,
            classes.Select(entry => string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{entry.Operation}: n={entry.Samples.Count}, p50={Percentile.NearestRank(entry.Samples, 50):F1} ms, p95={Percentile.NearestRank(entry.Samples, 95):F1} ms, max={entry.Samples.Max():F1} ms")));
}

/// <summary>
/// The collection for story 2.9's timed scale cases (Nightly P-2, Weekly P-1 and P-11): they measure time, so they run with
/// nothing else in the process (CLAUDE.md: "Timing tests need a <c>DisableParallelization</c> collection"). Each class takes
/// its own database on the suite's one engine - no new engine, no new fixture type.
/// </summary>
[CollectionDefinition(nameof(BoardScaleCollection), DisableParallelization = true)]
public sealed class BoardScaleCollection;
