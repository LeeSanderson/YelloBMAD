using Xunit;
using Yello.Application.Projects.DeleteProject;
using Yello.Domain.Projects;

namespace Yello.Tests.Slices.Projects.DeleteProject;

/// <summary>
/// Story 2.5 Task 2 - the Project deletion slice answers what the store decided under its locks, and decides nothing
/// itself: no count is read and no expected count is compared (open question 2). AC7; FR-17.
/// </summary>
/// <remarks>
/// No database: <c>ProjectEndpointTests.Delete</c> and <c>.DeleteRace</c> hold the locks and the cascades.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "FR-17")]
[Trait("Requirement", "AR-3")]
public sealed class DeleteProjectHandlerTests
{
    /// <summary>
    /// AC7 - each outcome the store reaches is the slice's answer, for the Project the route named.
    /// </summary>
    /// <param name="outcome">What the store decided.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(ProjectDeletion.Deleted)]
    [InlineData(ProjectDeletion.NotFound)]
    public async Task The_store_decides_and_the_slice_answers_it(ProjectDeletion outcome)
    {
        var store = new FixedStore(outcome);
        var projectId = Guid.NewGuid();

        Assert.Equal(outcome, await new DeleteProjectHandler(store).HandleAsync(projectId, TestContext.Current.CancellationToken));
        Assert.Equal(projectId, Assert.Single(store.Deleted));
    }

    private sealed class FixedStore(ProjectDeletion outcome) : IProjectStore
    {
        public List<Guid> Deleted { get; } = [];

        public Task<Project> CreateAsync(Guid spaceId, string name, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The deletion slice creates nothing.");

        public Task<Project?> RenameAsync(Guid projectId, string name, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The deletion slice renames nothing.");

        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("The deletion slice lists nothing.");

        public Task<IReadOnlyDictionary<Guid, int>> TaskCountsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("The deletion slice counts nothing.");

        public Task<int> TaskCountAsync(Guid projectId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The deletion slice counts nothing.");

        public Task<ProjectDeletion> DeleteAsync(Guid projectId, CancellationToken cancellationToken)
        {
            Deleted.Add(projectId);

            return Task.FromResult(outcome);
        }
    }
}
