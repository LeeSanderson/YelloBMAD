using Microsoft.Data.SqlClient;
using Xunit;
using Yello.Tests.Shared;
using Yello.Tests.Slices.Accounts.RegisterAccount;
using Yello.Tests.Slices.Tasks;
using static Yello.Tests.Slices.Lists.ListWire;
using static Yello.Tests.Slices.Tasks.TaskWire;

namespace Yello.Tests.Slices.Lists;

/// <summary>
/// The List View's Assignee dimension, which completes FR-30: the Assignee sort (by <c>AssigneeMembershipId</c>, so
/// each person's Tasks are grouped, Unassigned fixed last, <c>TaskId</c> the tiebreaker) walked to its end with page
/// boundaries inside runs of ties and across the Unassigned segment (P-3); the Assignee filter (repeatable <c>assignee</c>
/// ids and an <c>unassigned</c> flag, the <c>dueNone</c> precedent) with its true total; and NFR-5 at 5,000 Tasks for the new
/// sort, unfiltered and filtered - the last page reads no more than the first, and the page seeks the sort's own composite
/// index without converting its column (P-4 at run time).
/// </summary>
/// <remarks>
/// <para>
/// <b>Expected orders come from SQL Server</b> (<see cref="ExpectedAssigneeOrderAsync"/>): the order between people is the
/// <c>uniqueidentifier</c> order of their Membership ids, stable but not alphabetical, and .NET's <see cref="Guid"/>
/// comparison does not reproduce it.
/// </para>
/// <para>
/// <b>Seed constraints checked.</b> The Tasks are <see cref="ListWire.SeedListAsync"/>'s (its remarks list every
/// constraint). The Assignees are three Accounts registered through the product, given Member rows in the owner's Space by
/// <see cref="TaskWire.AddMemberAsync"/> (<c>FK_Membership_Account_AccountId</c> holds, one row per Account per Space, no
/// second Owner). The one <c>UPDATE</c> that assigns them runs under the Space's key (BLOCK AFTER UPDATE admits it: the
/// <c>SpaceId</c> is unchanged) and names only Memberships of that Space, so the composite key
/// <c>(SpaceId, AssigneeMembershipId) → Membership(SpaceId, Id)</c> admits every row. Seeding the 5,000 directly rather
/// than through the product is the bulk exception <c>TESTING-CONVENTIONS.md</c> allows.
/// </para>
/// <para>
/// <b>Existing cases the dev phase changes in the same commit</b>: LV-10's <c>[InlineData("sort=assignee")]</c> (now a
/// <c>200</c>), and the cursor case that sends <c>sort=assignee&amp;after=garbage</c>.
/// </para>
/// </remarks>
public sealed partial class ListPagingTests
{
    private const string SortAssignee = "assignee";

    private const string AssigneeIndex = "IX_Task_SpaceId_ProjectId_AssigneeMembershipId_Id";

    private const string AssigneeSortSkip = "red: AC7 - the List offers no Assignee sort and dbo.[Task] has no AssigneeMembershipId yet (Q6 (a))";

    /// <summary>
    /// P-3 for the Assignee sort: 155 Tasks, a quarter unassigned and the rest shared by three Members (runs of about 39), walk
    /// grouped by Membership in SQL Server's <c>uniqueidentifier</c> order, then Unassigned, each run in <c>TaskId</c> order;
    /// boundaries at 50, 100 and 150 fall inside runs and across into the Unassigned segment; every Task appears once, every
    /// page states the true total, and the last cursor is <c>null</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = AssigneeSortSkip)]
    [Trait("Requirement", "FR-30")]
    [Trait("Requirement", "AD-29")]
    public async Task Sorted_by_Assignee_each_persons_Tasks_are_grouped_Unassigned_last_and_none_skipped_or_repeated()
    {
        var database = Available();
        IReadOnlyList<ListPage> pages = [];
        IReadOnlyList<Guid> expected = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Grouped by person");

            _ = await SeedAssignedAsync(client, database, owner, projectId, count: 155);

            pages = await WalkAsync(owner, owner.SpaceId, projectId, ListQuery.By(SortAssignee));
            expected = await ExpectedAssigneeOrderAsync(database, owner.SpaceId, projectId);
        });

        Assert.Equal(155, expected.Count);
        AssertWalk(pages, expected);
    }

    /// <summary>
    /// The Assignee filter: one Member's id and the <c>unassigned</c> flag together serve exactly that Member's Tasks and the
    /// unassigned ones (any of), with their true total; the other Members' Tasks are not served. Anchor: unfiltered, the same
    /// sort serves every Task.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = "red: AC7 - the List reads no assignee or unassigned parameter yet (Q6 (a) sub-question)")]
    [Trait("Requirement", "FR-30")]
    public async Task Filtering_by_an_Assignee_and_Unassigned_serves_exactly_their_Tasks_and_their_true_total()
    {
        var database = Available();
        var total = 0;
        IReadOnlyList<ListPage> pages = [];
        IReadOnlyList<Guid> expected = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Filtered by person");
            var members = await SeedAssignedAsync(client, database, owner, projectId, count: 90);
            var path = PathOf(owner.SpaceId, projectId, ListQuery.By(SortAssignee)) + $"&{AssignmentWire.AssigneeParameter}={members[0]:D}&{AssignmentWire.UnassignedParameter}=true";

            total = PageOf(await PageAsync(owner, owner.SpaceId, projectId, ListQuery.By(SortAssignee))).Total;
            pages = await WalkPathAsync(owner, path);
            expected = await ExpectedAssigneeOrderAsync(database, owner.SpaceId, projectId, $"t.AssigneeMembershipId = '{members[0]:D}' OR t.AssigneeMembershipId IS NULL");
        });

        Assert.Equal(90, total);
        Assert.InRange(expected.Count, 1, total - 1);
        AssertWalk(pages, expected);
    }

    /// <summary>
    /// NFR-5 at 5,000 Tasks for the Assignee sort (the deterministic half of the List View's budget): the last page reads no more logical
    /// pages than the first, within the tolerance of 5, unfiltered and under the Assignee filter.
    /// </summary>
    /// <param name="filtered">Whether under the Assignee filter (one Member and Unassigned).</param>
    /// <returns>A task.</returns>
    [Theory(Skip = AssigneeSortSkip)]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "NFR-5")]
    [Trait("Requirement", "NFR-8")]
    [Trait("Requirement", "FR-30")]
    public async Task The_last_page_of_the_Assignee_sort_reads_no_more_than_its_first_at_5000_Tasks(bool filtered)
    {
        const long tolerance = 5;

        var database = Available();
        long first = 0;
        long last = 0;

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Five thousand, by person");
            var members = await SeedAssignedAsync(client, database, owner, projectId, count: Scale);
            var path = PathOf(owner.SpaceId, projectId, ListQuery.By(SortAssignee))
                + (filtered ? $"&{AssignmentWire.AssigneeParameter}={members[0]:D}&{AssignmentWire.UnassignedParameter}=true" : string.Empty);

            var walked = await WalkPathAsync(owner, path);

            Assert.True(walked.Count >= 3, $"The walk is {walked.Count} pages; the case is about depth.");

            var lastPath = path + "&after=" + Uri.EscapeDataString(walked[^2].NextAfter!);

            // Warm both statements' plans: a first execution carries compilation's reads.
            _ = PageOf(await owner.Session.GetAsync(path));
            _ = PageOf(await owner.Session.GetAsync(lastPath));

            first = await LogicalReadsOfAsync(database, async () => _ = PageOf(await owner.Session.GetAsync(path)));
            last = await LogicalReadsOfAsync(database, async () => _ = PageOf(await owner.Session.GetAsync(lastPath)));
        });

        TestContext.Current.TestOutputHelper?.WriteLine($"assignee{(filtered ? " (filtered)" : string.Empty)}: first page {first}, last page {last} logical reads");

        Assert.True(first > 0, "The first page reported no logical reads on the Task table.");
        Assert.True(last <= first + tolerance, $"assignee: the last page read {last} logical pages against the first's {first}.");
    }

    /// <summary>
    /// P-4 at run time (LV-19's sibling): the Assignee sort's pages are served by a seek on
    /// <c>IX_Task_SpaceId_ProjectId_AssigneeMembershipId_Id</c>, and no plan converts <c>AssigneeMembershipId</c> implicitly
    /// (the cursor's Membership id is bound as a <c>uniqueidentifier</c>).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact(Skip = AssigneeSortSkip)]
    [Trait("Requirement", "AR-22")]
    [Trait("Requirement", "AD-29")]
    public async Task The_Assignee_sort_seeks_its_own_composite_index_and_never_converts_its_column()
    {
        var database = Available();
        IReadOnlyList<string> plans = [];

        await fixture.Hosts.RunAsync(fixture.ConnectionString!, Configured, async client =>
        {
            var owner = await RegisterAsync(client, database);
            var projectId = await CreateProjectAsync(owner, "Seeks by person");

            _ = await SeedAssignedAsync(client, database, owner, projectId, count: 600);

            var firstPage = PageOf(await PageAsync(owner, owner.SpaceId, projectId, ListQuery.By(SortAssignee)));

            _ = PageOf(await PageAsync(owner, owner.SpaceId, projectId, ListQuery.By(SortAssignee), firstPage.NextAfter));
            plans = await PlansNamingAsync(database, AssigneeIndex);
        });

        Assert.NotEmpty(plans);
        Assert.Contains(plans, plan => plan.Contains("SeekPredicates", StringComparison.Ordinal));
        Assert.All(plans, plan => Assert.False(ConvertsImplicitly(plan, "AssigneeMembershipId"), "A plan converts AssigneeMembershipId implicitly."));
    }

    /// <summary>
    /// <paramref name="count"/> Tasks in the Project, and three Members of the owner's Space registered through the product:
    /// the Task titled <c>Listed N</c> is unassigned when <c>N % 4 = 0</c> and otherwise assigned to Member <c>N % 4</c>. One
    /// set-based <c>UPDATE</c> under the Space's key.
    /// </summary>
    /// <returns>The three Members' Membership ids.</returns>
    private async Task<IReadOnlyList<Guid>> SeedAssignedAsync(HttpClient client, RegistrationDatabase database, Actor owner, Guid projectId, int count)
    {
        var statusIds = (await StatusesAsync(database, owner.SpaceId)).Select(status => status.Id).ToList();
        var wire = new WireDatabase(fixture.ConnectionString!);
        var members = new List<Guid>();

        await SeedListAsync(database, owner.SpaceId, projectId, statusIds, count);

        for (var index = 0; index < 3; index++)
        {
            var member = await RegisterAsync(client, database);

            await AddMemberAsync(database, owner.SpaceId, member.AccountId, "Member");
            members.Add((await wire.MembershipOfAsync(owner.SpaceId, member.AccountId)).Id);
        }

        await using var connection = await database.OpenForSpaceAsync(owner.SpaceId, TestContext.Current.CancellationToken);

        _ = await ExecuteAsync(
            connection,
            """
            UPDATE dbo.[Task]
            SET AssigneeMembershipId = CASE CAST(SUBSTRING(Title, 8, 10) AS int) % 4 WHEN 1 THEN @m1 WHEN 2 THEN @m2 WHEN 3 THEN @m3 ELSE NULL END
            WHERE SpaceId = @space AND ProjectId = @project
            """,
            ("@m1", members[0]),
            ("@m2", members[1]),
            ("@m3", members[2]),
            ("@space", owner.SpaceId),
            ("@project", projectId));

        return members;
    }

    /// <summary>
    /// The Project's Task ids in the Assignee sort's order, computed by SQL Server under the Space's key: assigned first,
    /// grouped by Membership id in <c>uniqueidentifier</c> order, then unassigned, each by <c>TaskId</c>.
    /// <paramref name="filter"/> is a SQL predicate over <c>t</c> written by the case.
    /// </summary>
    private static async Task<IReadOnlyList<Guid>> ExpectedAssigneeOrderAsync(RegistrationDatabase database, Guid spaceId, Guid projectId, string filter = "")
    {
        await using var connection = await database.OpenForSpaceAsync(spaceId, TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

#pragma warning disable CA2100 // Review SQL queries - a constant predicate written by the case; values are parameters.
        command.CommandText =
            "SELECT t.Id FROM dbo.[Task] t WHERE t.ProjectId = @project"
            + (filter.Length > 0 ? " AND (" + filter + ")" : string.Empty)
            + " ORDER BY CASE WHEN t.AssigneeMembershipId IS NULL THEN 1 ELSE 0 END, t.AssigneeMembershipId, t.Id";
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
    /// Reads the first page of a List path the case wrote, and follows its cursor until it is <c>null</c>.
    /// </summary>
    private static async Task<IReadOnlyList<ListPage>> WalkPathAsync(Actor owner, string path)
    {
        var pages = new List<ListPage> { PageOf(await owner.Session.GetAsync(path)) };

        while (pages[^1].NextAfter is { } after)
        {
            Assert.True(pages.Count < 1_000, "The List's cursor had not reached null after 1,000 pages.");

            pages.Add(PageOf(await owner.Session.GetAsync(path + "&after=" + Uri.EscapeDataString(after))));
        }

        return pages;
    }
}
