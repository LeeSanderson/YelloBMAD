using System.Text.Json;
using Yello.Contracts;

namespace Yello.Host.Endpoints;

/// <summary>
/// Reads a Task move's body (story 2.6, open question 2 (a)) inside the endpoint and after the pipeline has run, as
/// every Space-scoped body is read.
/// </summary>
/// <remarks>
/// <para>
/// <b>Read as a document, not bound to <c>MoveTaskRequest</c></b>, so a body that leaves <c>afterTaskId</c> out is
/// told apart from one that sets it to <c>null</c> (the top of the column), and a <c>positionKey</c> member is seen at
/// all.
/// </para>
/// <para>
/// <b>Every refusal is a <c>400</c>, never a <c>500</c></b>, and none echoes what was sent: a body that cannot be read,
/// leaves out either member, names an <c>afterTaskId</c> that is neither an id nor <c>null</c>, or carries a
/// <c>positionKey</c> is <c>invalid-task-edit</c>; a <c>statusId</c> that is not an id is <c>invalid-task-status</c>, the
/// same bytes as a Status that is not the Project's (I-13). Any other member is ignored, as the edit ignores it.
/// </para>
/// </remarks>
internal static class TaskMoveBody
{
    private const string StatusMember = "statusId";

    private const string AfterMember = "afterTaskId";

    private const string PositionKeyMember = "positionKey";

    /// <summary>
    /// Reads the move, or the refusal that answers it.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The destination Status and neighbour, or a refusal - exactly one of the two.</returns>
    internal static async Task<(Guid StatusId, Guid? AfterTaskId, IResult? Refusal)> ReadAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        using var document = await ParseAsync(request, cancellationToken).ConfigureAwait(false);

        return document?.RootElement is { ValueKind: JsonValueKind.Object } root
            ? Read(root)
            : (Guid.Empty, null, InvalidMove());
    }

    private static (Guid StatusId, Guid? AfterTaskId, IResult? Refusal) Read(JsonElement root)
    {
        if (root.TryGetProperty(PositionKeyMember, out _)
            || !root.TryGetProperty(StatusMember, out var statusElement)
            || !root.TryGetProperty(AfterMember, out var afterElement))
        {
            return (Guid.Empty, null, InvalidMove());
        }

        if (statusElement.ValueKind != JsonValueKind.String || !statusElement.TryGetGuid(out var statusId))
        {
            return (Guid.Empty, null, TaskEditBody.InvalidStatus());
        }

        if (afterElement.ValueKind == JsonValueKind.Null)
        {
            return (statusId, null, null);
        }

        return afterElement.ValueKind == JsonValueKind.String && afterElement.TryGetGuid(out var afterTaskId)
            ? (statusId, afterTaskId, null)
            : (Guid.Empty, null, InvalidMove());
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

    /// <summary>
    /// <c>409 task-position-unavailable</c>: the neighbour is not a Task of the destination column, or no key fits. One
    /// body for a random id and another Space's real id alike (I-13).
    /// </summary>
    /// <returns>The refusal.</returns>
    internal static IResult PositionUnavailable() =>
        Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            type: ProblemTypes.TaskPositionUnavailable,
            title: "That position is not available. Choose the position again.");

    private static IResult InvalidMove() =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            type: ProblemTypes.InvalidTaskEdit,
            title: "A move names a Status and the Task to follow, or null for the top of the column.");
}
