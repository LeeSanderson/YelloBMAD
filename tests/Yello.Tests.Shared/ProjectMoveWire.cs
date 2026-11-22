using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Yello.Tests.Shared;

/// <summary>
/// Moving one Task into another Project of its Space, as every suite sends and reads it: the body
/// <c>{ projectId, statusId? }</c>, and the <c>projectId</c> a Task answer states. The route and the problem type are the
/// contract's (<c>TaskRoutes.ProjectMove</c>, <c>ProblemTypes.InvalidTaskProject</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Bodies are anonymous objects or raw JSON</b>, never the contract type, so a case can send what the contract forbids
/// (a <c>positionKey</c>, a <c>spaceId</c>, a missing <c>projectId</c>). <see cref="Body(Guid)"/> leaves <c>statusId</c>
/// out; <see cref="Body(Guid, Guid?)"/> always writes it, <c>null</c> included, because both mean "no destination Status".
/// </para>
/// </remarks>
public static class ProjectMoveWire
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The body with no destination Status: the Task keeps its Status if the destination holds it.
    /// </summary>
    /// <param name="projectId">The destination Project.</param>
    /// <returns>The body.</returns>
    public static JsonContent Body(Guid projectId) => JsonContent.Create(new { projectId }, options: JsonOptions);

    /// <summary>
    /// The body with <c>statusId</c> always present, <c>null</c> meaning none was chosen.
    /// </summary>
    /// <param name="projectId">The destination Project.</param>
    /// <param name="statusId">The destination Status, or <c>null</c>.</param>
    /// <returns>The body.</returns>
    public static JsonContent Body(Guid projectId, Guid? statusId) => JsonContent.Create(new { projectId, statusId }, options: JsonOptions);

    /// <summary>
    /// A body of the case's own text.
    /// </summary>
    /// <param name="json">The JSON text, sent as given.</param>
    /// <returns>The body.</returns>
    public static StringContent RawBody(string json) => new(json, Encoding.UTF8, "application/json");

    /// <summary>
    /// The <c>projectId</c> a Task answer states, or <c>null</c> when the answer has none.
    /// </summary>
    /// <param name="taskBody">A <c>TaskResponse</c> body.</param>
    /// <returns>The Project id, or <c>null</c>.</returns>
    public static Guid? ProjectIdIn(string taskBody)
    {
        using var document = JsonDocument.Parse(taskBody);

        return document.RootElement.TryGetProperty("projectId", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetGuid()
            : null;
    }
}
