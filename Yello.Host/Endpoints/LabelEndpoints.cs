using Yello.Application.Labels.DefineLabel;
using Yello.Application.Labels.DeleteLabel;
using Yello.Application.Labels.ListLabels;
using Yello.Application.Spaces;
using Yello.Contracts;
using Yello.Contracts.Labels;
using Yello.Domain.Labels;
using Yello.Domain.Memberships;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// List, define and delete the active Space's Labels. FR-22; story 2.4, AC1, AC3, AC9.
/// </summary>
/// <remarks>
/// <para>
/// <b>No Membership check, no Role, no bound count and no idempotency here, and there must never be any</b> - the
/// reasons <see cref="ProjectEndpoints"/> gives (AR-3). Each route carries <c>{spaceId}</c>, so the pipeline has
/// resolved the Space, checked the declared capability and replayed a key it had already seen before this code runs.
/// </para>
/// <para>
/// <b>Reading takes the capability every Role holds</b> (a Viewer sees chips); <b>defining and deleting take
/// <c>ManageLabels</c></b>, which only Owners and Admins hold. Applying a Label is the Task edit's, under
/// <c>ManageTasks</c>. <b>No bound</b> (open question 6).
/// </para>
/// <para>
/// <b>Both surfaces</b> (AD-4): none is Session-only. <b>Bodies are read inside the endpoint</b>, never bound.
/// </para>
/// </remarks>
public static class LabelEndpoints
{
    /// <summary>
    /// Registers <c>GET /api/v1/spaces/{spaceId}/labels</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapListLabels(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet(LabelRoutes.List, ListAsync)
            .RequiresCapability(Capability.ReadProjectsAndTasks);

    /// <summary>
    /// Registers <c>POST /api/v1/spaces/{spaceId}/labels</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapDefineLabel(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPost(LabelRoutes.Create, DefineAsync)
            .RequiresCapability(Capability.ManageLabels);

    /// <summary>
    /// Registers <c>DELETE /api/v1/spaces/{spaceId}/labels/{labelId}</c>.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapDeleteLabel(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapDelete(LabelRoutes.Label, DeleteAsync)
            .RequiresCapability(Capability.ManageLabels);

    /// <summary>
    /// Lists the active Space's Labels.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns><c>200</c> with the Labels, by name then id.</returns>
    public static async Task<IResult> ListAsync(ListLabelsHandler handler, CancellationToken cancellationToken)
    {
        var labels = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(labels.Select(Response).ToList());
    }

    /// <summary>
    /// Defines a Label in the active Space.
    /// </summary>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">
    /// The request: its services hold the context the pipeline resolved, and its body is read here.
    /// </param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>201</c> with the Label and its <c>Location</c>; <c>400 invalid-label-name</c> or
    /// <c>400 invalid-label-colour</c>; or <c>409 label-name-taken</c>. None echoes the name sent.
    /// </returns>
    public static async Task<IResult> DefineAsync(
        DefineLabelHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var request = await JsonRequestBody.ReadAsync(httpContext.Request, typeof(CreateLabelRequest), cancellationToken).ConfigureAwait(false) as CreateLabelRequest;

        if (!LabelName.TryNormalise(request?.Name, out var name))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: ProblemTypes.InvalidLabelName,
                title: "A Label's name is 1 to 50 characters.");
        }

        if (!LabelPalette.IsKnown(request?.Colour))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                type: ProblemTypes.InvalidLabelColour,
                title: "A Label's colour is one of the palette's.");
        }

        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();

        var result = await handler.HandleAsync(context, name, request!.Colour!, cancellationToken).ConfigureAwait(false);

        if (result.Label is not { } label)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: ProblemTypes.LabelNameTaken,
                title: "This Space already has a Label of that name.");
        }

        // Set on the response rather than through Results.Created, so the pipeline's idempotency record reads it
        // from one place whatever the endpoint (ARCH-009 holds the rule).
        httpContext.Response.Headers.Location = LabelRoutes.LabelOf(label.SpaceId, label.Id);

        return Results.Json(Response(label), statusCode: StatusCodes.Status201Created);
    }

    /// <summary>
    /// Deletes a Label of the active Space, unless a Task carries it.
    /// </summary>
    /// <param name="labelId">The Label, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>204</c>; <c>409 label-in-use</c>, with no count and no Task in it; or the boundary's
    /// <c>404 not-available</c> - byte for byte, and not recorded - for an id that matches nothing in the active
    /// Space, another Space's Label included.
    /// </returns>
    public static async Task<IResult> DeleteAsync(Guid labelId, DeleteLabelHandler handler, CancellationToken cancellationToken)
    {
        var deletion = await handler.HandleAsync(labelId, cancellationToken).ConfigureAwait(false);

        return deletion switch
        {
            LabelDeletion.Deleted => Results.NoContent(),

            LabelDeletion.InUse => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                type: ProblemTypes.LabelInUse,
                title: "This Label is on Tasks. Remove it from them first."),

            // Not recorded: an in-Space not-found crossed no boundary (AD-20). The body is the boundary's.
            _ => BoundaryProblems.NotAvailable(),
        };
    }

    private static LabelResponse Response(Label label) => new(label.Id, label.Name, label.Colour);
}
