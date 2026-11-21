using System.Text.Json;
using Yello.Contracts.Projects;

namespace Yello.Tests.Client;

/// <summary>
/// A Project's Status delta on the fake: the read of its effective set and the add, rename and place writes, answered as the
/// Host answers them - every answer the whole set, one <c>{ defaultName, id, name, source, taskCount }</c> per Status in
/// effective order - and the Project's Board following the set, so a browser case sees the Board's columns, the pager's
/// tabs and the Task detail picker take the Project's order with no client change.
/// </summary>
/// <remarks>
/// <para>
/// <b>Inert unless a case sends a path under <c>…/projects/{projectId}/statuses</c></b>, which no earlier case does, so every
/// other case is served exactly what it would be without this partial.
/// </para>
/// <para>
/// <b>Bodies are the contract's <see cref="ProjectStatusResponse"/> shape</b>, serialised from <see cref="FakeProjectStatus"/>
/// with the same member names.
/// </para>
/// </remarks>
internal sealed partial class FakeYelloApi
{
    /// <summary>
    /// Each Project's effective set, in effective order. <see cref="SeedProjectStatuses"/> starts one from its Board.
    /// </summary>
    public IDictionary<(Guid SpaceId, Guid ProjectId), List<FakeProjectStatus>> ProjectStatuses { get; } =
        new Dictionary<(Guid SpaceId, Guid ProjectId), List<FakeProjectStatus>>();

    /// <summary>
    /// When set, the next Project Status write is refused with this status and problem type and changes nothing.
    /// </summary>
    public (int Status, string ProblemType)? NextProjectStatusRefusal { get; set; }

    /// <summary>
    /// Every Project Status write the page sent, in order.
    /// </summary>
    public IReadOnlyList<FakeRequest> ProjectStatusWrites =>
        [.. _requests.Where(request => request.Method != "GET" && request.Method != "OPTIONS" && TryProjectStatusPath(request.Path, out _, out _, out _, out _))];

    /// <summary>
    /// Starts a Project's effective set from its seeded Board: each column a Space default, in column order.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    public void SeedProjectStatuses(Guid spaceId, Guid projectId) =>
        ProjectStatuses[(spaceId, projectId)] =
        [
            .. Boards[(spaceId, projectId)].Columns.Select(column =>
                new FakeProjectStatus(column.StatusId, column.Name, ProjectStatusSources.SpaceDefault, DefaultName: null, column.Total)),
        ];

    /// <summary>
    /// The answer to a Project Status request, or <c>null</c> when the request is not one.
    /// </summary>
    private (int Status, string Body)? ProjectStatus(FakeRequest request)
    {
        if (!TryProjectStatusPath(request.Path, out var spaceId, out var projectId, out var statusId, out var place)
            || !ProjectStatuses.TryGetValue((spaceId, projectId), out var set))
        {
            return null;
        }

        if (request.Method == "GET" && statusId is null)
        {
            return (200, Entries(set));
        }

        if (NextProjectStatusRefusal is { } refusal)
        {
            NextProjectStatusRefusal = null;

            return (refusal.Status, Problem(refusal.ProblemType));
        }

        return (request.Method, statusId, place) switch
        {
            ("POST", null, _) => AddProjectStatus(spaceId, projectId, set, request.Body),
            ("PATCH", { } renamed, false) => RenameProjectStatus(spaceId, projectId, set, renamed, request.Body),
            ("POST", { } placed, true) => PlaceProjectStatus(spaceId, projectId, set, placed, request.Body),
            _ => (404, Problem(Yello.Contracts.ProblemTypes.NotAvailable)),
        };
    }

    private (int Status, string Body) AddProjectStatus(Guid spaceId, Guid projectId, List<FakeProjectStatus> set, string? body)
    {
        var name = ReadString(body, "name").Trim();

        set.Add(new FakeProjectStatus(Guid.NewGuid(), name, ProjectStatusSources.AddedHere, DefaultName: null, TaskCount: 0));
        FollowOnTheBoard(spaceId, projectId, set);

        return (201, Entries(set));
    }

    private (int Status, string Body) RenameProjectStatus(Guid spaceId, Guid projectId, List<FakeProjectStatus> set, Guid statusId, string? body)
    {
        var index = set.FindIndex(entry => entry.Id == statusId);

        if (index < 0)
        {
            return (404, Problem(Yello.Contracts.ProblemTypes.NotAvailable));
        }

        var entry = set[index];
        var defaultName = entry.Source == ProjectStatusSources.SpaceDefault ? entry.DefaultName ?? entry.Name : null;

        set[index] = entry with { Name = ReadString(body, "name").Trim(), DefaultName = defaultName };
        FollowOnTheBoard(spaceId, projectId, set);

        return (200, Entries(set));
    }

    private (int Status, string Body) PlaceProjectStatus(Guid spaceId, Guid projectId, List<FakeProjectStatus> set, Guid statusId, string? body)
    {
        var index = set.FindIndex(entry => entry.Id == statusId);

        if (index < 0)
        {
            return (404, Problem(Yello.Contracts.ProblemTypes.NotAvailable));
        }

        using var document = JsonDocument.Parse(body ?? "{}");
        var afterId = document.RootElement.TryGetProperty("afterStatusId", out var anchor) && anchor.ValueKind == JsonValueKind.String
            ? anchor.GetGuid()
            : Guid.Empty;
        var moving = set[index];

        set.RemoveAt(index);
        set.Insert(afterId == Guid.Empty ? 0 : set.FindIndex(entry => entry.Id == afterId) + 1, moving);
        FollowOnTheBoard(spaceId, projectId, set);

        return (200, Entries(set));
    }

    /// <summary>
    /// Rebuilds the Project's Board columns in the set's order and names, keeping each column's Tasks and total.
    /// </summary>
    private void FollowOnTheBoard(Guid spaceId, Guid projectId, List<FakeProjectStatus> set)
    {
        if (!Boards.TryGetValue((spaceId, projectId), out var board))
        {
            return;
        }

        var columns = board.Columns.ToDictionary(column => column.StatusId);

        Boards[(spaceId, projectId)] = board with
        {
            Columns =
            [
                .. set.Select(entry => columns.TryGetValue(entry.Id, out var column)
                    ? new FakeColumn(entry.Id, entry.Name, column.Total, column.Tasks)
                    : new FakeColumn(entry.Id, entry.Name, 0, [])),
            ],
        };
    }

    private static string Entries(List<FakeProjectStatus> set) => JsonSerializer.Serialize(set, JsonOptions);

    private static string ReadString(string? body, string member)
    {
        using var document = JsonDocument.Parse(body ?? "{}");

        return document.RootElement.TryGetProperty(member, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    /// <summary>
    /// Whether <paramref name="path"/> is <c>…/projects/{p}/statuses</c>, <c>…/statuses/{s}</c> or <c>…/statuses/{s}/place</c>.
    /// </summary>
    private static bool TryProjectStatusPath(string path, out Guid spaceId, out Guid projectId, out Guid? statusId, out bool place)
    {
        var segments = path.Split('/');
        var at = Array.LastIndexOf(segments, "statuses");

        statusId = null;
        place = false;
        spaceId = Guid.Empty;
        projectId = Guid.Empty;

        if (at < 1 || !TryProject(string.Join('/', segments[..at]), out spaceId, out projectId))
        {
            return false;
        }

        var rest = segments[(at + 1)..];

        if (rest.Length == 0)
        {
            return true;
        }

        if (!Guid.TryParse(rest[0], out var id) || rest.Length > 2 || (rest.Length == 2 && rest[1] != "place"))
        {
            return false;
        }

        statusId = id;
        place = rest.Length == 2;

        return true;
    }
}

/// <summary>
/// One entry of a Project's effective set as the fake serves it - the shape of <see cref="ProjectStatusResponse"/>, kept a
/// record of its own so a case can rewrite one entry with <c>with</c>.
/// </summary>
/// <param name="Id">The Status's id.</param>
/// <param name="Name">Its effective name in the Project.</param>
/// <param name="Source"><c>space-default</c> or <c>added-here</c>.</param>
/// <param name="DefaultName">The Space default's own name when the Project renamed it, else <c>null</c>.</param>
/// <param name="TaskCount">How many of the Project's Tasks hold it.</param>
internal sealed record FakeProjectStatus(Guid Id, string Name, string Source, string? DefaultName, int TaskCount);
