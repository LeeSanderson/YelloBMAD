using Yello.Application.Spaces;
using Yello.Application.Statuses;
using Yello.Application.Statuses.AddSpaceStatus;
using Yello.Application.Statuses.PlaceSpaceStatus;
using Yello.Application.Statuses.RenameSpaceStatus;
using Yello.Contracts;
using Yello.Contracts.Spaces;
using Yello.Domain.Memberships;
using Yello.Domain.Statuses;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// Add, rename and place one of the Space's default Statuses, beside the Space's own Status read.
/// </summary>
/// <remarks>
/// <para>
/// <b>No Membership check, no Role and no idempotency here</b> - the reasons <see cref="ProjectEndpoints"/> gives. Every
/// write declares <c>ConfigureSpaceDefaultStatuses</c>, which only Owners and Admins hold; <b>no bound</b>, <b>both
/// surfaces</b>, and <b>bodies are read inside the endpoint</b>, never bound.
/// </para>
/// <para>
/// <b>A Status in the path that is not one of this Space's defaults answers the boundary's <c>404</c></b>, byte for byte,
/// unrecorded: another Space's default and a Project's own Status are not this Space's defaults.
/// </para>
/// </remarks>
public static class SpaceDefaultStatusEndpoints
{
    /// <summary>
    /// Registers <c>POST</c> on the Space's Statuses.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapAddSpaceStatus(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(SpaceRoutes.Statuses, AddAsync)
            .RequiresCapability(Capability.ConfigureSpaceDefaultStatuses);

    /// <summary>
    /// Registers <c>PATCH</c> on one of the Space's default Statuses.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapRenameSpaceStatus(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPatch(SpaceRoutes.Status, RenameAsync)
            .RequiresCapability(Capability.ConfigureSpaceDefaultStatuses);

    /// <summary>
    /// Registers <c>POST</c> on one of the Space's default Statuses' place action.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapPlaceSpaceStatus(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(SpaceRoutes.StatusPlace, PlaceAsync)
            .RequiresCapability(Capability.ConfigureSpaceDefaultStatuses);

    /// <summary>
    /// Adds a default Status.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request: its services hold the resolved context, and its body is read here.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>201</c> with the defaults and a <c>Location</c> on the collection; <c>400 invalid-status-name</c> or
    /// <c>invalid-status-position</c>; or <c>409 status-name-taken</c>.
    /// </returns>
    public static async Task<IResult> AddAsync(AddSpaceStatusHandler handler, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var request = await JsonRequestBody.ReadAsync(httpContext.Request, typeof(AddSpaceStatusRequest), cancellationToken).ConfigureAwait(false) as AddSpaceStatusRequest;

        if (!StatusName.TryNormalise(request?.Name, out var name))
        {
            return ProjectStatusAnswers.InvalidName();
        }

        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();
        var result = await handler.HandleAsync(context, name, request!.AfterStatusId, cancellationToken).ConfigureAwait(false);

        if (result.Defaults is not { } defaults)
        {
            return Refusal(result);
        }

        // Set on the response rather than through Results.Created, so the pipeline's idempotency record reads it from one
        // place whatever the endpoint.
        httpContext.Response.Headers.Location = SpaceRoutes.StatusesOf(context.SpaceId);

        return Results.Json(Responses(defaults), statusCode: StatusCodes.Status201Created);
    }

    /// <summary>
    /// Renames a default Status.
    /// </summary>
    /// <param name="statusId">The default, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request, whose body is read here.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>200</c> with the defaults; <c>400 invalid-status-name</c>; <c>409 status-name-taken</c> or
    /// <c>status-rename-conflict</c>; or the boundary's <c>404</c>.
    /// </returns>
    public static async Task<IResult> RenameAsync(Guid statusId, RenameSpaceStatusHandler handler, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var request = await JsonRequestBody.ReadAsync(httpContext.Request, typeof(RenameSpaceStatusRequest), cancellationToken).ConfigureAwait(false) as RenameSpaceStatusRequest;

        if (!StatusName.TryNormalise(request?.Name, out var name))
        {
            return ProjectStatusAnswers.InvalidName();
        }

        var result = await handler.HandleAsync(statusId, name, request!.ReplaceProjectNames, cancellationToken).ConfigureAwait(false);

        return result.Defaults is { } defaults ? Results.Ok(Responses(defaults)) : Refusal(result);
    }

    /// <summary>
    /// Places a default Status first or after another default.
    /// </summary>
    /// <param name="statusId">The default, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request, whose body is read here.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>200</c> with the defaults; <c>400 invalid-status-position</c>; or the boundary's <c>404</c>.</returns>
    public static async Task<IResult> PlaceAsync(Guid statusId, PlaceSpaceStatusHandler handler, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (await JsonRequestBody.ReadAsync(httpContext.Request, typeof(PlaceSpaceStatusRequest), cancellationToken).ConfigureAwait(false) is not PlaceSpaceStatusRequest request)
        {
            return ProjectStatusAnswers.Refusal(ProjectStatusWriteOutcome.PositionInvalid);
        }

        var result = await handler.HandleAsync(statusId, request.AfterStatusId, cancellationToken).ConfigureAwait(false);

        return result.Defaults is { } defaults ? Results.Ok(Responses(defaults)) : Refusal(result);
    }

    private static List<StatusResponse> Responses(IReadOnlyList<StatusDefinition> defaults) =>
        [.. defaults.Select(status => new StatusResponse(status.Id, status.Name, status.Position))];

    /// <summary>
    /// The answer to a change the slice refused. The two lists carry only Projects of the request's Space: they were read
    /// under both isolation layers.
    /// </summary>
    private static IResult Refusal(SpaceStatusWriteResult result) => result.Outcome switch
    {
        SpaceStatusWriteOutcome.RenameConflict => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: ProblemTypes.StatusRenameConflict,
            title: "Some Projects renamed this Status themselves.",
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["conflicts"] = result.Conflicts.Select(conflict => new StatusRenameConflictResponse(conflict.ProjectId, conflict.ProjectName, conflict.Name, conflict.TaskCount)).ToList(),
            }),

        SpaceStatusWriteOutcome.NameTaken when result.Projects.Count > 0 => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: ProblemTypes.StatusNameTaken,
            title: "Some Projects already have a Status of that name.",
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["projects"] = result.Projects.Select(project => new ProjectIdentityResponse(project.Id, project.Name)).ToList(),
            }),

        SpaceStatusWriteOutcome.NameTaken => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: ProblemTypes.StatusNameTaken,
            title: "The Space already has a default Status of that name."),

        SpaceStatusWriteOutcome.SetEmpty => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: ProblemTypes.StatusSetEmpty,
            title: "A Space keeps at least one default Status."),

        SpaceStatusWriteOutcome.PositionInvalid => ProjectStatusAnswers.Refusal(ProjectStatusWriteOutcome.PositionInvalid),

        // Not recorded: an in-Space not-found crossed no boundary. The body is the boundary's.
        _ => BoundaryProblems.NotAvailable(),
    };
}
