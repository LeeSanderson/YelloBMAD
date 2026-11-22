using System.Net.Http.Json;
using System.Text.Json;
using Yello.Contracts.Spaces;

namespace Yello.Tests.Shared;

/// <summary>
/// The Space's own default Statuses as the three Space-level writes take and answer them: add on the collection, rename on
/// one Status, place on its <c>place</c> action. Every answer is the Space's whole default list as <c>{ id, name, position }</c>
/// rows in Space order - the shape the existing read already serves.
/// </summary>
/// <remarks>
/// <para>
/// <b>Routes, problem types and the problem body's members are the contract's</b> (<see cref="SpaceRoutes"/>,
/// <see cref="Yello.Contracts.ProblemTypes"/>): the bodies here are written as JSON so a case can leave a member out, and the
/// lists are read from the raw body so a member the contract does not declare is not silently dropped.
/// </para>
/// <para>
/// <b>A rename with no decision leaves the member out</b> rather than sending <c>null</c>: the decision is absent until the
/// Admin has seen a conflict.
/// </para>
/// </remarks>
public static class SpaceStatusWire
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);


    /// <summary>
    /// The add body with no position: the new default goes last.
    /// </summary>
    /// <param name="name">The name, sent as given.</param>
    /// <returns>The body.</returns>
    public static JsonContent AddBody(string? name) => AddBody(name, afterStatusId: null);

    /// <summary>
    /// The add body, with <c>afterStatusId</c> only when the add names a position.
    /// </summary>
    /// <param name="name">The name, sent as given.</param>
    /// <param name="afterStatusId">The default it goes after, or <c>null</c> for last.</param>
    /// <returns>The body.</returns>
    public static JsonContent AddBody(string? name, Guid? afterStatusId) =>
        afterStatusId is { } after
            ? JsonContent.Create(new { name, afterStatusId = after }, options: JsonOptions)
            : JsonContent.Create(new { name }, options: JsonOptions);

    /// <summary>
    /// The rename body with no decision.
    /// </summary>
    /// <param name="name">The new name, sent as given.</param>
    /// <returns>The body.</returns>
    public static JsonContent RenameBody(string? name) => RenameBody(name, replaceProjectNames: null);

    /// <summary>
    /// The rename body, with <c>replaceProjectNames</c> only when the request carries a decision.
    /// </summary>
    /// <param name="name">The new name, sent as given.</param>
    /// <param name="replaceProjectNames">The one decision for every conflicting Project, or <c>null</c> for none.</param>
    /// <returns>The body.</returns>
    public static JsonContent RenameBody(string? name, bool? replaceProjectNames) =>
        replaceProjectNames is { } replace
            ? JsonContent.Create(new { name, replaceProjectNames = replace }, options: JsonOptions)
            : JsonContent.Create(new { name }, options: JsonOptions);

    /// <summary>
    /// The place body: <c>afterStatusId</c> is always present, <c>null</c> meaning first.
    /// </summary>
    /// <param name="afterStatusId">The anchor, or <c>null</c> for first.</param>
    /// <returns>The body.</returns>
    public static JsonContent PlaceBody(Guid? afterStatusId) => JsonContent.Create(new { afterStatusId }, options: JsonOptions);

    /// <summary>
    /// The default rows of an answer, in the order served.
    /// </summary>
    /// <param name="body">The read's or a write's body.</param>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<SpaceStatusRow> DefaultsOf(string body)
    {
        using var document = JsonDocument.Parse(body);

        return
        [
            .. document.RootElement.EnumerateArray().Select(row => new SpaceStatusRow(
                row.GetProperty("id").GetGuid(),
                row.GetProperty("name").GetString() ?? string.Empty,
                row.GetProperty("position").GetInt32())),
        ];
    }

    /// <summary>
    /// The conflict report of a refusal: every Project that gave the Status its own name, or empty when the body has none.
    /// </summary>
    /// <param name="body">The refusal's body.</param>
    /// <returns>The conflicts, in the order served.</returns>
    public static IReadOnlyList<SpaceStatusConflict> ConflictsOf(string body)
    {
        using var document = JsonDocument.Parse(body);

        if (!document.RootElement.TryGetProperty("conflicts", out var conflicts) || conflicts.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return
        [
            .. conflicts.EnumerateArray().Select(conflict => new SpaceStatusConflict(
                conflict.GetProperty("projectId").GetGuid(),
                conflict.GetProperty("projectName").GetString() ?? string.Empty,
                conflict.GetProperty("name").GetString() ?? string.Empty,
                conflict.GetProperty("taskCount").GetInt32())),
        ];
    }

    /// <summary>
    /// The Projects a name-clash refusal names, as (id, name), or empty when the body names none.
    /// </summary>
    /// <param name="body">The refusal's body.</param>
    /// <returns>The Projects, in the order served.</returns>
    public static IReadOnlyList<(Guid Id, string Name)> ProjectsOf(string body)
    {
        using var document = JsonDocument.Parse(body);

        if (!document.RootElement.TryGetProperty("projects", out var projects) || projects.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. projects.EnumerateArray().Select(project => (project.GetProperty("id").GetGuid(), project.GetProperty("name").GetString() ?? string.Empty))];
    }

    /// <summary>
    /// The name of the Space-level Status lock as the product spells it: <c>Yello/StatusDefaults/{spaceId:D}</c>, lower case, so
    /// a holder and the product meet on the same resource.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The resource name.</returns>
    public static string StatusDefaultsLockOf(Guid spaceId) => $"Yello/StatusDefaults/{spaceId:D}";


    /// <summary>
    /// The removal body with no exceptions: <c>destinationStatusId</c> is always present, <c>null</c> meaning none was chosen.
    /// </summary>
    /// <param name="destinationStatusId">The Space-wide destination, or <c>null</c>.</param>
    /// <returns>The body.</returns>
    public static JsonContent RemoveBody(Guid? destinationStatusId) => RemoveBody(destinationStatusId, exceptions: null);

    /// <summary>
    /// The removal body, with <c>exceptions</c> - each <c>{ projectId, destinationStatusId }</c> - only when the request
    /// carries some.
    /// </summary>
    /// <param name="destinationStatusId">The Space-wide destination, or <c>null</c>.</param>
    /// <param name="exceptions">A destination of its own for each named Project, or <c>null</c> for none.</param>
    /// <returns>The body.</returns>
    public static JsonContent RemoveBody(Guid? destinationStatusId, IReadOnlyList<(Guid ProjectId, Guid DestinationStatusId)>? exceptions) =>
        exceptions is null
            ? JsonContent.Create(new { destinationStatusId }, options: JsonOptions)
            : JsonContent.Create(
                new { destinationStatusId, exceptions = exceptions.Select(exception => new { projectId = exception.ProjectId, destinationStatusId = exception.DestinationStatusId }) },
                options: JsonOptions);

    /// <summary>
    /// The occupancy report of a removal's refusal: every Project of the Space holding the Status with at least one Task in
    /// it, with its count and its own set after the removal, or empty when the body carries none.
    /// </summary>
    /// <param name="body">The refusal's body.</param>
    /// <returns>The report, in the order served.</returns>
    public static IReadOnlyList<SpaceStatusOccupancy> RemovalOf(string body)
    {
        using var document = JsonDocument.Parse(body);

        if (!document.RootElement.TryGetProperty("removal", out var removal) || removal.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return
        [
            .. removal.EnumerateArray().Select(entry => new SpaceStatusOccupancy(
                entry.GetProperty("projectId").GetGuid(),
                entry.GetProperty("projectName").GetString() ?? string.Empty,
                entry.GetProperty("taskCount").GetInt32(),
                [.. entry.GetProperty("destinations").EnumerateArray().Select(choice => new SpaceStatusChoice(choice.GetProperty("id").GetGuid(), choice.GetProperty("name").GetString() ?? string.Empty))])),
        ];
    }
}

/// <summary>
/// One Space default as the Space's Status read and writes serve it.
/// </summary>
/// <param name="Id">The Status's id, the same at both levels.</param>
/// <param name="Name">Its Space name.</param>
/// <param name="Position">Its place in Space order.</param>
public sealed record SpaceStatusRow(Guid Id, string Name, int Position);

/// <summary>
/// One Project in a rename's conflict report.
/// </summary>
/// <param name="ProjectId">The Project.</param>
/// <param name="ProjectName">Its name.</param>
/// <param name="Name">Its own name for the Status.</param>
/// <param name="TaskCount">How many of its Tasks hold the Status.</param>
public sealed record SpaceStatusConflict(Guid ProjectId, string ProjectName, string Name, int TaskCount);

/// <summary>
/// One Project in a Space-level removal's occupancy report. <see cref="Destinations"/> is a list, which a record compares by
/// reference: compare a projection.
/// </summary>
/// <param name="ProjectId">The Project.</param>
/// <param name="ProjectName">Its name.</param>
/// <param name="TaskCount">How many of its Tasks hold the Status being removed.</param>
/// <param name="Destinations">Its own set after the removal, in Board order: what its picker offers.</param>
public sealed record SpaceStatusOccupancy(Guid ProjectId, string ProjectName, int TaskCount, IReadOnlyList<SpaceStatusChoice> Destinations);

/// <summary>
/// One Status a Project's destination picker offers.
/// </summary>
/// <param name="Id">The Status.</param>
/// <param name="Name">The Project's own name for it.</param>
public sealed record SpaceStatusChoice(Guid Id, string Name);
