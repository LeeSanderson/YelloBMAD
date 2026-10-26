using Xunit;
using Yello.Application.Labels.DefineLabel;
using Yello.Application.Spaces;
using Yello.Domain.Labels;
using Yello.Domain.Memberships;

namespace Yello.Tests.Slices.Labels.DefineLabel;

/// <summary>
/// Story 2.4 Tasks 2 and 5 - the definition slice stamps the resolved Space on the Label and answers a taken name as
/// such, and the Domain rules it leans on (the name's trim and length, the palette's closed set) hold their edges.
/// AC1, AC4, AC11; FR-22, AR-34.
/// </summary>
/// <remarks>
/// No database: the wire cases in <c>TaskEndpointTests.Labels</c> hold the unique index, the row and the problem
/// bodies.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "FR-22")]
[Trait("Requirement", "AR-3")]
public sealed class DefineLabelHandlerTests
{
    /// <summary>
    /// AC1, AD-2 - the Label is defined in the Space the pipeline resolved, never one the request named.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Requirement", "AD-2")]
    public async Task A_Label_is_defined_in_the_resolved_Space()
    {
        var store = new RecordingStore(nameTaken: false);
        var context = new ActiveSpaceContext(Guid.NewGuid(), Guid.NewGuid(), Role.Admin);

        var result = await new DefineLabelHandler(store).HandleAsync(context, "Bug", "rust", TestContext.Current.CancellationToken);

        Assert.Equal(DefineLabelOutcome.Defined, result.Outcome);
        Assert.Equal((context.SpaceId, "Bug", "rust"), Assert.Single(store.Calls));
        Assert.Equal(context.SpaceId, result.Label?.SpaceId);
    }

    /// <summary>
    /// AC11, Q2 - a name the Space already holds is <see cref="DefineLabelOutcome.NameTaken"/>, with no Label.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    [Trait("Assumption", "FR-22-Q2")]
    public async Task A_name_the_Space_already_holds_is_taken()
    {
        var context = new ActiveSpaceContext(Guid.NewGuid(), Guid.NewGuid(), Role.Owner);

        var result = await new DefineLabelHandler(new RecordingStore(nameTaken: true)).HandleAsync(context, "Bug", "rust", TestContext.Current.CancellationToken);

        Assert.Equal(DefineLabelOutcome.NameTaken, result.Outcome);
        Assert.Null(result.Label);
    }

    /// <summary>
    /// Q2 - a name is trimmed, then 1-50 characters.
    /// </summary>
    /// <param name="submitted">The name as sent.</param>
    /// <param name="expected">The normalised name, or <c>null</c> when refused.</param>
    [Theory]
    [Trait("Assumption", "FR-22-Q2")]
    [InlineData("  Bug  ", "Bug")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void A_Label_name_is_trimmed_and_required(string? submitted, string? expected)
    {
        Assert.Equal(expected is not null, LabelName.TryNormalise(submitted, out var name));
        Assert.Equal(expected ?? string.Empty, name);
    }

    /// <summary>
    /// Q2 - fifty characters is a name; fifty-one is not.
    /// </summary>
    [Fact]
    [Trait("Assumption", "FR-22-Q2")]
    public void A_Label_name_is_at_most_fifty_characters()
    {
        Assert.True(LabelName.TryNormalise(new string('a', 50), out _));
        Assert.False(LabelName.TryNormalise(new string('a', 51), out _));
    }

    /// <summary>
    /// AC4, Q3 - the palette is eight keys, compared exactly; a colour value or another word is not one.
    /// </summary>
    [Fact]
    [Trait("Requirement", "UX-DR13")]
    public void The_palette_is_a_closed_set_of_keys()
    {
        Assert.Equal(8, LabelPalette.Keys.Count);
        Assert.All(LabelPalette.Keys, key => Assert.True(LabelPalette.IsKnown(key)));
        Assert.False(LabelPalette.IsKnown("magenta"));
        Assert.False(LabelPalette.IsKnown("#D2592D"));
        Assert.False(LabelPalette.IsKnown("Rust"));
        Assert.False(LabelPalette.IsKnown(null));
    }

    private sealed class RecordingStore(bool nameTaken) : ILabelStore
    {
        public List<(Guid SpaceId, string Name, string Colour)> Calls { get; } = [];

        public Task<IReadOnlyList<Label>> ListAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException("The definition slice lists nothing.");

        public Task<Label?> CreateAsync(Guid spaceId, string name, string colour, CancellationToken cancellationToken)
        {
            Calls.Add((spaceId, name, colour));

            return Task.FromResult(nameTaken
                ? null
                : new Label { Id = Guid.NewGuid(), SpaceId = spaceId, Name = name, Colour = colour, CreatedAt = DateTimeOffset.UnixEpoch });
        }

        public Task<LabelDeletion> DeleteAsync(Guid labelId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The definition slice deletes nothing.");
    }
}
