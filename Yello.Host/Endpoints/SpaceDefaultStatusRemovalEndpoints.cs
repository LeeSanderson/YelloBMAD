using Yello.Application.Statuses;
using Yello.Application.Statuses.RemoveSpaceStatus;
using Yello.Contracts;
using Yello.Contracts.Spaces;
using Yello.Domain.Memberships;
using Yello.Domain.Statuses;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// Remove one of the Space's default Statuses from the Space and every Project of it.
/// </summary>
/// <remarks>
/// Apart from <see cref="SpaceDefaultStatusEndpoints"/> only for size; the same rules hold: <c>ConfigureSpaceDefaultStatuses</c>,
/// both surfaces, no bound, the body read inside the endpoint, and a Status in the path that is not one of this Space's
/// defaults answering the boundary's <c>404</c>, unrecorded.
/// </remarks>
public static class SpaceDefaultStatusRemovalEndpoints
{
    /// <summary>
    /// Registers <c>POST</c> on one of the Space's default Statuses' remove action.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapRemoveSpaceStatus(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(SpaceRoutes.StatusRemove, RemoveAsync)
            .RequiresCapability(Capability.ConfigureSpaceDefaultStatuses);

    /// <summary>
    /// Removes a default Status, moving every Task that holds it.
    /// </summary>
    /// <param name="statusId">The default, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request, whose body is read here.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>Every answer <see cref="SpaceRoutes.StatusRemove"/> lists.</returns>
    public static async Task<IResult> RemoveAsync(Guid statusId, RemoveSpaceStatusHandler handler, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (await JsonRequestBody.ReadAsync(httpContext.Request, typeof(RemoveSpaceStatusRequest), cancellationToken).ConfigureAwait(false) is not RemoveSpaceStatusRequest request
            || request.Exceptions?.Any(exception => exception is null) == true
            || request.Exceptions?.DistinctBy(exception => exception.ProjectId).Count() < request.Exceptions?.Count)
        {
            return DestinationInvalid([]);
        }

        var removal = new SpaceStatusRemovalRequest(
            statusId,
            request.DestinationStatusId,
            [.. (request.Exceptions ?? []).Select(exception => new SpaceStatusDestination(exception.ProjectId, exception.DestinationStatusId))]);
        var result = await handler.HandleAsync(removal, cancellationToken).ConfigureAwait(false);

        return result.Defaults is { } defaults
            ? Results.Ok(defaults.Select(status => new StatusResponse(status.Id, status.Name, status.Position)).ToList())
            : Refusal(result);
    }

    /// <summary>
    /// The answer to a removal the slice refused. Every Project named was read under both isolation layers, so it is one of
    /// the request's Space.
    /// </summary>
    private static IResult Refusal(SpaceStatusWriteResult result) => result.Outcome switch
    {
        SpaceStatusWriteOutcome.SetEmpty => Problem(
            StatusCodes.Status409Conflict,
            ProblemTypes.StatusSetEmpty,
            result.Projects.Count > 0 ? "Some Projects hold no other Status." : "A Space keeps at least one default Status.",
            Projects(result.Projects)),

        SpaceStatusWriteOutcome.DestinationInvalid => DestinationInvalid(result.Projects),

        SpaceStatusWriteOutcome.DestinationRequired => Problem(
            StatusCodes.Status409Conflict,
            ProblemTypes.StatusDestinationRequired,
            "Tasks hold this Status, so its removal needs a destination for them.",
            Removal(result.Removal)),

        SpaceStatusWriteOutcome.ProjectDestinationRequired => Problem(
            StatusCodes.Status409Conflict,
            ProblemTypes.StatusProjectDestinationRequired,
            "Some Projects cannot take the destination and need one of their own.",
            Removal(result.Removal)),

        // Not recorded: an in-Space not-found crossed no boundary. The body is the boundary's.
        _ => BoundaryProblems.NotAvailable(),
    };

    private static IResult DestinationInvalid(IReadOnlyList<SpaceStatusProject> projects) =>
        Problem(StatusCodes.Status400BadRequest, ProblemTypes.InvalidStatusDestination, "A removed Status's Tasks go to a Status the Project keeps.", Projects(projects));

    private static IResult Problem(int statusCode, string type, string title, KeyValuePair<string, object?>? extension) =>
        Results.Problem(
            statusCode: statusCode,
            type: type,
            title: title,
            extensions: extension is { } member ? new Dictionary<string, object?>(StringComparer.Ordinal) { [member.Key] = member.Value } : null);

    private static KeyValuePair<string, object?>? Projects(IReadOnlyList<SpaceStatusProject> named) =>
        named.Count == 0 ? null : new("projects", named.Select(project => new ProjectIdentityResponse(project.Id, project.Name)).ToList());

    private static KeyValuePair<string, object?> Removal(IReadOnlyList<SpaceStatusOccupancyReport> report) =>
        new(
            "removal",
            report.Select(entry => new SpaceStatusOccupancyResponse(
                entry.ProjectId,
                entry.ProjectName,
                entry.TaskCount,
                [.. entry.Destinations.Select(choice => new StatusChoiceResponse(choice.Id, choice.Name))])).ToList());
}
