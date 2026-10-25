using Xunit;
using Yello.Application.Projects.CreateProject;
using Yello.Application.Spaces;
using Yello.Domain.Memberships;
using Yello.Domain.Projects;

namespace Yello.Tests.Slices.Projects.CreateProject;

/// <summary>
/// Story 2.1 Task 11 - the create slice takes its Space from the pipeline's resolved context and from
/// nothing else, and the name rule is the Domain's. AC2, AC7; FR-17, AR-34.
/// </summary>
/// <remarks>
/// No database: the slice is one call to its port, so what is worth holding is what it passes. The
/// wire cases in <c>ProjectEndpointTests</c> hold the rest - the row's Space, the generator's id.
/// </remarks>
[Trait("Suite", "Slices")]
[Trait("Priority", "P1")]
[Trait("Requirement", "FR-17")]
[Trait("Requirement", "AR-3")]
public sealed class CreateProjectHandlerTests
{
    /// <summary>
    /// AC2, AC7 - the Project is created in the context's Space, whatever the context's Role: the slice
    /// never reads the Role, and has no other Space to read.
    /// </summary>
    /// <param name="role">The Role on the context, which must make no difference.</param>
    /// <returns>A task.</returns>
    [Theory]
    [InlineData(Role.Owner)]
    [InlineData(Role.Viewer)]
    public async Task The_Project_is_created_in_the_Space_the_pipeline_resolved(Role role)
    {
        var store = new RecordingStore();
        var context = new ActiveSpaceContext(Guid.NewGuid(), Guid.NewGuid(), role);

        var created = await new CreateProjectHandler(store).HandleAsync(context, "Harbour", TestContext.Current.CancellationToken);

        Assert.Equal(context.SpaceId, Assert.Single(store.CreatedIn));
        Assert.Equal(context.SpaceId, created.SpaceId);
        Assert.Equal("Harbour", created.Name);
    }

    /// <summary>
    /// Q3 (a) - a name is trimmed and 1-100 characters; duplicates are a matter for nobody here.
    /// </summary>
    /// <param name="submitted">The name as sent.</param>
    /// <param name="expected">The normalised name, or <c>null</c> when refused.</param>
    [Theory]
    [InlineData("  Atlas  ", "Atlas")]
    [InlineData("A", "A")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public void A_name_is_trimmed_and_refused_when_blank(string? submitted, string? expected)
    {
        var accepted = ProjectName.TryNormalise(submitted, out var name);

        Assert.Equal(expected is not null, accepted);
        Assert.Equal(expected ?? string.Empty, name);
    }

    /// <summary>
    /// Q3 (a) - 100 characters is accepted and 101 is refused, after trimming.
    /// </summary>
    [Fact]
    public void A_name_is_at_most_100_characters_once_trimmed()
    {
        Assert.True(ProjectName.TryNormalise(" " + new string('n', ProjectName.MaxLength) + " ", out var longest));
        Assert.Equal(ProjectName.MaxLength, longest.Length);
        Assert.False(ProjectName.TryNormalise(new string('n', ProjectName.MaxLength + 1), out _));
    }

    private sealed class RecordingStore : IProjectStore
    {
        public List<Guid> CreatedIn { get; } = [];

        public Task<Project> CreateAsync(Guid spaceId, string name, CancellationToken cancellationToken)
        {
            CreatedIn.Add(spaceId);

            return Task.FromResult(new Project { Id = Guid.NewGuid(), SpaceId = spaceId, Name = name, CreatedAt = DateTimeOffset.UnixEpoch });
        }

        public Task<Project?> RenameAsync(Guid projectId, string name, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Not used by the create slice.");

        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Not used by the create slice.");
    }
}
