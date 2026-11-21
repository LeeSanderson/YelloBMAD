using System.Net.Http.Json;
using System.Text.Json;

namespace Yello.Tests.Shared;

/// <summary>
/// A Project's Status delta over the wire: the read of its effective set (<c>GET …/projects/{projectId}/statuses</c>), and
/// the three writes - add (<c>POST</c> on the collection), rename (<c>PATCH</c> on one Status) and place
/// (<c>POST …/statuses/{statusId}/place</c> with <c>{ afterStatusId }</c>, <c>null</c> meaning first). Every answer of the
/// four is the Project's whole effective set, one <c>{ defaultName, id, name, source, taskCount }</c> per Status, in
/// effective order.
/// </summary>
/// <remarks>
/// <para>
/// <b>The routes, the problem types and the source codes stand in for contract members that do not exist yet.</b> Every
/// use is swapped for the <c>Yello.Contracts</c> member when the routes, <c>ProjectStatusResponse</c> and the four problem
/// types are added, and the stand-ins here are then deleted, so each is stated once. They follow the story's
/// recommendations: the four routes of open question 12 (a), the response of open question 5 (a), and the problem types
/// its Task 4 names. A different shape in the built contract is corrected here, and recorded, never weakened.
/// </para>
/// <para>
/// <b>An add answers <c>201</c></b> with the whole set and a <c>Location</c> on the collection, as the Label definition does;
/// rename and place answer <c>200</c>. An add may carry <c>afterStatusId</c> to add at a position (the delta's "Add, then
/// Place, in one transaction").
/// </para>
/// </remarks>
public static class ProjectStatusWire
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The collection's pattern: the read and the add.
    /// </summary>
    public static string Collection => "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/statuses";

    /// <summary>
    /// One Status's pattern under a Project: the rename.
    /// </summary>
    public static string Item => "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/statuses/{statusId:guid}";

    /// <summary>
    /// The reorder action's pattern.
    /// </summary>
    public static string Place => "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/statuses/{statusId:guid}/place";

    /// <summary>
    /// A name that is blank after trimming or longer than 100 characters.
    /// </summary>
    public static string InvalidStatusNameType => "urn:yello:problem:invalid-status-name";

    /// <summary>
    /// A name the Project's effective set already holds, compared case-insensitively.
    /// </summary>
    public static string StatusNameTakenType => "urn:yello:problem:status-name-taken";

    /// <summary>
    /// An operation whose result would be an empty effective set.
    /// </summary>
    public static string StatusSetEmptyType => "urn:yello:problem:status-set-empty";

    /// <summary>
    /// A place anchor outside the Project's effective set.
    /// </summary>
    public static string InvalidStatusPositionType => "urn:yello:problem:invalid-status-position";

    /// <summary>
    /// The source of a Space default, renamed here or not.
    /// </summary>
    public static string SpaceDefault => "space-default";

    /// <summary>
    /// The source of a Status this Project added.
    /// </summary>
    public static string AddedHere => "added-here";

    /// <summary>
    /// The capability every write declares, as its stable code.
    /// </summary>
    public static string ConfigureCapability => "configure-project-status-delta";

    /// <summary>
    /// The members of one entry, in ordinal order.
    /// </summary>
    public static IReadOnlyList<string> EntryMembers { get; } = ["defaultName", "id", "name", "source", "taskCount"];

    /// <summary>
    /// The collection's path.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <returns>The path.</returns>
    public static string CollectionOf(Guid spaceId, Guid projectId) => $"/api/v1/spaces/{spaceId:D}/projects/{projectId:D}/statuses";

    /// <summary>
    /// One Status's path under a Project.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <param name="statusId">The Status.</param>
    /// <returns>The path.</returns>
    public static string ItemOf(Guid spaceId, Guid projectId, Guid statusId) => $"{CollectionOf(spaceId, projectId)}/{statusId:D}";

    /// <summary>
    /// The reorder action's path.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <param name="statusId">The Status being placed.</param>
    /// <returns>The path.</returns>
    public static string PlaceOf(Guid spaceId, Guid projectId, Guid statusId) => $"{ItemOf(spaceId, projectId, statusId)}/place";

    /// <summary>
    /// The add and rename body.
    /// </summary>
    /// <param name="name">The name, sent as given.</param>
    /// <returns>The body.</returns>
    public static JsonContent NameBody(string? name) => JsonContent.Create(new { name }, options: JsonOptions);

    /// <summary>
    /// The add-at-a-position body.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="afterStatusId">The Status it goes after.</param>
    /// <returns>The body.</returns>
    public static JsonContent NameAfterBody(string name, Guid afterStatusId) => JsonContent.Create(new { name, afterStatusId }, options: JsonOptions);

    /// <summary>
    /// The place body: <c>afterStatusId</c> is always present, <c>null</c> meaning first.
    /// </summary>
    /// <param name="afterStatusId">The anchor, or <c>null</c> for first.</param>
    /// <returns>The body.</returns>
    public static JsonContent PlaceBody(Guid? afterStatusId) => JsonContent.Create(new { afterStatusId }, options: JsonOptions);

    /// <summary>
    /// The entries of an answer, in the order served.
    /// </summary>
    /// <param name="body">A read's or a write's body.</param>
    /// <returns>The entries.</returns>
    public static IReadOnlyList<ProjectStatusEntry> EntriesOf(string body)
    {
        using var document = JsonDocument.Parse(body);

        return
        [
            .. document.RootElement.EnumerateArray().Select(entry => new ProjectStatusEntry(
                entry.GetProperty("id").GetGuid(),
                entry.GetProperty("name").GetString() ?? string.Empty,
                entry.GetProperty("source").GetString() ?? string.Empty,
                entry.TryGetProperty("defaultName", out var defaultName) && defaultName.ValueKind == JsonValueKind.String ? defaultName.GetString() : null,
                entry.TryGetProperty("taskCount", out var count) ? count.GetInt32() : -1,
                [.. entry.EnumerateObject().Select(member => member.Name).Order(StringComparer.Ordinal)])),
        ];
    }

    /// <summary>
    /// The problem type of a refusal body, or <c>null</c>.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns>The type.</returns>
    public static string? ProblemTypeOf(string body)
    {
        using var document = JsonDocument.Parse(body);

        return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("type", out var type)
            ? type.GetString()
            : null;
    }
}

/// <summary>
/// One entry of a Project's effective set as the read serves it.
/// </summary>
/// <param name="Id">The Status's id, the same at both levels.</param>
/// <param name="Name">Its effective name in this Project.</param>
/// <param name="Source"><see cref="ProjectStatusWire.SpaceDefault"/> or <see cref="ProjectStatusWire.AddedHere"/>.</param>
/// <param name="DefaultName">The Space default's own name when this Project renamed it; otherwise <c>null</c>.</param>
/// <param name="TaskCount">How many of the Project's Tasks hold it; <c>-1</c> when the member is absent.</param>
/// <param name="Members">The entry's member names, in ordinal order.</param>
public sealed record ProjectStatusEntry(Guid Id, string Name, string Source, string? DefaultName, int TaskCount, IReadOnlyList<string> Members);
