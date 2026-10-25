using Microsoft.EntityFrameworkCore;
using Yello.Domain;
using Yello.Domain.Projects;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Creates, renames and lists Projects inside the request's Space-scoped unit of work. Story 2.1.
/// </summary>
/// <remarks>
/// <para>
/// <b>No <c>Where</c> on <c>SpaceId</c>, anywhere.</b> Both isolation layers are already narrowed to
/// the request's Space (see <see cref="StatusDefinitionReader"/>), so a Project in another Space is
/// invisible here exactly as one that never existed is.
/// </para>
/// <para>
/// <b>Rename writes one column.</b> <c>ExecuteUpdate</c> with a single <c>SetProperty</c> on
/// <c>Name</c>: the statement cannot touch the Space, and the database would refuse it if it did.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
/// <param name="identifiers">Produces the Project's identity (AC6).</param>
/// <param name="clock">Stamps the creation instant.</param>
internal sealed class ProjectStore(YelloDbContext dbContext, IIdentifierGenerator identifiers, TimeProvider clock)
    : IProjectStore
{
    /// <inheritdoc />
    public async Task<Project> CreateAsync(Guid spaceId, string name, CancellationToken cancellationToken)
    {
        var project = new Project
        {
            Id = identifiers.Next(),
            SpaceId = spaceId,
            Name = name,
            CreatedAt = clock.GetUtcNow(),
        };

        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return project;
    }

    /// <inheritdoc />
    public async Task<Project?> RenameAsync(Guid projectId, string name, CancellationToken cancellationToken)
    {
        var renamed = await dbContext.Projects
            .Where(project => project.Id == projectId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(project => project.Name, name), cancellationToken)
            .ConfigureAwait(false);

        if (renamed == 0)
        {
            return null;
        }

        return await dbContext.Projects
            .AsNoTracking()
            .SingleAsync(project => project.Id == projectId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Project>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Projects
            .AsNoTracking()
            .OrderBy(project => project.Name)
            .ThenBy(project => project.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
