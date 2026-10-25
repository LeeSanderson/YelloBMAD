using Yello.Application.Spaces;
using Yello.Application.Tasks.CreateTask;
using Yello.Application.Tasks.EditTaskAttributes;
using Yello.Application.Tasks.ViewTask;
using Yello.Contracts;
using Yello.Contracts.Tasks;
using Yello.Domain.Bounds;
using Yello.Domain.Memberships;
using Yello.Domain.Tasks;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// Create a Task in a Project of the active Space, and read and edit one Task. FR-19, FR-20; stories 2.2
/// and 2.3.
/// </summary>
/// <remarks>
/// <para>
/// <b>No Membership check, no Role, no bound count and no idempotency here, and there must never be
/// any</b> - the reasons <see cref="ProjectEndpoints"/> gives (AR-3). The route carries <c>{spaceId}</c> and
/// <c>{projectId}</c>, so the pipeline has resolved the Space, checked <c>ManageTasks</c>, replayed a key it
/// had already seen and counted the Project's Tasks before this code runs.
/// </para>
/// <para>
/// <b>Both surfaces</b> (AD-4): not Session-only. <b>The body is read inside the endpoint</b>, never bound.
/// </para>
/// </remarks>
public static class TaskEndpoints
{
    /// <summary>
    /// Registers <c>POST /api/v1/spaces/{spaceId}/projects/{projectId}/tasks</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapCreateTask(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(TaskRoutes.Create, CreateAsync)
            .RequiresCapability(Capability.ManageTasks)
            .ConsultsBound(Bound.TasksPerProject);

    /// <summary>
    /// Creates a Task in a Project of the active Space.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">
    /// The request: its services hold the context the pipeline resolved, and its body is read here.
    /// </param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>201</c> with the Task and its <c>Location</c>; <c>400 invalid-task-title</c>; or the boundary's
    /// <c>404 not-available</c> - byte for byte, and not recorded - for a Project id that matches nothing in
    /// the active Space, another Space's Project included.
    /// </returns>
    public static async Task<IResult> CreateAsync(
        Guid projectId,
        CreateTaskHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var request = await JsonRequestBody.ReadAsync(httpContext.Request, typeof(CreateTaskRequest), cancellationToken).ConfigureAwait(false) as CreateTaskRequest;

        if (!TaskTitle.TryNormalise(request?.Title, out var title))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: ProblemTypes.InvalidTaskTitle,
                title: "A Task's title is 1 to 200 characters.");
        }

        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();

        var task = await handler.HandleAsync(context, projectId, title, cancellationToken).ConfigureAwait(false);

        if (task is null)
        {
            // Not recorded: an in-Space not-found crossed no boundary (AD-20). The body is the boundary's.
            return BoundaryProblems.NotAvailable();
        }

        // Set on the response rather than through Results.Created, so the pipeline's idempotency record
        // reads it from one place whatever the endpoint (ARCH-009 holds the rule).
        httpContext.Response.Headers.Location = TaskRoutes.TaskOf(task.SpaceId, task.Id);

        return Results.Json(Response(task), statusCode: StatusCodes.Status201Created);
    }

    /// <summary>
    /// Registers <c>GET /api/v1/spaces/{spaceId}/tasks/{taskId}</c>. Story 2.3.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapViewTask(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(TaskRoutes.Task, ViewAsync)
            .RequiresCapability(Capability.ReadProjectsAndTasks);

    /// <summary>
    /// Registers <c>PATCH /api/v1/spaces/{spaceId}/tasks/{taskId}</c>. Story 2.3.
    /// </summary>
    /// <remarks>
    /// <b>No bound</b>: an edit adds no Task, so consulting Tasks-per-Project would refuse edits in a full
    /// Project.
    /// </remarks>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapEditTask(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPatch(TaskRoutes.Task, EditAsync)
            .RequiresCapability(Capability.ManageTasks);

    /// <summary>
    /// Reads one Task of the active Space.
    /// </summary>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>200</c> with the Task, or the boundary's <c>404 not-available</c> - byte for byte, and not
    /// recorded - for an id that matches nothing in the active Space, another Space's Task included.
    /// </returns>
    public static async Task<IResult> ViewAsync(Guid taskId, ViewTaskHandler handler, CancellationToken cancellationToken)
    {
        var task = await handler.HandleAsync(taskId, cancellationToken).ConfigureAwait(false);

        return task is null ? BoundaryProblems.NotAvailable() : Results.Ok(Response(task));
    }

    /// <summary>
    /// Edits one Task of the active Space: any of its title, Status and due date, read as a JSON merge patch
    /// (story 2.3, open question 1 (a)).
    /// </summary>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request, whose body is read here.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>200</c> with the Task as stored; <c>400</c> with <c>invalid-task-edit</c>, <c>invalid-task-title</c>,
    /// <c>invalid-task-status</c> or <c>invalid-due-date</c>; or the boundary's <c>404 not-available</c> - byte
    /// for byte, and not recorded - for an id that matches nothing in the active Space.
    /// </returns>
    public static async Task<IResult> EditAsync(
        Guid taskId,
        EditTaskAttributesHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var read = await TaskEditBody.ReadAsync(httpContext.Request, cancellationToken).ConfigureAwait(false);

        if (read.Refusal is { } refusal)
        {
            return refusal;
        }

        var result = await handler.HandleAsync(taskId, read.Edit!, cancellationToken).ConfigureAwait(false);

        return result.Outcome switch
        {
            EditTaskAttributesOutcome.Edited => Results.Ok(Response(result.Task!)),

            EditTaskAttributesOutcome.StatusOutsideTheEffectiveSet => TaskEditBody.InvalidStatus(),

            // Not recorded: an in-Space not-found crossed no boundary (AD-20). The body is the boundary's.
            _ => BoundaryProblems.NotAvailable(),
        };
    }

    /// <summary>
    /// A Task as the wire carries it.
    /// </summary>
    /// <param name="task">The Task.</param>
    /// <returns>Its response.</returns>
    internal static TaskResponse Response(TaskItem task) =>
        new(task.Id, task.Title, task.StatusId, task.PositionKey, task.CreatedAt, task.DueDate);
}
