using Xunit;
using Yello.Application.Boards.ViewBoard;
using Yello.Domain.Boards;
using Yello.Domain.Projects;
using Yello.Domain.Statuses;
using Yello.Domain.Tasks;

namespace Yello.Tests.Slices.Boards.ViewBoard;

/// <summary>
/// Story 2.2 Task 4 and Q6 - the Board slice takes each column's total from the database's count, never
/// from the Tasks it places, and places each Task in exactly the column of its own Status. AC4, AC5; FR-28,
/// AD-16, AD-17, AD-29.
/// </summary>
/// <remarks>
/// <b>The case the wire cannot hold.</b> Under Q6 (a) the Board returns every Task, so over the wire a total
/// and its rows are always equal, and a total taken from <c>Tasks.Count</c> would pass every wire case. Here
/// the reader returns fewer Tasks than it counts - the shape story 2.9's paging will produce - so only a
/// total taken from the count is right.
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
        var first = world.NewTask(done);
        var second = world.NewTask(todo);
        var third = world.NewTask(done);

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
    /// AD-17 - a Task holding a Status outside the effective set is a broken invariant, and fails loudly
    /// rather than vanishing from every column.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    public async Task A_Task_outside_the_effective_set_fails_loudly()
    {
        var world = new BoardWorld();

        _ = world.Status(0);
        world.Tasks.Add(world.NewTask(new StatusDefinition { Id = Guid.NewGuid(), SpaceId = world.SpaceId, Name = "Gone", Position = 9, CreatedAt = DateTimeOffset.UnixEpoch }));

        await Assert.ThrowsAsync<InvalidOperationException>(world.HandleAsync);
    }

    private sealed class BoardWorld : IBoardReader, IStatusDefinitionReader
    {
        private readonly List<StatusDefinition> _statuses = [];

        public Guid SpaceId { get; } = Guid.NewGuid();

        public Guid ProjectId { get; } = Guid.NewGuid();

        public bool ProjectVisible { get; init; } = true;

        public List<TaskItem> Tasks { get; } = [];

        public Dictionary<Guid, int> Counts { get; } = [];

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

        public Task<BoardView?> HandleAsync() =>
            new ViewBoardHandler(this, this).HandleAsync(ProjectId, TestContext.Current.CancellationToken);

        public Task<Project?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult(ProjectVisible && projectId == ProjectId
                ? new Project { Id = ProjectId, SpaceId = SpaceId, Name = "Board", CreatedAt = DateTimeOffset.UnixEpoch }
                : null);

        public Task<IReadOnlyDictionary<Guid, int>> CountTasksByStatusAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, int>>(Counts);

        public Task<IReadOnlyList<TaskItem>> ListTasksAsync(Guid projectId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TaskItem>>(Tasks);

        public Task<IReadOnlyList<StatusDefinition>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StatusDefinition>>(_statuses);

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LabelIdsOfAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(new Dictionary<Guid, IReadOnlyList<Guid>>());
    }
}
