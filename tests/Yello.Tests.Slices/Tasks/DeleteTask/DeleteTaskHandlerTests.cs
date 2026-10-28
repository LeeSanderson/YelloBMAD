using Xunit;
using Yello.Application.Tasks.DeleteTask;
using Yello.Domain.Tasks;

namespace Yello.Tests.Slices.Tasks.DeleteTask;

/// <summary>
/// Story 2.5 Task 2 - the Task deletion slice answers what the store decided under its locks, and decides nothing
/// itself. AC7; FR-23.
/// </summary>
/// <remarks>
/// No database: <c>TaskEndpointTests.Delete</c> and <c>.DeleteRace</c> hold the locks and the cascade.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "FR-23")]
[Trait("Requirement", "AR-3")]
public sealed class DeleteTaskHandlerTests
{
    /// <summary>
    /// AC7 - each outcome the store reaches is the slice's answer, for the Task the route named.
    /// </summary>
    /// <param name="outcome">What the store decided.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(TaskDeletion.Deleted)]
    [InlineData(TaskDeletion.NotFound)]
    public async Task The_store_decides_and_the_slice_answers_it(TaskDeletion outcome)
    {
        var store = new FixedStore(outcome);
        var taskId = Guid.NewGuid();

        Assert.Equal(outcome, await new DeleteTaskHandler(store).HandleAsync(taskId, TestContext.Current.CancellationToken));
        Assert.Equal(taskId, Assert.Single(store.Deleted));
    }

    private sealed class FixedStore(TaskDeletion outcome) : ITaskStore
    {
        public List<Guid> Deleted { get; } = [];

        public Task<TaskItem?> CreateAsync(Guid spaceId, Guid projectId, Guid statusId, string title, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The deletion slice creates nothing.");

        public Task<TaskItem?> FindAsync(Guid taskId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The deletion slice reads nothing.");

        public Task<TaskItem?> EditAsync(Guid taskId, TaskEdit edit, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The deletion slice edits nothing.");

        public Task<bool> ReplaceLabelsAsync(Guid taskId, IReadOnlyList<Guid> labelIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The deletion slice applies no Label.");

        public Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LabelIdsOfAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The deletion slice reads no Label.");

        public Task<TaskDeletion> DeleteAsync(Guid taskId, CancellationToken cancellationToken)
        {
            Deleted.Add(taskId);

            return Task.FromResult(outcome);
        }

        public Task<TaskMove> MoveAsync(Guid taskId, Guid statusId, Guid? afterTaskId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The deletion slice moves nothing.");
    }
}
