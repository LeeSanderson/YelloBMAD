using Xunit;
using Yello.Application.Tasks.EditTaskAttributes;
using Yello.Domain.Statuses;
using Yello.Domain.Tasks;

namespace Yello.Tests.Slices.Tasks.EditTaskAttributes;

/// <summary>
/// Story 2.3 Tasks 2 and 5 - the edit slice checks a Status against the effective set before anything is
/// written, answers a Task it cannot see as not found, and the Domain rules it leans on (the due date's
/// normalisation and range, effective-set membership) hold their edges. AC4, AC7, AC11; FR-20, AD-17, AR-34.
/// </summary>
/// <remarks>
/// No database: the wire cases in <c>TaskEndpointTests</c> hold the row, the lock and the problem bodies.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "FR-20")]
[Trait("Requirement", "AR-3")]
public sealed class EditTaskAttributesHandlerTests
{
    /// <summary>
    /// AD-17, Q13 (a) - a Status outside the effective set is refused and the store is never asked to write,
    /// so the composite foreign key is never what refuses another Space's Status.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-17")]
    public async Task A_Status_outside_the_effective_set_is_refused_before_any_write()
    {
        var spaceId = Guid.NewGuid();
        var task = NewTask(spaceId);
        var store = new RecordingStore(task);

        var result = await new EditTaskAttributesHandler(store, new FixedStatuses(Status(spaceId, 0), Status(spaceId, 1)))
            .HandleAsync(task.Id, new TaskEdit(Title: null, StatusId: Guid.NewGuid(), ChangesDueDate: false, DueDate: null), TestContext.Current.CancellationToken);

        Assert.Equal(EditTaskAttributesOutcome.StatusOutsideTheEffectiveSet, result.Outcome);
        Assert.Empty(store.Edits);
    }

    /// <summary>
    /// AC4 - a Status in the effective set is passed to the store, which writes it.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_Status_in_the_effective_set_is_written()
    {
        var spaceId = Guid.NewGuid();
        var task = NewTask(spaceId);
        var done = Status(spaceId, 2);
        var store = new RecordingStore(task);
        var edit = new TaskEdit(Title: null, StatusId: done.Id, ChangesDueDate: false, DueDate: null);

        var result = await new EditTaskAttributesHandler(store, new FixedStatuses(Status(spaceId, 0), done))
            .HandleAsync(task.Id, edit, TestContext.Current.CancellationToken);

        Assert.Equal(EditTaskAttributesOutcome.Edited, result.Outcome);
        Assert.Equal(edit, Assert.Single(store.Edits));
    }

    /// <summary>
    /// AD-3, AD-20 - a Task the unit of work cannot see is not found, and nothing is written.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-3")]
    public async Task A_Task_that_is_not_visible_is_not_found_and_nothing_is_written()
    {
        var store = new RecordingStore(visible: null);

        var result = await new EditTaskAttributesHandler(store, new FixedStatuses())
            .HandleAsync(Guid.NewGuid(), new TaskEdit("A title", StatusId: null, ChangesDueDate: false, DueDate: null), TestContext.Current.CancellationToken);

        Assert.Equal(EditTaskAttributesOutcome.NotFound, result.Outcome);
        Assert.Empty(store.Edits);
    }

    /// <summary>
    /// Story 2.4, AC2, AC7, Q1 (a) - a Labels edit hands the whole set to the store and answers the set it now carries.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-22")]
    public async Task A_Labels_edit_replaces_the_whole_set_and_answers_it()
    {
        var spaceId = Guid.NewGuid();
        var task = NewTask(spaceId);
        var store = new RecordingStore(task);
        IReadOnlyList<Guid> labels = [Guid.NewGuid(), Guid.NewGuid()];

        var result = await new EditTaskAttributesHandler(store, new FixedStatuses())
            .HandleAsync(task.Id, new TaskEdit(null, null, false, null, ChangesLabels: true, LabelIds: labels), TestContext.Current.CancellationToken);

        Assert.Equal(EditTaskAttributesOutcome.Edited, result.Outcome);
        Assert.Equal(labels, Assert.Single(store.Replacements));
        Assert.Equal(labels, result.LabelIds);
    }

    /// <summary>
    /// Story 2.4, AC10, AC11 - a set the store cannot see whole (unknown, another Space's, or deleted under the edit) is
    /// <see cref="EditTaskAttributesOutcome.LabelsInvalid"/>, never an edit.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-22")]
    public async Task A_Labels_edit_the_store_cannot_see_whole_is_refused()
    {
        var spaceId = Guid.NewGuid();
        var task = NewTask(spaceId);
        var store = new RecordingStore(task, labelsVisible: false);

        var result = await new EditTaskAttributesHandler(store, new FixedStatuses())
            .HandleAsync(task.Id, new TaskEdit(null, null, false, null, ChangesLabels: true, LabelIds: [Guid.NewGuid()]), TestContext.Current.CancellationToken);

        Assert.Equal(EditTaskAttributesOutcome.LabelsInvalid, result.Outcome);
        Assert.Null(result.Task);
    }

    /// <summary>
    /// Story 2.4, AC7 - an edit that does not name the Labels never asks the store to replace them.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "FR-22")]
    public async Task An_edit_without_the_Labels_leaves_them_alone()
    {
        var spaceId = Guid.NewGuid();
        var task = NewTask(spaceId);
        var store = new RecordingStore(task);

        var result = await new EditTaskAttributesHandler(store, new FixedStatuses())
            .HandleAsync(task.Id, new TaskEdit("Renamed", null, false, null), TestContext.Current.CancellationToken);

        Assert.Equal(EditTaskAttributesOutcome.Edited, result.Outcome);
        Assert.Empty(store.Replacements);
    }

    /// <summary>
    /// AR-34, Q4 (a) - a due date is the same instant at offset zero; <c>null</c> is no due date.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AR-34")]
    public void A_due_date_is_normalised_to_the_same_instant_in_UTC()
    {
        var sent = new DateTimeOffset(2026, 11, 3, 9, 30, 0, TimeSpan.FromHours(5.5));

        Assert.True(TaskDueDate.TryNormalise(sent, out var due));
        Assert.Equal(sent, due);
        Assert.Equal(TimeSpan.Zero, due!.Value.Offset);

        Assert.True(TaskDueDate.TryNormalise(null, out var none));
        Assert.Null(none);
    }

    /// <summary>
    /// Q4 (a) - the range is the years 2000 to 2100, judged on the day as submitted in its own offset (the day
    /// the editor chose), so the range's first and last days are due dates in every zone.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month.</param>
    /// <param name="day">The day.</param>
    /// <param name="offsetHours">The offset the instant is written with.</param>
    /// <param name="accepted">Whether it is a due date.</param>
    [Theory]
    [InlineData(2000, 1, 1, 0, true)]
    [InlineData(1999, 12, 31, 0, false)]
    [InlineData(2000, 1, 1, 14, true)]
    [InlineData(1999, 12, 31, -12, false)]
    [InlineData(2100, 12, 31, 0, true)]
    [InlineData(2100, 12, 31, -12, true)]
    [InlineData(2101, 1, 1, 0, false)]
    [InlineData(2101, 1, 1, 14, false)]
    public void A_due_date_is_between_2000_and_2100_on_the_day_submitted(int year, int month, int day, int offsetHours, bool accepted)
    {
        var submitted = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.FromHours(offsetHours));

        Assert.Equal(accepted, TaskDueDate.TryNormalise(submitted, out var due));
        Assert.Equal(accepted, due is not null);
    }

    /// <summary>
    /// AD-17 - membership of the effective set is by id.
    /// </summary>
    [Fact]
    [Trait("Requirement", "AD-17")]
    public void The_effective_set_holds_its_own_Statuses_and_no_other()
    {
        var spaceId = Guid.NewGuid();
        var effective = EffectiveStatusSet.Of([Status(spaceId, 1), Status(spaceId, 0)]);

        Assert.All(effective, status => Assert.True(EffectiveStatusSet.Contains(effective, status.Id)));
        Assert.False(EffectiveStatusSet.Contains(effective, Guid.NewGuid()));
    }

    private static TaskItem NewTask(Guid spaceId) => new()
    {
        Id = Guid.NewGuid(),
        SpaceId = spaceId,
        ProjectId = Guid.NewGuid(),
        StatusId = Guid.NewGuid(),
        Title = "Draft the brief",
        PositionKey = PositionKey.First,
        CreatedAt = DateTimeOffset.UnixEpoch,
    };

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

    private sealed class RecordingStore(TaskItem? visible, bool labelsVisible = true) : ITaskStore
    {
        public List<TaskEdit> Edits { get; } = [];

        public List<IReadOnlyList<Guid>> Replacements { get; } = [];

        public Task<TaskItem?> CreateAsync(Guid spaceId, Guid projectId, Guid statusId, string title, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The edit slice creates no Task.");

        public Task<TaskItem?> FindAsync(Guid taskId, CancellationToken cancellationToken) =>
            Task.FromResult(visible?.Id == taskId ? visible : null);

        public Task<TaskItem?> EditAsync(Guid taskId, TaskEdit edit, CancellationToken cancellationToken)
        {
            Edits.Add(edit);

            return Task.FromResult(visible);
        }

        public Task<bool> ReplaceLabelsAsync(Guid taskId, IReadOnlyList<Guid> labelIds, CancellationToken cancellationToken)
        {
            Replacements.Add(labelIds);

            return Task.FromResult(labelsVisible);
        }

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LabelIdsOfAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>>(
                Replacements.Count > 0 && visible is not null
                    ? new Dictionary<Guid, IReadOnlyList<Guid>> { [visible.Id] = Replacements[^1] }
                    : new Dictionary<Guid, IReadOnlyList<Guid>>());
    }
}
