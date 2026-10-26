using Xunit;
using Yello.Application.Spaces;
using Yello.Application.Tasks.CreateTask;
using Yello.Domain.Memberships;
using Yello.Domain.Statuses;
using Yello.Domain.Tasks;

namespace Yello.Tests.Slices.Tasks.CreateTask;

/// <summary>
/// Story 2.2 Tasks 2 and 4 - the create slice takes its Space from the pipeline's context and its Status
/// from the effective set, and the Domain rules it leans on (title, position key, effective set) hold
/// their edges. AC2; FR-19, AD-15, AD-16, AR-34.
/// </summary>
/// <remarks>
/// No database: the wire cases in <c>TaskEndpointTests</c> hold the row, the generator's id and the
/// in-transaction serialisation the position key relies on.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "FR-19")]
[Trait("Requirement", "AR-3")]
public sealed class CreateTaskHandlerTests
{
    /// <summary>
    /// AC2, AD-16 - the Task is created in the context's Space, whatever the context's Role, and in the
    /// Status with the lowest position, however the reader happened to order them.
    /// </summary>
    /// <param name="role">The Role on the context, which must make no difference.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(Role.Owner)]
    [InlineData(Role.Viewer)]
    [Trait("Requirement", "AD-16")]
    public async Task The_Task_takes_the_resolved_Space_and_the_first_Status_of_the_effective_set(Role role)
    {
        var context = new ActiveSpaceContext(Guid.NewGuid(), Guid.NewGuid(), role);
        var first = Status(context.SpaceId, position: 0);
        var store = new RecordingStore();
        var projectId = Guid.NewGuid();

        var created = await new CreateTaskHandler(store, new FixedStatuses(Status(context.SpaceId, 2), first, Status(context.SpaceId, 1)))
            .HandleAsync(context, projectId, "Draft the brief", TestContext.Current.CancellationToken);

        Assert.NotNull(created);
        Assert.Equal((context.SpaceId, projectId, first.Id, "Draft the brief"), Assert.Single(store.Calls));
    }

    /// <summary>
    /// Q3 (a) - a title is trimmed and 1-200 characters; blank or 201 is refused.
    /// </summary>
    /// <param name="length">The title's length before padding.</param>
    /// <param name="accepted">Whether it is a title.</param>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void A_title_is_trimmed_and_1_to_200_characters(int length, bool accepted)
    {
        Assert.Equal(accepted, TaskTitle.TryNormalise("  " + new string('t', length) + "  ", out var title));
        Assert.Equal(accepted ? length : 0, title.Length);
    }

    /// <summary>
    /// AD-15, Q2 (a) - an empty column's first key is the fixed first key, and every next key sorts strictly
    /// after the last in ordinal order, carrying from the right and lengthening only when every character is
    /// the alphabet's last. The seeded digit-only keys the bound cases write sort before what follows them.
    /// </summary>
    /// <param name="last">The column's last key.</param>
    /// <param name="expected">The key that follows it.</param>
    [Theory]
    [InlineData(null, "V0000")]
    [InlineData("", "V0000")]
    [InlineData("V0000", "V0001")]
    [InlineData("V0009", "V000A")]
    [InlineData("V000Z", "V000a")]
    [InlineData("V000z", "V0010")]
    [InlineData("004999", "00499A")]
    [InlineData("zz", "zzV")]
    [InlineData("a-", "a0")]
    [Trait("Requirement", "AD-15")]
    public void A_position_key_sorts_strictly_after_the_last(string? last, string expected)
    {
        var next = PositionKey.After(last);

        Assert.Equal(expected, next);
        Assert.True(string.CompareOrdinal(next, last) > 0);
    }

    /// <summary>
    /// AD-15 - five thousand appends to one column - the bound - stay strictly increasing and never lengthen
    /// the key, so a full Project's keys are as short as its first.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-15")]
    public void Five_thousand_appends_stay_ordered_and_the_same_length()
    {
        string? last = null;

        for (var index = 0; index < 5_000; index++)
        {
            var next = PositionKey.After(last);

            Assert.True(last is null || string.CompareOrdinal(next, last) > 0, $"{next} does not follow {last}.");
            last = next;
        }

        Assert.Equal(PositionKey.First.Length, last!.Length);
    }

    /// <summary>
    /// AD-16, <c>deferred-work.md:271</c> - the effective set orders by position and keeps the reader's order
    /// for a tie, which is the database's <c>uniqueidentifier</c> order rather than .NET's.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-16")]
    public void The_effective_set_orders_by_position_and_keeps_the_readers_order_on_a_tie()
    {
        var spaceId = Guid.NewGuid();
        var tiedFirst = Status(spaceId, 1);
        var tiedSecond = Status(spaceId, 1);
        var lowest = Status(spaceId, 0);

        Assert.Equal([lowest, tiedFirst, tiedSecond], EffectiveStatusSet.Of([tiedFirst, tiedSecond, lowest]));
        Assert.Equal([lowest, tiedSecond, tiedFirst], EffectiveStatusSet.Of([tiedSecond, lowest, tiedFirst]));
    }

    private static StatusDefinition Status(Guid spaceId, int position) => new()
    {
        Id = Guid.NewGuid(),
        SpaceId = spaceId,
        Name = $"Status {position}",
        Position = position,
        CreatedAt = DateTimeOffset.UnixEpoch,
    };

    private sealed class FixedStatuses(params StatusDefinition[] statuses) : IStatusDefinitionReader
    {
        public Task<IReadOnlyList<StatusDefinition>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StatusDefinition>>(statuses);
    }

    private sealed class RecordingStore : ITaskStore
    {
        public List<(Guid SpaceId, Guid ProjectId, Guid StatusId, string Title)> Calls { get; } = [];

        public Task<TaskItem?> CreateAsync(Guid spaceId, Guid projectId, Guid statusId, string title, CancellationToken cancellationToken)
        {
            Calls.Add((spaceId, projectId, statusId, title));

            return Task.FromResult<TaskItem?>(new TaskItem
            {
                Id = Guid.NewGuid(),
                SpaceId = spaceId,
                ProjectId = projectId,
                StatusId = statusId,
                Title = title,
                PositionKey = PositionKey.First,
                CreatedAt = DateTimeOffset.UnixEpoch,
            });
        }

        public Task<TaskItem?> FindAsync(Guid taskId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The create slice reads no Task.");

        public Task<TaskItem?> EditAsync(Guid taskId, TaskEdit edit, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The create slice edits no Task.");

        public Task<bool> ReplaceLabelsAsync(Guid taskId, IReadOnlyList<Guid> labelIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The create slice applies no Label.");

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LabelIdsOfAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The create slice reads no Label.");
    }
}
