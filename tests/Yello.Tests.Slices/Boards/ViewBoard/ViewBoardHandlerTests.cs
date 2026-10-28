using Xunit;
using Yello.Application.Boards;
using Yello.Application.Boards.ViewBoard;
using Yello.Application.Boards.ViewBoardColumn;
using Yello.Domain.Boards;
using Yello.Domain.Projects;
using Yello.Domain.Statuses;
using Yello.Domain.Tasks;

namespace Yello.Tests.Slices.Boards.ViewBoard;

/// <summary>
/// Story 2.2 Task 4 and Q6 - the Board slice takes each column's total from the database's count, never
/// from the Tasks it places, and places each Task in exactly the column of its own Status. AC4, AC5; FR-28,
/// AD-16, AD-17, AD-29. Story 2.9 - the Board's first page per column is one bounded seek each, its cursor is the last
/// served key exactly when another follows, and the column page answers the same way. AC1, AC9.
/// </summary>
/// <remarks>
/// <b>The case the wire cannot hold.</b> Here the reader returns fewer Tasks than it counts, so only a total taken
/// from the count is right.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P0")]
[Trait("Requirement", "FR-28")]
[Trait("Requirement", "AD-29")]
public sealed class ViewBoardHandlerTests
{
    /// <summary>
    /// AC5, AD-29 - a column's total is the reader's count for its Status, even where the reader returned
    /// fewer Tasks, and a Status holding none totals 0.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_columns_total_is_the_count_not_the_number_of_Tasks_placed()
    {
        var world = new BoardWorld();
        var todo = world.Status(0);
        var doing = world.Status(1);
        var done = world.Status(2);

        world.Tasks.Add(world.NewTask(todo));
        world.Tasks.Add(world.NewTask(doing));
        world.Counts[todo.Id] = 7;
        world.Counts[doing.Id] = 1;

        var board = await world.HandleAsync();

        Assert.NotNull(board);
        Assert.Equal([7, 1, 0], board.Columns.Select(column => column.Total));
        Assert.Equal([1, 1, 0], board.Columns.Select(column => column.Tasks.Count));
        Assert.Equal([todo.Id, doing.Id, done.Id], board.Columns.Select(column => column.Status.Id));
    }

    /// <summary>
    /// AC4 - every Task lands in the column of its own Status and in no other, in the reader's order.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Each_Task_lands_in_exactly_the_column_of_its_Status()
    {
        var world = new BoardWorld();
        var todo = world.Status(0);
        var done = world.Status(1);
        var first = world.NewTask(done, "V0001");
        var second = world.NewTask(todo);
        var third = world.NewTask(done, "V0002");

        world.Tasks.AddRange([first, second, third]);

        var board = await world.HandleAsync();

        Assert.NotNull(board);
        Assert.Equal([second], board.Columns[0].Tasks);
        Assert.Equal([first, third], board.Columns[1].Tasks);
    }

    /// <summary>
    /// AC9 - a Project the reader cannot see answers no Board, which the endpoint turns into the boundary's
    /// <c>404</c>.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_Project_that_is_not_visible_has_no_Board()
    {
        var world = new BoardWorld { ProjectVisible = false };

        Assert.Null(await world.HandleAsync());
    }

    /// <summary>
    /// Story 2.9, AC1, P-2 - first paint is one bounded seek per column of the effective set, each from the column's start
    /// and asking for one more than a page: never one read of the whole Project, never a Status outside the set. This
    /// replaces 2.2's "a Task outside the effective set fails loudly" (AD-17): each column now reads only its own
    /// Status, so such a Task - which the composite foreign key forbids - is read into no column, never into two.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    [Trait("Requirement", "AR-22")]
    public async Task First_paint_is_one_bounded_seek_per_column_of_the_effective_set()
    {
        var world = new BoardWorld();
        var todo = world.Status(0);
        var done = world.Status(1);

        world.Tasks.Add(world.NewTask(new StatusDefinition { Id = Guid.NewGuid(), SpaceId = world.SpaceId, Name = "Gone", Position = 9, CreatedAt = DateTimeOffset.UnixEpoch }));

        var board = await world.HandleAsync();

        Assert.NotNull(board);
        Assert.Equal([(todo.Id, null, BoardPage.Size + 1), (done.Id, null, BoardPage.Size + 1)], world.Seeks);
        Assert.All(board.Columns, column => Assert.Empty(column.Tasks));
    }

    /// <summary>
    /// Story 2.9, AC9 - a column of 51 answers its first 50 and a cursor that is the 50th Task's key; a column of exactly 50
    /// answers all of them and no cursor.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_column_carries_a_cursor_exactly_when_a_Task_follows_its_first_page()
    {
        var world = new BoardWorld();
        var todo = world.Status(0);
        var doing = world.Status(1);

        world.AddColumn(todo, BoardPage.Size + 1);
        world.AddColumn(doing, BoardPage.Size);

        var board = await world.HandleAsync();

        Assert.NotNull(board);
        Assert.Equal([BoardPage.Size, BoardPage.Size], board.Columns.Select(column => column.Tasks.Count));
        Assert.Equal(board.Columns[0].Tasks[^1].PositionKey, board.Columns[0].NextAfter);
        Assert.Null(board.Columns[1].NextAfter);
    }

    /// <summary>
    /// Story 2.9, AC1, AC4, AC9 - the column page is the page after the cursor, with the column's own count as its total
    /// and the next cursor; the last page has none.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_column_page_is_the_page_after_its_cursor_with_the_columns_count()
    {
        var world = new BoardWorld();
        var todo = world.Status(0);

        world.AddColumn(todo, 120);
        world.Counts[todo.Id] = 4_812;

        var second = await world.PageAsync(todo.Id, world.Tasks[49].PositionKey);

        Assert.NotNull(second);

        var third = await world.PageAsync(todo.Id, second.NextAfter);

        Assert.NotNull(third);
        Assert.Equal(4_812, second.Total);
        Assert.Equal(world.Tasks.Skip(50).Take(50), second.Tasks);
        Assert.Equal(world.Tasks[99].PositionKey, second.NextAfter);
        Assert.Equal(world.Tasks.Skip(100), third.Tasks);
        Assert.Null(third.NextAfter);
    }

    /// <summary>
    /// Story 2.9, AC9 - a column page for a Status outside the effective set, or of a Project that is not visible, is no
    /// page: the endpoint answers the boundary's <c>404</c>. Nothing is sought.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_column_page_outside_the_effective_set_or_of_an_invisible_Project_is_no_page()
    {
        var world = new BoardWorld();
        var todo = world.Status(0);
        var hidden = new BoardWorld { ProjectVisible = false };
        var hiddenTodo = hidden.Status(0);

        Assert.Null(await world.PageAsync(Guid.NewGuid(), after: null));
        Assert.Null(await hidden.PageAsync(hiddenTodo.Id, after: null));
        Assert.Empty(world.Seeks);
        Assert.Empty(hidden.Seeks);
        Assert.NotNull(await world.PageAsync(todo.Id, after: null));
    }

    private sealed class BoardWorld : IBoardReader, IStatusDefinitionReader
    {
        private readonly List<StatusDefinition> _statuses = [];

        public Guid SpaceId { get; } = Guid.NewGuid();

        public Guid ProjectId { get; } = Guid.NewGuid();

        public bool ProjectVisible { get; init; } = true;

        public List<TaskItem> Tasks { get; } = [];

        public Dictionary<Guid, int> Counts { get; } = [];

        public List<(Guid StatusId, string? After, int Take)> Seeks { get; } = [];

        public StatusDefinition Status(int position)
        {
            var status = new StatusDefinition { Id = Guid.NewGuid(), SpaceId = SpaceId, Name = $"Status {position}", Position = position, CreatedAt = DateTimeOffset.UnixEpoch };

            _statuses.Add(status);

            return status;
        }

        public TaskItem NewTask(StatusDefinition status, string? key = null) => new()
        {
            Id = Guid.NewGuid(),
            SpaceId = SpaceId,
            ProjectId = ProjectId,
            StatusId = status.Id,
            Title = $"Task {Guid.NewGuid():N}",
            PositionKey = key ?? PositionKey.First,
            CreatedAt = DateTimeOffset.UnixEpoch,
        };

        public void AddColumn(StatusDefinition status, int count)
        {
            for (var index = 1; index <= count; index++)
            {
                Tasks.Add(NewTask(status, string.Create(System.Globalization.CultureInfo.InvariantCulture, $"V{index:D4}")));
            }
        }

        public Task<BoardView?> HandleAsync() =>
            new ViewBoardHandler(this, this).HandleAsync(ProjectId, TestContext.Current.CancellationToken);

        public Task<BoardColumnPage?> PageAsync(Guid statusId, string? after) =>
            new ViewBoardColumnHandler(this, this).HandleAsync(ProjectId, statusId, after, TestContext.Current.CancellationToken);

        public Task<Project?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(ProjectVisible && projectId == ProjectId
                ? new Project { Id = ProjectId, SpaceId = SpaceId, Name = "Board", CreatedAt = DateTimeOffset.UnixEpoch }
                : null);

        public Task<IReadOnlyDictionary<Guid, int>> CountTasksByStatusAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, int>>(Counts);

        public Task<int> CountTasksAsync(Guid projectId, Guid statusId, CancellationToken cancellationToken) =>
            Task.FromResult(Counts.GetValueOrDefault(statusId));

        public Task<IReadOnlyList<TaskItem>> PageAsync(Guid projectId, Guid statusId, string? after, int take, CancellationToken cancellationToken)
        {
            Seeks.Add((statusId, after, take));

            return Task.FromResult<IReadOnlyList<TaskItem>>(
            [
                .. Tasks
                    .Where(task => task.StatusId == statusId && (after is null || string.CompareOrdinal(task.PositionKey, after) > 0))
                    .OrderBy(task => task.PositionKey, StringComparer.Ordinal)
                    .Take(take),
            ]);
        }

        public Task<IReadOnlyList<StatusDefinition>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StatusDefinition>>(_statuses);

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LabelIdsOfAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(new Dictionary<Guid, IReadOnlyList<Guid>>());
    }
}
