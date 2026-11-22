using System.Text.Json;
using Yello.Contracts;

namespace Yello.Host.Endpoints;

/// <summary>
/// Reads the body of a Task's move into another Project inside the endpoint, after the pipeline has run, and states the
/// move's own refusals.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read as a document, never bound</b>, so a <c>statusId</c> left out is told from one that is not an id, and a member the
/// request must not carry is seen at all.
/// </para>
/// <para>
/// <b>Every refusal is a <c>400</c> that echoes nothing sent</b>: a body that cannot be read, is not an object, leaves out
/// <c>projectId</c>, or names a position, a neighbour or a Space is <c>invalid-task-edit</c>; a <c>projectId</c> that is not
/// an id is <c>invalid-task-project</c>, the bytes a Project that is not the Space's gets; a <c>statusId</c> that is neither
/// an id nor <c>null</c> is <c>invalid-status-destination</c>, the bytes a Status outside the destination's set gets. Any
/// other member is ignored, as the edit ignores it.
/// </para>
/// </remarks>
internal static class TaskProjectMoveBody
{
    private const string ProjectMember = "projectId";

    private const string StatusMember = "statusId";

    private static readonly string[] ForbiddenMembers = ["positionKey", "afterTaskId", "spaceId"];

    private static readonly string[] BulkForbiddenMembers = [.. ForbiddenMembers, "taskIds"];

    /// <summary>
    /// Reads the move, or the refusal that answers it.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The destination Project and optional Status, or a refusal - exactly one of the two.</returns>
    internal static Task<(Guid ProjectId, Guid? StatusId, IResult? Refusal)> ReadAsync(HttpRequest request, CancellationToken cancellationToken) =>
        ReadAsync(request, ForbiddenMembers, cancellationToken);

    /// <summary>
    /// Reads the move of every Task of one Status, or the refusal that answers it: the single move's rules, and a list of
    /// Tasks is refused as a position is, since the Tasks moved are decided by the server.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The destination Project and optional Status, or a refusal - exactly one of the two.</returns>
    internal static Task<(Guid ProjectId, Guid? StatusId, IResult? Refusal)> ReadBulkAsync(HttpRequest request, CancellationToken cancellationToken) =>
        ReadAsync(request, BulkForbiddenMembers, cancellationToken);

    /// <summary>
    /// <c>400 invalid-task-project</c>: the destination is not another Project of the Space, whatever the cause.
    /// </summary>
    /// <returns>The refusal.</returns>
    internal static IResult InvalidProject() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: ProblemTypes.InvalidTaskProject,
            title: "A Task moves into another Project of its Space.");

    /// <summary>
    /// <c>400 invalid-status-destination</c>: the Status named is not one the destination holds.
    /// </summary>
    /// <returns>The refusal.</returns>
    internal static IResult InvalidStatus() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: ProblemTypes.InvalidStatusDestination,
            title: "A moved Task takes a Status its new Project holds.");

    /// <summary>
    /// <c>409 status-destination-required</c>: the destination lacks the Task's Status and the request named none.
    /// </summary>
    /// <returns>The refusal.</returns>
    internal static IResult StatusRequired() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: ProblemTypes.StatusDestinationRequired,
            title: "The new Project does not hold the Task's Status, so the move needs a Status for it.");

    private static async Task<(Guid ProjectId, Guid? StatusId, IResult? Refusal)> ReadAsync(HttpRequest request, string[] forbidden, CancellationToken cancellationToken)
    {
        using var document = await ParseAsync(request, cancellationToken).ConfigureAwait(false);

        return document?.RootElement is { ValueKind: JsonValueKind.Object } root
            ? Read(root, forbidden)
            : (Guid.Empty, null, InvalidMove());
    }

    private static (Guid ProjectId, Guid? StatusId, IResult? Refusal) Read(JsonElement root, string[] forbidden)
    {
        if (!root.TryGetProperty(ProjectMember, out var projectElement)
            || Array.Exists(forbidden, member => root.TryGetProperty(member, out _)))
        {
            return (Guid.Empty, null, InvalidMove());
        }

        if (projectElement.ValueKind != JsonValueKind.String || !projectElement.TryGetGuid(out var projectId))
        {
            return (Guid.Empty, null, InvalidProject());
        }

        if (!root.TryGetProperty(StatusMember, out var statusElement) || statusElement.ValueKind == JsonValueKind.Null)
        {
            return (projectId, null, null);
        }

        return statusElement.ValueKind == JsonValueKind.String && statusElement.TryGetGuid(out var statusId)
            ? (projectId, statusId, null)
            : (Guid.Empty, null, InvalidStatus());
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

    private static IResult InvalidMove() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: ProblemTypes.InvalidTaskEdit,
            title: "A move into another Project names the Project, and optionally a Status, and nothing else.");
}
