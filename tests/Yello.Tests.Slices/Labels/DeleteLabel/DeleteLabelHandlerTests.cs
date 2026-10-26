using Xunit;
using Yello.Application.Labels.DeleteLabel;
using Yello.Domain.Labels;

namespace Yello.Tests.Slices.Labels.DeleteLabel;

/// <summary>
/// Story 2.4 Task 5 - the deletion slice answers what the store decided under its lock, and decides nothing itself.
/// AC3; FR-22.
/// </summary>
/// <remarks>
/// No database: <c>TaskEndpointTests.Labels</c> and <c>.LabelsRace</c> hold the lock, the in-use probe and the
/// <c>NoAction</c> backstop.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "FR-22")]
[Trait("Requirement", "AR-3")]
public sealed class DeleteLabelHandlerTests
{
    /// <summary>
    /// AC3 - each outcome the store reaches is the slice's answer, for the Label the route named.
    /// </summary>
    /// <param name="outcome">What the store decided.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(LabelDeletion.Deleted)]
    [InlineData(LabelDeletion.InUse)]
    [InlineData(LabelDeletion.NotFound)]
    public async Task The_store_decides_and_the_slice_answers_it(LabelDeletion outcome)
    {
        var store = new FixedStore(outcome);
        var labelId = Guid.NewGuid();

        Assert.Equal(outcome, await new DeleteLabelHandler(store).HandleAsync(labelId, TestContext.Current.CancellationToken));
        Assert.Equal(labelId, Assert.Single(store.Deleted));
    }

    private sealed class FixedStore(LabelDeletion outcome) : ILabelStore
    {
        public List<Guid> Deleted { get; } = [];

        public Task<IReadOnlyList<Label>> ListAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("The deletion slice lists nothing.");

        public Task<Label?> CreateAsync(Guid spaceId, string name, string colour, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The deletion slice defines nothing.");

        public Task<LabelDeletion> DeleteAsync(Guid labelId, CancellationToken cancellationToken)
        {
            Deleted.Add(labelId);

            return Task.FromResult(outcome);
        }
    }
}
