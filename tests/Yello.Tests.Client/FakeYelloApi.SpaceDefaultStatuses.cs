using System.Text.Json;
using Yello.Contracts;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;

namespace Yello.Tests.Client;

/// <summary>
/// The Space's default Statuses changed at Space level on the fake - add, rename, place - answered as the Host answers them:
/// every answer the Space's whole default list, and every seeded Project's set and Board following a new or renamed default,
/// so a browser case sees a Project page take the change on its next read.
/// </summary>
/// <remarks>
/// <para>
/// <b>Inert unless a case sends <c>POST</c> or <c>PATCH</c> under <c>/api/v1/spaces/{id}/statuses</c></b> for a Space whose
/// defaults were seeded (<see cref="SpaceDefaultStatuses"/>); no earlier case sends one, and the Space's own read stays
/// <c>FakeYelloApi.StatusRemoval.cs</c>'s.
/// </para>
/// <para>
/// <b>A rename's decisions, in the Host's order</b>: a Status that is not a default is the in-Space <c>404</c>; an unchanged
/// name is admitted as it is; another default's name, ignoring case, is <c>409 status-name-taken</c>; a Project whose own name
/// for the Status differs (ordinal) from both the old and the new Space name is a conflict, and conflicts with no decision are
/// <c>409 status-rename-conflict</c> carrying the report; a Project whose set would then hold a name twice makes it
/// <c>409 status-name-taken</c> naming those Projects; otherwise every Project follows and the answer is the defaults.
/// </para>
/// </remarks>
internal sealed partial class FakeYelloApi
{
    /// <summary>
    /// When set, the next Space-level default Status write is refused with this status and problem type and changes nothing.
    /// </summary>
    public (int Status, string ProblemType)? NextSpaceDefaultStatusRefusal { get; set; }

    /// <summary>
    /// Every Space-level default Status write the page sent, in order.
    /// </summary>
    public IReadOnlyList<FakeRequest> SpaceDefaultStatusWrites =>
        [.. _requests.Where(request => request.Method is "POST" or "PATCH" && TrySpaceDefaultStatusPath(request.Path, out _, out _, out _))];

    /// <summary>
    /// The answer to a Space-level default Status write; <c>null</c> when the request is not one.
    /// </summary>
    private (int Status, string Body)? SpaceDefaultStatusWrite(FakeRequest request)
    {
        if (request.Method is not ("POST" or "PATCH")
            || !TrySpaceDefaultStatusPath(request.Path, out var spaceId, out var statusId, out var place)
            || !SpaceDefaultStatuses.TryGetValue(spaceId, out var defaults))
        {
            return null;
        }

        if (NextSpaceDefaultStatusRefusal is { } refusal)
        {
            NextSpaceDefaultStatusRefusal = null;

            return (refusal.Status, Problem(refusal.ProblemType));
        }

        return (request.Method, statusId, place) switch
        {
            ("POST", null, false) => AddSpaceDefault(spaceId, defaults, request.Body),
            ("PATCH", { } renamed, false) => RenameSpaceDefault(spaceId, defaults, renamed, request.Body),
            ("POST", { } placed, true) => PlaceSpaceDefault(defaults, placed, request.Body),
            _ => (404, Problem(ProblemTypes.NotAvailable)),
        };
    }

    private (int Status, string Body) AddSpaceDefault(Guid spaceId, List<StatusResponse> defaults, string? body)
    {
        var name = ReadString(body, "name").Trim();

        if (defaults.Exists(status => string.Equals(status.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return (409, Problem(ProblemTypes.StatusNameTaken));
        }

        var clashing = SetsOf(spaceId)
            .Where(set => set.Entries.Exists(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)))
            .Select(set => set.ProjectId)
            .ToList();

        if (clashing.Count > 0)
        {
            return (409, NameTakenNaming(spaceId, clashing));
        }

        var added = new StatusResponse(Guid.NewGuid(), name, defaults.Count);
        var anchor = ReadAnchor(body);

        defaults.Insert(anchor is { } after ? defaults.FindIndex(status => status.Id == after) + 1 : defaults.Count, added);
        Renumber(defaults);

        foreach (var (projectId, set) in SetsOf(spaceId))
        {
            set.Insert(set.FindLastIndex(entry => entry.Source == ProjectStatusSources.SpaceDefault) + 1, new FakeProjectStatus(added.Id, name, ProjectStatusSources.SpaceDefault, DefaultName: null, TaskCount: 0));
            FollowOnTheBoard(spaceId, projectId, set);
        }

        return (201, JsonSerializer.Serialize(defaults, JsonOptions));
    }

    private (int Status, string Body) RenameSpaceDefault(Guid spaceId, List<StatusResponse> defaults, Guid statusId, string? body)
    {
        var index = defaults.FindIndex(status => status.Id == statusId);

        if (index < 0)
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        var old = defaults[index].Name;
        var name = ReadString(body, "name").Trim();

        if (string.Equals(old, name, StringComparison.Ordinal))
        {
            return (200, JsonSerializer.Serialize(defaults, JsonOptions));
        }

        if (defaults.Exists(status => status.Id != statusId && string.Equals(status.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return (409, Problem(ProblemTypes.StatusNameTaken));
        }

        bool IsConflict(FakeProjectStatus entry) =>
            entry.DefaultName is not null && !string.Equals(entry.Name, old, StringComparison.Ordinal) && !string.Equals(entry.Name, name, StringComparison.Ordinal);

        var sets = SetsOf(spaceId);
        var conflicts = sets
            .Select(set => (set.ProjectId, Entry: set.Entries.Find(entry => entry.Id == statusId)))
            .Where(found => found.Entry is not null && IsConflict(found.Entry))
            .ToList();
        var decision = ReadDecision(body);

        if (conflicts.Count > 0 && decision is null)
        {
            var report = conflicts.Select(found => new { projectId = found.ProjectId, projectName = ProjectName(spaceId, found.ProjectId), name = found.Entry!.Name, taskCount = found.Entry.TaskCount });

            return (409, JsonSerializer.Serialize(new { type = ProblemTypes.StatusRenameConflict, conflicts = report }, JsonOptions));
        }

        string NameAfter(FakeProjectStatus entry) => IsConflict(entry) && decision is false ? entry.Name : name;

        var clashing = sets
            .Where(set => set.Entries.Find(entry => entry.Id == statusId) is { } entry
                && set.Entries.Exists(other => other.Id != statusId && string.Equals(other.Name, NameAfter(entry), StringComparison.OrdinalIgnoreCase)))
            .Select(set => set.ProjectId)
            .ToList();

        if (clashing.Count > 0)
        {
            return (409, NameTakenNaming(spaceId, clashing));
        }

        defaults[index] = defaults[index] with { Name = name };
        FollowTheRename(spaceId, statusId, name, NameAfter);

        return (200, JsonSerializer.Serialize(defaults, JsonOptions));
    }

    /// <summary>
    /// Gives every seeded Project's entry for the renamed default the name it ends with, the new Space name as its default
    /// name when that differs, and rebuilds its Board.
    /// </summary>
    private void FollowTheRename(Guid spaceId, Guid statusId, string name, Func<FakeProjectStatus, string> nameAfter)
    {
        foreach (var (projectId, set) in SetsOf(spaceId))
        {
            var at = set.FindIndex(entry => entry.Id == statusId);

            if (at < 0)
            {
                continue;
            }

            var after = nameAfter(set[at]);

            set[at] = set[at] with { Name = after, DefaultName = string.Equals(after, name, StringComparison.Ordinal) ? null : name };
            FollowOnTheBoard(spaceId, projectId, set);
        }
    }

    private static (int Status, string Body) PlaceSpaceDefault(List<StatusResponse> defaults, Guid statusId, string? body)
    {
        var index = defaults.FindIndex(status => status.Id == statusId);

        if (index < 0)
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        var anchor = ReadAnchor(body);

        if (anchor is { } named && (named == statusId || defaults.TrueForAll(status => status.Id != named)))
        {
            return (400, Problem(ProblemTypes.InvalidStatusPosition));
        }

        var moving = defaults[index];

        defaults.RemoveAt(index);
        defaults.Insert(anchor is { } after ? defaults.FindIndex(status => status.Id == after) + 1 : 0, moving);
        Renumber(defaults);

        return (200, JsonSerializer.Serialize(defaults, JsonOptions));
    }

    private List<(Guid ProjectId, List<FakeProjectStatus> Entries)> SetsOf(Guid spaceId) =>
        [.. ProjectStatuses.Where(pair => pair.Key.SpaceId == spaceId).Select(pair => (pair.Key.ProjectId, pair.Value))];

    private string ProjectName(Guid spaceId, Guid projectId) =>
        Projects.TryGetValue(spaceId, out var projects) && projects.Find(project => project.Id == projectId) is { } project ? project.Name : string.Empty;

    private string NameTakenNaming(Guid spaceId, IEnumerable<Guid> projectIds) =>
        JsonSerializer.Serialize(
            new { type = ProblemTypes.StatusNameTaken, projects = projectIds.Select(id => new { id, name = ProjectName(spaceId, id) }) },
            JsonOptions);

    private static void Renumber(List<StatusResponse> defaults)
    {
        for (var position = 0; position < defaults.Count; position++)
        {
            defaults[position] = defaults[position] with { Position = position };
        }
    }

    private static Guid? ReadAnchor(string? body)
    {
        using var document = JsonDocument.Parse(body ?? "{}");

        return document.RootElement.TryGetProperty("afterStatusId", out var anchor) && anchor.ValueKind == JsonValueKind.String
            ? anchor.GetGuid()
            : null;
    }

    private static bool? ReadDecision(string? body)
    {
        using var document = JsonDocument.Parse(body ?? "{}");

        return document.RootElement.TryGetProperty("replaceProjectNames", out var decision) && decision.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? decision.GetBoolean()
            : null;
    }

    /// <summary>
    /// Whether <paramref name="path"/> is <c>/api/v1/spaces/{s}/statuses</c>, <c>…/statuses/{id}</c> or
    /// <c>…/statuses/{id}/place</c> - the Space's own, never a Project's.
    /// </summary>
    private static bool TrySpaceDefaultStatusPath(string path, out Guid spaceId, out Guid? statusId, out bool place)
    {
        var prefix = SpaceRoutes.Directory + "/";

        spaceId = Guid.Empty;
        statusId = null;
        place = false;

        if (!path.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var segments = path[prefix.Length..].Split('/');

        if (segments.Length < 2 || segments.Length > 4 || segments[1] != "statuses" || !Guid.TryParse(segments[0], out spaceId))
        {
            return false;
        }

        if (segments.Length == 2)
        {
            return true;
        }

        if (!Guid.TryParse(segments[2], out var id) || (segments.Length == 4 && segments[3] != "place"))
        {
            return false;
        }

        statusId = id;
        place = segments.Length == 4;

        return true;
    }
}
