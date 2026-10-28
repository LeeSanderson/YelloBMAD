using System.Globalization;
using System.Net;
using Xunit;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using Yello.Tests.Slices.Boards;
using static Yello.Tests.Slices.Lists.ListWire;
using static Yello.Tests.Slices.Tasks.LabelWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Lists;

/// <summary>
/// Story 2.10's Nightly measurement of the List View at 5,000 Tasks (FR-30 "on the same terms as FR-28"; AC10): the first
/// page and the last page of each offered sort, unfiltered and under the heaviest filter combination, each hold a
/// server-side p95 at or under 300 ms. AC10 (timed half); FR-30, NFR-5, NFR-8, AD-29. Written red by the story 2.10 ATDD
/// run.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nightly, never the PR stage</b> (<c>Tier=Nightly</c>, <c>Explicit</c>; <c>ci.yml</c> excludes the tier by trait). The
/// PR stage's proof is deterministic: <c>ListPagingTests</c>' LV-18 compares logical reads.
/// </para>
/// <para>
/// <b>Server-side, warm, local, and said so</b> - 2.9's terms (AR-40b: NFR-5 measured warm, the cold path exempt). Every
/// sample is the Host's own request-finished figure (<see cref="RunningHost.ServerDurationOfAsync"/>), the twelve
/// operations are interleaved after a warm-up, and each p95 is the nearest rank of 100 (<see cref="Percentile.NearestRank"/>).
/// The output states the condition - a local engine, warm, never production evidence (R18).
/// </para>
/// <para>
/// <b>Alone</b>: <see cref="BoardScaleCollection"/> disables parallelisation. One database of its own on the suite's one
/// engine; no new engine, no new fixture type, no new collection.
/// </para>
/// </remarks>
/// <param name="fixture">This class's migrated database on the shared engine.</param>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Tier", "Nightly")]
[Trait("Requirement", "FR-30")]
[Trait("Requirement", "NFR-5")]
[Trait("Requirement", "NFR-8")]
[Trait("Requirement", "AD-29")]
[Collection(nameof(BoardScaleCollection))]
public sealed class ListScaleTimingTests(MigratedDatabaseFixture fixture) : IClassFixture<MigratedDatabaseFixture>
{
    private const int Scale = 5_000;
    private const int Samples = 100;
    private const int WarmUp = 5;
    private const double ReadBudgetMilliseconds = 300;

    /// <summary>
    /// P-3/P-4 timed (AC10) - at 5,000 Tasks with ties on every sort column, about a third undated and two fifths
    /// unlabelled, the first and the last page of each of the three sorts, unfiltered and under two of three Statuses, two
    /// Labels and a four-day range, each meet the 300 ms p95 server-side read budget over 100 interleaved samples.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Explicit = true)]
    public async Task The_first_and_last_page_of_every_sort_meet_the_read_budget_at_5000_Tasks()
    {
        Assert.SkipUnless(fixture.IsAvailable, fixture.UnavailableReason ?? string.Empty);

        var database = fixture.Database!;

        await using var host = await RunningHost.StartAsync(fixture.ConnectionString!, Configured);

        var owner = await RegisterAsync(host.Client, database);
        var projectId = await CreateProjectAsync(owner, "Listed at the envelope");
        var statusIds = (await StatusesAsync(database, owner.SpaceId)).Select(status => status.Id).ToList();
        var alpha = await SeedAsync(owner, owner.SpaceId, "Alpha " + Guid.NewGuid().ToString("N")[..8]);
        var bravo = await SeedAsync(owner, owner.SpaceId, "Bravo " + Guid.NewGuid().ToString("N")[..8]);
        var charlie = await SeedAsync(owner, owner.SpaceId, "Charlie " + Guid.NewGuid().ToString("N")[..8]);

        await SeedListAsync(database, owner.SpaceId, projectId, statusIds, Scale);
        await SetDueDatesAsync(database, owner.SpaceId, projectId, distinctDays: 7);
        await ApplyLabelAsync(database, owner.SpaceId, projectId, alpha, modulus: 5, remainder: 0);
        await ApplyLabelAsync(database, owner.SpaceId, projectId, alpha, modulus: 5, remainder: 1);
        await ApplyLabelAsync(database, owner.SpaceId, projectId, bravo, modulus: 5, remainder: 2);
        await ApplyLabelAsync(database, owner.SpaceId, projectId, charlie, modulus: 5, remainder: 2);
        await ApplyLabelAsync(database, owner.SpaceId, projectId, charlie, modulus: 5, remainder: 3);
        await BackfillFirstLabelNamesAsync(database, owner.SpaceId, projectId);

        var from = new DateTimeOffset(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);
        var operations = new List<(string Name, Func<Task<double>> Measure, List<double> Samples)>();

        foreach (var sort in Sorts)
        {
            foreach (var query in new[] { ListQuery.By(sort), new ListQuery(sort, [statusIds[0], statusIds[1]], [alpha, charlie], DueFrom: from, DueTo: from.AddDays(4)) })
            {
                // Paged to the end first (P-2's terms: "measured after paging to the end").
                var walked = await WalkAsync(owner, owner.SpaceId, projectId, query);
                var lastCursor = walked.Count > 1 ? walked[^2].NextAfter : null;
                var label = query.StatusIds.Count > 0 ? $"{sort}, heaviest filter" : sort;

                operations.Add(($"{label}: first page", () => host.ServerDurationOfAsync(() => ExpectAsync(PageAsync(owner, owner.SpaceId, projectId, query))), new List<double>(Samples)));
                operations.Add(($"{label}: last page ({walked.Count})", () => host.ServerDurationOfAsync(() => ExpectAsync(PageAsync(owner, owner.SpaceId, projectId, query, lastCursor))), new List<double>(Samples)));
            }
        }

        for (var round = 0; round < WarmUp; round++)
        {
            foreach (var operation in operations)
            {
                _ = await operation.Measure();
            }
        }

        for (var round = 0; round < Samples; round++)
        {
            foreach (var operation in operations)
            {
                operation.Samples.Add(await operation.Measure());
            }
        }

        var report = Report([.. operations.Select(operation => (operation.Name, operation.Samples))]);

        TestContext.Current.TestOutputHelper?.WriteLine(report);

        Assert.All(operations, operation => Assert.True(Percentile.NearestRank(operation.Samples, 95) <= ReadBudgetMilliseconds, report));
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
                CultureInfo.InvariantCulture,
                $"{entry.Operation}: n={entry.Samples.Count}, p50={Percentile.NearestRank(entry.Samples, 50):F1} ms, p95={Percentile.NearestRank(entry.Samples, 95):F1} ms, max={entry.Samples.Max():F1} ms")));
}
