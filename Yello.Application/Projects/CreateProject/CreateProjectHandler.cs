using Yello.Application.Spaces;
using Yello.Domain.Projects;

namespace Yello.Application.Projects.CreateProject;

/// <summary>
/// Creates a Project in the active Space. FR-17; story 2.1, AC2.
/// </summary>
/// <remarks>
/// <b>Everything cross-cutting happened before this ran</b> (AR-3): the pipeline resolved the Space,
/// checked <c>ManageProjects</c> against the matrix, consulted the Projects-per-Space bound and replayed
/// an <c>Idempotency-Key</c> it had already seen. This slice reads the Space off the resolved context -
/// never its Role, and never a Space from the request.
/// </remarks>
/// <param name="store">Writes the Project.</param>
public sealed class CreateProjectHandler(IProjectStore store)
{
    /// <summary>
    /// Creates the Project.
    /// </summary>
    /// <param name="context">The active Space, as the pipeline resolved it on this request.</param>
    /// <param name="name">The name, already normalised.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The created Project.</returns>
    public Task<Project> HandleAsync(ActiveSpaceContext context, string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return store.CreateAsync(context.SpaceId, name, cancellationToken);
    }
}
