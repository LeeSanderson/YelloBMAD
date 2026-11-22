using System.Text.Json;

namespace Yello.Tests.Shared;

/// <summary>
/// Moving every Task of one Status of a Project into another Project, as every suite reads it: the answer
/// <c>{ projectId, statusId, movedCount }</c>. The route is <c>ProjectStatusRoutes.TasksToProject</c>. The body is the
/// single move's (<see cref="ProjectMoveWire.Body(Guid)"/>, <see cref="ProjectMoveWire.Body(Guid, Guid?)"/>,
/// <see cref="ProjectMoveWire.RawBody"/>): a destination Project and an optional destination Status.
/// </summary>
public static class StatusBulkMoveWire
{
    /// <summary>
    /// The <c>movedCount</c> an answer states, or <c>null</c> when it states none.
    /// </summary>
    /// <param name="body">A bulk move answer.</param>
    /// <returns>The count, or <c>null</c>.</returns>
    public static int? MovedCountIn(string body)
    {
        using var document = JsonDocument.Parse(body);

        return document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("movedCount", out var value)
            && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;
    }

    /// <summary>
    /// The <c>statusId</c> an answer states - the Status the moved Tasks hold in the destination - or <c>null</c>.
    /// </summary>
    /// <param name="body">A bulk move answer.</param>
    /// <returns>The Status id, or <c>null</c>.</returns>
    public static Guid? StatusIdIn(string body)
    {
        using var document = JsonDocument.Parse(body);

        return document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("statusId", out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetGuid()
            : null;
    }
}
