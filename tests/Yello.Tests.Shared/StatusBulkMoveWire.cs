using System.Text.Json;

namespace Yello.Tests.Shared;

/// <summary>
/// Moving every Task of one Status of a Project into another Project, as every suite sends and reads it: the route, which
/// names the source Project and Status in its path, and the answer <c>{ projectId, statusId, movedCount }</c>. The body is the
/// single move's (<see cref="ProjectMoveWire.Body(Guid)"/>, <see cref="ProjectMoveWire.Body(Guid, Guid?)"/>,
/// <see cref="ProjectMoveWire.RawBody"/>): a destination Project and an optional destination Status.
/// </summary>
/// <remarks>
/// <para>
/// <b>The route members are stand-ins</b> for <c>ProjectStatusRoutes.TasksToProject</c> and
/// <c>ProjectStatusRoutes.TasksToProjectOf</c>, which the contract does not declare yet. When it does, swap every use for the
/// contract member and delete these two.
/// </para>
/// </remarks>
public static class StatusBulkMoveWire
{
    /// <summary>
    /// The route template, as the Host maps it.
    /// </summary>
    public static string Route => "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/statuses/{statusId:guid}/tasks/project";

    /// <summary>
    /// The path that moves every Task of <paramref name="statusId"/> out of <paramref name="projectId"/>.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The source Project.</param>
    /// <param name="statusId">The source Status.</param>
    /// <returns>The path.</returns>
    public static string PathOf(Guid spaceId, Guid projectId, Guid statusId) =>
        $"/api/v1/spaces/{spaceId:D}/projects/{projectId:D}/statuses/{statusId:D}/tasks/project";

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
