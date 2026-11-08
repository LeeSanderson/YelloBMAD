using System.Text.Json;
using Yello.Application.Spaces;
using Yello.Application.Tasks.AssignTask;
using Yello.Contracts;
using Yello.Contracts.Tasks;
using Yello.Domain.Memberships;
using Yello.Domain.Tasks;
using Yello.Host.Spaces;

namespace Yello.Host.Endpoints;

/// <summary>
/// Assign a Task of the active Space to one of its Memberships, or unassign it.
/// </summary>
/// <remarks>
/// <para>
/// <b>No Membership check, no Role, no bound count, no idempotency and no refusal record here</b> - the reasons
/// <see cref="ProjectEndpoints"/> gives. The pipeline has resolved the Space, checked <c>AssignTask</c> - the matrix's
/// own row, which no other route declares - and replayed a key it had already seen before this code runs. Both surfaces.
/// </para>
/// <para>
/// <b>The body is read as a document</b>, never bound: a bound record cannot tell a body that sets <c>membershipId</c> to
/// <c>null</c> (unassign) from one that does not name it (refused).
/// </para>
/// </remarks>
public static class TaskAssigneeEndpoints
{
    private const string MembershipMember = "membershipId";

    /// <summary>
    /// Registers <c>PUT /api/v1/spaces/{spaceId}/tasks/{taskId}/assignee</c>.
    /// </summary>
    /// <remarks>
    /// <b>No bound</b>: an assignment adds nothing.
    /// </remarks>
    /// <param name="endpoints">The route builder.</param>
    /// <returns>The registered route, for further configuration.</returns>
    public static RouteHandlerBuilder MapAssignTask(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPut(TaskRoutes.Assignee, AssignAsync)
            .RequiresCapability(Capability.AssignTask);

    /// <summary>
    /// Assigns one Task of the active Space, or unassigns it.
    /// </summary>
    /// <param name="taskId">The Task, from the route.</param>
    /// <param name="handler">The slice.</param>
    /// <param name="httpContext">The request: its services hold the resolved context, and its body is read here.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>
    /// <c>200</c> with the Task as stored; <c>400 invalid-assignee</c> - one body for every cause - for a body naming no
    /// Membership or a Membership that is not this Space's; or the boundary's <c>404 not-available</c>, byte for byte and not
    /// recorded, for a Task id that matches nothing in the active Space.
    /// </returns>
    public static async Task<IResult> AssignAsync(
        Guid taskId,
        AssignTaskHandler handler,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var (named, membershipId) = await ReadAsync(httpContext.Request, cancellationToken).ConfigureAwait(false);

        if (!named)
        {
            return InvalidAssignee();
        }

        var context = httpContext.RequestServices.GetRequiredService<ActiveSpaceContext>();

        var result = await handler.HandleAsync(context, taskId, membershipId, cancellationToken).ConfigureAwait(false);

        return result.Outcome switch
        {
            TaskAssignmentOutcome.Assigned => Results.Ok(TaskEndpoints.Response(result.Task!, result.LabelIds)),

            // Not recorded: a Membership that is not this Space's is a validation refusal, the same bytes for a random id and
            // a foreign one. The unit of work rolls back.
            TaskAssignmentOutcome.InvalidAssignee => InvalidAssignee(),

            // Not recorded: an in-Space not-found crossed no boundary. The body is the boundary's.
            TaskAssignmentOutcome.NotFound => BoundaryProblems.NotAvailable(),

            _ => throw new InvalidOperationException($"Unanswered assignment outcome {result.Outcome}."),
        };
    }

    private static IResult InvalidAssignee() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: ProblemTypes.InvalidAssignee,
            title: "That is not a Membership of this Space.");

    /// <summary>
    /// Whether the body is an object naming <c>membershipId</c>, and the id it names - <c>null</c> to unassign.
    /// </summary>
    private static async Task<(bool Named, Guid? MembershipId)> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var document = await ParseAsync(request, cancellationToken).ConfigureAwait(false);

        if (document?.RootElement is not { ValueKind: JsonValueKind.Object } root || !root.TryGetProperty(MembershipMember, out var member))
        {
            return (false, null);
        }

        return member.ValueKind switch
        {
            JsonValueKind.Null => (true, null),
            JsonValueKind.String when member.TryGetGuid(out var membershipId) => (true, membershipId),
            _ => (false, null),
        };
    }

    private static async Task<JsonDocument?> ParseAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (!request.HasJsonContentType())
        {
            return null;
        }

        try
        {
            return await JsonDocument.ParseAsync(request.Body, default, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
