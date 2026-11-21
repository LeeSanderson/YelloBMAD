using System.Net.Http.Json;
using System.Text.Json;
using Yello.Contracts.Projects;

namespace Yello.Tests.Shared;

/// <summary>
/// The bodies a Project's Status delta takes and the reading of its answers. The routes, problem types and sources are the
/// contract's own (<see cref="ProjectStatusRoutes"/>, <c>ProblemTypes</c>, <see cref="ProjectStatusSources"/>). Every answer
/// of the four routes is the Project's whole effective set, one <c>{ defaultName, id, name, source, taskCount }</c> per
/// Status, in effective order.
/// </summary>
/// <remarks>
/// <para>
/// <b>An add answers <c>201</c></b> with the whole set and a <c>Location</c> on the collection, as the Label definition does;
/// rename and place answer <c>200</c>. An add may carry <c>afterStatusId</c> to add at a position (the delta's "Add, then
/// Place, in one transaction").
/// </para>
/// <para>
/// <b>The removal and add-back members are stand-ins</b> (<see cref="Remove"/>, <see cref="Restore"/>, the two problem
/// types) for routes and types the contract does not state yet. The change that adds them swaps every use for the contract
/// member and deletes the stand-in.
/// </para>
/// </remarks>
public static class ProjectStatusWire
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The members of one entry, in ordinal order.
    /// </summary>
    public static IReadOnlyList<string> EntryMembers { get; } = ["defaultName", "id", "name", "source", "taskCount"];

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
    /// Stand-in for the removal route, <c>POST …/statuses/{statusId}/remove</c>, until <c>ProjectStatusRoutes</c> states it.
    /// </summary>
    public static string Remove => ProjectStatusRoutes.Item + "/remove";

    /// <summary>
    /// Stand-in for the add-back route of a removed Space default, <c>POST …/statuses/{statusId}/restore</c>, no body.
    /// </summary>
    public static string Restore => ProjectStatusRoutes.Item + "/restore";

    /// <summary>
    /// Stand-in for <c>ProblemTypes.StatusDestinationRequired</c>: an occupied Status removed with no destination (<c>409</c>).
    /// </summary>
    public static string StatusDestinationRequired => "urn:yello:problem:status-destination-required";

    /// <summary>
    /// Stand-in for <c>ProblemTypes.InvalidStatusDestination</c>: a destination outside the set the removal leaves (<c>400</c>).
    /// </summary>
    public static string InvalidStatusDestination => "urn:yello:problem:invalid-status-destination";

    /// <summary>
    /// The removal path of one Status of a Project.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <param name="statusId">The Status to remove.</param>
    /// <returns>The path.</returns>
    public static string RemoveOf(Guid spaceId, Guid projectId, Guid statusId) => $"{ProjectStatusRoutes.ItemOf(spaceId, projectId, statusId)}/remove";

    /// <summary>
    /// The add-back path of one removed Space default.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <param name="statusId">The removed default.</param>
    /// <returns>The path.</returns>
    public static string RestoreOf(Guid spaceId, Guid projectId, Guid statusId) => $"{ProjectStatusRoutes.ItemOf(spaceId, projectId, statusId)}/restore";

    /// <summary>
    /// The removal body: <c>destinationStatusId</c> is always present, <c>null</c> meaning no destination.
    /// </summary>
    /// <param name="destinationStatusId">Where the occupying Tasks go, or <c>null</c>.</param>
    /// <returns>The body.</returns>
    public static JsonContent RemoveBody(Guid? destinationStatusId) => JsonContent.Create(new { destinationStatusId }, options: JsonOptions);

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
/// <param name="Source"><see cref="ProjectStatusSources.SpaceDefault"/> or <see cref="ProjectStatusSources.AddedHere"/>.</param>
/// <param name="DefaultName">The Space default's own name when this Project renamed it; otherwise <c>null</c>.</param>
/// <param name="TaskCount">How many of the Project's Tasks hold it; <c>-1</c> when the member is absent.</param>
/// <param name="Members">The entry's member names, in ordinal order.</param>
public sealed record ProjectStatusEntry(Guid Id, string Name, string Source, string? DefaultName, int TaskCount, IReadOnlyList<string> Members);
