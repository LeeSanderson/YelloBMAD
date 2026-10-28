using Xunit;
using Yello.Application.Lists;
using Yello.Application.Lists.ViewList;
using Yello.Domain.Boards;
using Yello.Domain.Lists;
using Yello.Domain.Projects;
using Yello.Domain.Statuses;
using Yello.Domain.Tasks;

namespace Yello.Tests.Slices.Lists.ViewList;

/// <summary>
/// Story 2.10 Task 3 - the List slice takes its total from the reader's count, never from the rows it serves; narrows
/// filter ids to the Project's effective Statuses and the Space's Labels before any read, so a foreign or random id
/// matches nothing and is never refused; hands the reader the Statuses in effective order; and issues a cursor exactly
/// when another Task follows. AC4, AC11, AC14, AC15; FR-30, AD-16, AD-29.
/// </summary>
/// <remarks>
/// <b>The cases the wire cannot hold</b>: a reader that returns fewer Tasks than it counts, and Statuses defined out of
/// position order - so only a total from the count and an order from <c>Position</c> are right.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-30")]
[Trait("Requirement", "AR-22")]
[Trait("Requirement", "AD-29")]
public sealed class ViewListHandlerTests
{
    /// <summary>
    /// AC11 - the total is the reader's count over the filter, even where the page holds fewer Tasks.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_total_is_the_count_not_the_number_of_Tasks_served()
    {
        var world = new ListWorld();
        var todo = world.Status(0);

        world.Tasks.Add(world.NewTask(todo));
        world.Count = 812;

        var page = await world.HandleAsync(ListSort.Status, ListFilter.None);

        Assert.NotNull(page);
        Assert.Equal(812, page.Total);
        Assert.Single(page.Tasks);
    }

    /// <summary>
    /// AC14, AD-16 - the Statuses are served, and handed to the reader, in effective order (by <c>Position</c>), not in
    /// the order they were defined or named.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Statuses_are_served_and_sought_in_effective_order()
    {
        var world = new ListWorld();
        var done = world.Status(2);
        var todo = world.Status(0);
        var doing = world.Status(1);

        var page = await world.HandleAsync(ListSort.Status, ListFilter.None);

        Assert.NotNull(page);
        Assert.Equal([todo.Id, doing.Id, done.Id], page.Statuses.Select(status => status.Id));
        Assert.Equal([todo.Id, doing.Id, done.Id], world.LastStatusOrder);
    }

    /// <summary>
    /// AC15, Q4 (a) - a Status filter naming a foreign or random id keeps only the effective ones, in effective order; a
    /// Label filter keeps only the Space's Labels; neither is refused.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Filter_ids_outside_the_Project_and_Space_drop_out_before_any_read()
    {
        var world = new ListWorld();
        var todo = world.Status(0);
        var doing = world.Status(1);
        var done = world.Status(2);
        var label = Guid.NewGuid();

        world.Labels.Add(label);

        var page = await world.HandleAsync(ListSort.DueDate, new ListFilter(
            StatusIds: [done.Id, Guid.NewGuid(), todo.Id],
            LabelIds: [Guid.NewGuid(), label]));

        Assert.NotNull(page);
        Assert.Equal([done.Id, todo.Id], world.LastFilter?.StatusIds);
        Assert.Equal([label], world.LastFilter?.LabelIds);
        Assert.Equal([todo.Id, done.Id], world.LastStatusOrder);
        Assert.DoesNotContain(doing.Id, world.LastStatusOrder);
    }

    /// <summary>
    /// AC15 - a filter whose every id is foreign is left empty, which matches nothing: the narrowed filter says so, and
    /// it is never <c>null</c> (which would mean "not filtered" and list everything).
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_filter_left_with_no_id_matches_nothing_rather_than_everything()
    {
        var world = new ListWorld();

        world.Status(0);

        var page = await world.HandleAsync(ListSort.Label, new ListFilter(LabelIds: [Guid.NewGuid()]));

        Assert.NotNull(page);
        Assert.NotNull(world.LastFilter?.LabelIds);
        Assert.Empty(world.LastFilter.LabelIds);
        Assert.True(world.LastFilter.MatchesNothing);
    }

    /// <summary>
    /// AC3 - a Project the reader cannot see answers no page, which the endpoint turns into the boundary's <c>404</c>;
    /// nothing else is read.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task An_invisible_Project_answers_no_page_and_reads_nothing_else()
    {
        var world = new ListWorld { ProjectVisible = false };

        world.Status(0);

        Assert.Null(await world.HandleAsync(ListSort.Status, ListFilter.None));
        Assert.Null(world.LastFilter);
    }

    /// <summary>
    /// AC4, Q2 (a) - the reader is asked for one more than a page; a full page plus one issues a cursor for the last served
    /// Task's pair under the page's sort, and exactly a page issues none.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_cursor_is_issued_for_the_last_served_pair_exactly_when_another_Task_follows()
    {
        var world = new ListWorld();
        var todo = world.Status(0);
        var due = new DateTimeOffset(2026, 11, 5, 0, 0, 0, TimeSpan.Zero);

        for (var index = 0; index <= Application.Lists.ListPage.Size; index++)
        {
            var task = world.NewTask(todo);

            task.DueDate = due;
            world.Tasks.Add(task);
        }

        var full = await world.HandleAsync(ListSort.DueDate, ListFilter.None);

        Assert.NotNull(full);
        Assert.Equal(Application.Lists.ListPage.Size + 1, world.LastTake);
        Assert.Equal(Application.Lists.ListPage.Size, full.Tasks.Count);
        Assert.NotNull(full.NextAfter);
        Assert.True(ListCursorText.TryRead(ListSort.DueDate, full.NextAfter, out var cursor));
        Assert.Equal(new ListCursor(full.Tasks[^1].Id, DueDate: due), cursor);

        world.Tasks.RemoveAt(world.Tasks.Count - 1);

        var exact = await world.HandleAsync(ListSort.DueDate, ListFilter.None);

        Assert.NotNull(exact);
        Assert.Null(exact.NextAfter);
    }

    private sealed class ListWorld : IBoardReader, IListReader, IStatusDefinitionReader
    {
        private readonly List<StatusDefinition> _statuses = [];

        public Guid SpaceId { get; } = Guid.NewGuid();

        public Guid ProjectId { get; } = Guid.NewGuid();

        public bool ProjectVisible { get; init; } = true;

        public List<TaskItem> Tasks { get; } = [];

        public List<Guid> Labels { get; } = [];

        public int Count { get; set; }

        public ListFilter? LastFilter { get; private set; }

        public IReadOnlyList<Guid> LastStatusOrder { get; private set; } = [];

        public int LastTake { get; private set; }

        public StatusDefinition Status(int position)
        {
            var status = new StatusDefinition { Id = Guid.NewGuid(), SpaceId = SpaceId, Name = $"Status {position}", Position = position, CreatedAt = DateTimeOffset.UnixEpoch };

            _statuses.Add(status);

            return status;
        }

        public TaskItem NewTask(StatusDefinition status) => new()
        {
            Id = Guid.NewGuid(),
            SpaceId = SpaceId,
            ProjectId = ProjectId,
            StatusId = status.Id,
            Title = $"Task {Guid.NewGuid():N}",
            PositionKey = PositionKey.First,
            CreatedAt = DateTimeOffset.UnixEpoch,
        };

        public Task<ListView?> HandleAsync(ListSort sort, ListFilter filter) =>
            new ViewListHandler(this, this, this).HandleAsync(ProjectId, sort, filter, null, TestContext.Current.CancellationToken);

        public Task<Project?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(ProjectVisible && projectId == ProjectId
                ? new Project { Id = ProjectId, SpaceId = SpaceId, Name = "List", CreatedAt = DateTimeOffset.UnixEpoch }
                : null);

        public Task<IReadOnlyDictionary<Guid, int>> CountTasksByStatusAsync(Guid projectId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The List never reads the Board's per-Status counts.");

        public Task<int> CountTasksAsync(Guid projectId, Guid statusId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The List never reads a Board column's count.");

        public Task<IReadOnlyList<TaskItem>> PageAsync(Guid projectId, Guid statusId, string? after, int take, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The List never reads a Board column.");

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LabelIdsOfAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(new Dictionary<Guid, IReadOnlyList<Guid>>());

        public Task<IReadOnlyList<Guid>> VisibleLabelIdsAsync(IReadOnlyCollection<Guid> labelIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([.. labelIds.Where(Labels.Contains)]);

        public Task<int> CountAsync(Guid projectId, ListFilter filter, CancellationToken cancellationToken)
        {
            LastFilter = filter;

            return Task.FromResult(Count);
        }

        public Task<IReadOnlyList<TaskItem>> PageAsync(
            Guid projectId,
            ListFilter filter,
            ListSort sort,
            IReadOnlyList<Guid> statusOrder,
            ListCursor? after,
            int take,
            CancellationToken cancellationToken)
        {
            LastStatusOrder = statusOrder;
            LastTake = take;

            return Task.FromResult<IReadOnlyList<TaskItem>>([.. Tasks.Take(take)]);
        }

        public Task<IReadOnlyList<StatusDefinition>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StatusDefinition>>(_statuses);
    }
}
