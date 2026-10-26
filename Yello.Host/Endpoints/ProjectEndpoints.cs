using System.Text.Json;
using Yello.Application.Projects.CreateProject;
using Yello.Application.Projects.DeleteProject;
using Yello.Application.Projects.ListProjects;
using Yello.Application.Projects.RenameProject;
using Yello.Application.Spaces;
using Yello.Contracts;
using Yello.Contracts.Projects;
using Yello.Domain.Bounds;
using Yello.Domain.Memberships;
using Yello.Domain.Projects;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// List, create, rename and delete the active Space's Projects. FR-17, FR-18; stories 2.1 and 2.5.
/// </summary>
/// <remarks>
/// <para>
/// <b>No Membership check, no Role, no bound count and no idempotency here, and there must never be
/// any.</b> Each route carries <c>{spaceId}</c>, so the pipeline has resolved the Space, checked the
/// declared capability, consulted the declared bound and replayed a key it had already seen before
/// this code runs (AR-3).
/// </para>
/// <para>
/// <b>Both surfaces.</b> None of these is Session-only: a Token bound to the Space reaches them exactly
/// as a Session does (AD-4, FR-35).
/// </para>
/// <para>
/// <b>Bodies are read inside the endpoint, never bound as parameters</b> - the reason
/// <see cref="ApiTokenEndpoints.IssueAsync"/> gives: a body the framework failed to bind would be
/// answered before the pipeline ran, so a non-member would get neither the boundary <c>404</c> nor its
/// record.
/// </para>
/// </remarks>
public static class ProjectEndpoints
{
    /// <summary>
    /// Registers <c>GET /api/v1/spaces/{spaceId}/projects</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapListProjects(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(ProjectRoutes.List, ListAsync)
            .RequiresCapability(Capability.ReadProjectsAndTasks);

    /// <summary>
    /// Registers <c>POST /api/v1/spaces/{spaceId}/projects</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapCreateProject(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(ProjectRoutes.Create, CreateAsync)
            .RequiresCapability(Capability.ManageProjects)
            .ConsultsBound(Bound.ProjectsPerSpace);

    /// <summary>
    /// Registers <c>PATCH /api/v1/spaces/{spaceId}/projects/{projectId}</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapRenameProject(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPatch(ProjectRoutes.Rename, RenameAsync)
            .RequiresCapability(Capability.ManageProjects);

    /// <summary>
    /// Registers <c>DELETE /api/v1/spaces/{spaceId}/projects/{projectId}</c>. Story 2.5.
    /// </summary>
    /// <remarks>
    /// <b>No bound</b>: a deletion adds nothing, so counting would refuse deletions in a full Space.
    /// </remarks>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapDeleteProject(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapDelete(ProjectRoutes.Project, DeleteAsync)
            .RequiresCapability(Capability.ManageProjects);

    /// <summary>
    /// Lists the active Space's Projects.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>200</c> with the Projects, by name then id.</returns>
    public static async Task<IResult> ListAsync(ListProjectsHandler handler, CancellationToken cancellationToken)
    {
        var projects = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(projects.Select(listed => Response(listed.Project, listed.TaskCount)).ToList());
    }

    /// <summary>
    /// Creates a Project in the active Space.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">
    /// The request: its services hold the context the pipeline resolved, and its body is read here.
    /// </param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>201</c> with the Project and its <c>Location</c>, or <c>400 invalid-project-name</c>.
    /// </returns>
    public static async Task<IResult> CreateAsync(
        CreateProjectHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var request = await ReadAsync(httpContext.Request, typeof(CreateProjectRequest), cancellationToken).ConfigureAwait(false) as CreateProjectRequest;

        if (!ProjectName.TryNormalise(request?.Name, out var name))
        {
            return InvalidName();
        }

        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();

        var project = await handler.HandleAsync(context, name, cancellationToken).ConfigureAwait(false);

        // Set on the response rather than through Results.Created, so the pipeline's idempotency record
        // reads it from one place whatever the endpoint.
        httpContext.Response.Headers.Location = ProjectRoutes.ProjectOf(project.SpaceId, project.Id);

        // A new Project holds no Tasks.
        return Results.Json(Response(project, taskCount: 0), statusCode: StatusCodes.Status201Created);
    }

    /// <summary>
    /// Renames a Project in the active Space.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request, whose body is read here.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>200</c> with the renamed Project, <c>400 invalid-project-name</c>, or the boundary's
    /// <c>404 not-available</c> - byte for byte - for an id that matches nothing in the active Space,
    /// another Space's Project included.
    /// </returns>
    public static async Task<IResult> RenameAsync(
        Guid projectId,
        RenameProjectHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var request = await ReadAsync(httpContext.Request, typeof(RenameProjectRequest), cancellationToken).ConfigureAwait(false) as RenameProjectRequest;

        if (!ProjectName.TryNormalise(request?.Name, out var name))
        {
            return InvalidName();
        }

        var renamed = await handler.HandleAsync(projectId, name, cancellationToken).ConfigureAwait(false);

        // Not recorded: an in-Space not-found crossed no boundary (AD-20). The body is the boundary's.
        return renamed is null
            ? BoundaryProblems.NotAvailable()
            : Results.Ok(Response(renamed.Project, renamed.TaskCount));
    }

    /// <summary>
    /// Deletes a Project of the active Space, with every Task in it.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>204</c> with no body, or the boundary's <c>404 not-available</c> - byte for byte, and not recorded - for an id
    /// that matches nothing in the active Space: never existed, already deleted, or another Space's (AD-3).
    /// </returns>
    public static async Task<IResult> DeleteAsync(Guid projectId, DeleteProjectHandler handler, CancellationToken cancellationToken)
    {
        var deletion = await handler.HandleAsync(projectId, cancellationToken).ConfigureAwait(false);

        // Not recorded: an in-Space not-found crossed no boundary (AD-20). The body is the boundary's.
        return deletion == ProjectDeletion.Deleted ? Results.NoContent() : BoundaryProblems.NotAvailable();
    }

    private static ProjectResponse Response(Project project, int taskCount) =>
        new(project.Id, project.Name, project.CreatedAt, taskCount);

    private static IResult InvalidName() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: ProblemTypes.InvalidProjectName,
            title: "A Project's name is 1 to 100 characters.");

    private static Task<object?> ReadAsync(HttpRequest request, Type type, CancellationToken cancellationToken) =>
        JsonRequestBody.ReadAsync(request, type, cancellationToken);
}

/// <summary>
/// Reads a Space-scoped endpoint's JSON body inside the endpoint, after the pipeline has run. Stories 2.1
/// and 2.2.
/// </summary>
internal static class JsonRequestBody
{
    /// <summary>
    /// Reads a body, or <c>null</c> for one that is not JSON, does not parse, or names a charset the
    /// runtime does not know (which <c>ReadFromJsonAsync</c> reports as an
    /// <see cref="InvalidOperationException"/>).
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="type">The contract to read.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The body, or <c>null</c>.</returns>
    internal static async Task<object?> ReadAsync(HttpRequest request, Type type, CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            return null;
        }

        try
        {
            return await request.ReadFromJsonAsync(type, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
