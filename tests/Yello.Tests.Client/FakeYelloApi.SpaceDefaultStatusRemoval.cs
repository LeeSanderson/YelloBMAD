using System.Text.Json;
using Yello.Contracts;
using Yello.Contracts.Spaces;
using Yello.Tests.Shared;

namespace Yello.Tests.Client;

/// <summary>
/// A Space default removed at Space level on the fake, answered as the Host is to answer it: every seeded Project's set and
/// Board follow, so a browser case sees the column gone on its next read. Also a removal applied behind the page's back, for a
/// case that needs the server to have changed while a Board is open.
/// </summary>
/// <remarks>
/// <para>
/// <b>Inert unless a case sends <c>POST …/spaces/{id}/statuses/{statusId}/remove</c></b> for a Space whose defaults were seeded
/// (<see cref="SpaceDefaultStatuses"/>), or calls <see cref="RemoveSpaceDefaultBehindThePage"/>; no earlier case does either.
/// </para>
/// <para>
/// <b>The decisions, in the Host's order</b>, so a browser case proves the page and not the fake: a Status that is not a
/// default is the in-Space <c>404</c>; the last default is <c>409 status-set-empty</c>; a Project whose set is only that
/// Status makes it <c>409 status-set-empty</c> naming those Projects; an unreadable body, or a Space-wide destination that
/// is not another default, is <c>400 invalid-status-destination</c>; Tasks in it and no destination is
/// <c>409 status-destination-required</c> carrying the occupancy report; an exception for a blocked Project outside that
/// Project's set after the removal is <c>400</c> naming it; a blocked Project with no exception makes it
/// <c>409 status-project-destination-required</c> reporting those Projects; otherwise each affected Project's Tasks join
/// the end of their destination's column and the answer is the defaults left. A Project is blocked when it holds the Status
/// with Tasks in it and its set after the removal lacks the Space-wide destination. Exceptions for any other Project are
/// ignored.
/// </para>
/// <para>
/// <b>The report's member is <c>removal</c></b>, as <see cref="SpaceStatusWire.RemovalOf"/> reads it - the story's
/// recommended name, corrected in both places if the contract chooses another.
/// </para>
/// </remarks>
internal sealed partial class FakeYelloApi
{
    private TaskCompletionSource? _heldSpaceStatusRemoval;
    private TaskCompletionSource? _arrivedSpaceStatusRemoval;
    private TaskCompletionSource? _releaseSpaceStatusRemoval;

    /// <summary>
    /// When set, the next Space-level removal is refused with this status and problem type and changes nothing.
    /// </summary>
    public (int Status, string ProblemType)? NextSpaceStatusRemovalRefusal { get; set; }

    /// <summary>
    /// Every Space-level removal the page sent, in order. <see cref="StatusRemovals"/> counts these too, because it matches
    /// any path ending <c>/remove</c>.
    /// </summary>
    public IReadOnlyList<FakeRequest> SpaceStatusRemovals =>
        [.. _requests.Where(request => request.Method == "POST" && TrySpaceStatusRemovePath(request.Path, out _, out _))];

    /// <summary>
    /// Holds the <b>next</b> Space-level removal until <see cref="ReleaseSpaceStatusRemoval"/>.
    /// </summary>
    /// <returns>A task that completes when the held removal has arrived and is waiting.</returns>
    public Task HoldSpaceStatusRemovalAsync()
    {
        _heldSpaceStatusRemoval = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _releaseSpaceStatusRemoval = _heldSpaceStatusRemoval;
        _arrivedSpaceStatusRemoval = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        return _arrivedSpaceStatusRemoval.Task;
    }

    /// <summary>
    /// Lets the held Space-level removal go; it is then decided as any other.
    /// </summary>
    public void ReleaseSpaceStatusRemoval() => _releaseSpaceStatusRemoval?.TrySetResult();

    /// <summary>
    /// Removes <paramref name="statusId"/> from the Space as an admitted removal to <paramref name="destinationStatusId"/>
    /// would, without a request: the defaults, every seeded set, and every Board of the Space - including a Board seeded with
    /// no set - lose the column, whose Tasks join the end of the destination's.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="statusId">The default removed.</param>
    /// <param name="destinationStatusId">Where its Tasks go.</param>
    public void RemoveSpaceDefaultBehindThePage(Guid spaceId, Guid statusId, Guid destinationStatusId)
    {
        if (SpaceDefaultStatuses.TryGetValue(spaceId, out var defaults))
        {
            defaults.RemoveAll(status => status.Id == statusId);
        }

        foreach (var key in Boards.Keys.Where(key => key.SpaceId == spaceId).ToList())
        {
            if (ProjectStatuses.TryGetValue(key, out var set))
            {
                RemoveFromSet(key.SpaceId, key.ProjectId, set, statusId, destinationStatusId);
                continue;
            }

            MigrateOnTheBoard(key.SpaceId, key.ProjectId, statusId, destinationStatusId);

            var board = Boards[key];

            Boards[key] = board with { Columns = [.. board.Columns.Where(column => column.StatusId != statusId)] };
        }
    }

    /// <summary>
    /// The answer to a Space-level removal; <c>null</c> when the request is not one.
    /// </summary>
    private async Task<(int Status, string Body)?> SpaceStatusRemovalAsync(FakeRequest request)
    {
        if (request.Method != "POST"
            || !TrySpaceStatusRemovePath(request.Path, out var spaceId, out var statusId)
            || !SpaceDefaultStatuses.TryGetValue(spaceId, out var defaults))
        {
            return null;
        }

        if (Interlocked.Exchange(ref _heldSpaceStatusRemoval, null) is { } held)
        {
            _arrivedSpaceStatusRemoval?.TrySetResult();
            await held.Task.ConfigureAwait(false);
        }

        if (NextSpaceStatusRemovalRefusal is { } refusal)
        {
            NextSpaceStatusRemovalRefusal = null;

            return (refusal.Status, Problem(refusal.ProblemType));
        }

        return RemoveSpaceDefault(spaceId, defaults, statusId, request.Body);
    }

    private (int Status, string Body) RemoveSpaceDefault(Guid spaceId, List<StatusResponse> defaults, Guid statusId, string? body)
    {
        if (defaults.TrueForAll(status => status.Id != statusId))
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        if (defaults.Count == 1)
        {
            return (409, Problem(ProblemTypes.StatusSetEmpty));
        }

        var holding = SetsOf(spaceId).Where(set => set.Entries.Exists(entry => entry.Id == statusId)).ToList();
        var leftEmpty = holding.Where(set => set.Entries.Count == 1).Select(set => set.ProjectId).ToList();

        if (leftEmpty.Count > 0)
        {
            return (409, Naming(ProblemTypes.StatusSetEmpty, spaceId, leftEmpty));
        }

        if (!TryReadRemoval(body, out var destination, out var exceptions) || !IsAnotherDefault(defaults, statusId, destination))
        {
            return (400, Problem(ProblemTypes.InvalidStatusDestination));
        }

        var occupied = holding.Where(set => CountIn(set.Entries, statusId) > 0).Select(set => set.ProjectId).ToList();

        if (occupied.Count > 0 && destination is null)
        {
            return (409, Reporting(ProblemTypes.StatusDestinationRequired, spaceId, occupied, statusId));
        }

        var blocked = occupied.Where(projectId => !ProjectStatuses[(spaceId, projectId)].Exists(entry => entry.Id == destination)).ToList();

        return Decided(spaceId, defaults, statusId, destination, exceptions, blocked);
    }

    /// <summary>
    /// The decisions after the occupancy check: an invalid exception, a blocked Project with none, or the removal applied.
    /// </summary>
    private (int Status, string Body) Decided(Guid spaceId, List<StatusResponse> defaults, Guid statusId, Guid? destination, Dictionary<Guid, Guid> exceptions, List<Guid> blocked)
    {
        foreach (var projectId in blocked)
        {
            if (exceptions.TryGetValue(projectId, out var own) && (own == statusId || !ProjectStatuses[(spaceId, projectId)].Exists(entry => entry.Id == own)))
            {
                return (400, Naming(ProblemTypes.InvalidStatusDestination, spaceId, [projectId]));
            }
        }

        var missing = blocked.Where(projectId => !exceptions.ContainsKey(projectId)).ToList();

        if (missing.Count > 0)
        {
            return (409, Reporting(SpaceStatusWire.StatusProjectDestinationRequired, spaceId, missing, statusId));
        }

        foreach (var (projectId, entries) in SetsOf(spaceId))
        {
            RemoveFromSet(spaceId, projectId, entries, statusId, blocked.Contains(projectId) ? exceptions[projectId] : destination);
        }

        defaults.RemoveAll(status => status.Id == statusId);

        return (200, JsonSerializer.Serialize(defaults, JsonOptions));
    }

    /// <summary>
    /// Whether a Space-wide destination is absent or another default of the Space.
    /// </summary>
    private static bool IsAnotherDefault(List<StatusResponse> defaults, Guid statusId, Guid? destination) =>
        destination is not { } named || (named != statusId && defaults.Exists(status => status.Id == named));

    private static int CountIn(List<FakeProjectStatus> set, Guid statusId) => set.Find(entry => entry.Id == statusId)?.TaskCount ?? 0;

    /// <summary>
    /// Takes <paramref name="statusId"/> out of one Project's set, its Tasks and count joining <paramref name="to"/>'s, and
    /// rebuilds the Board. A set that does not hold it is left as it is.
    /// </summary>
    private void RemoveFromSet(Guid spaceId, Guid projectId, List<FakeProjectStatus> set, Guid statusId, Guid? to)
    {
        var at = set.FindIndex(entry => entry.Id == statusId);

        if (at < 0)
        {
            return;
        }

        var removed = set[at];

        set.RemoveAt(at);

        if (to is { } destination && set.FindIndex(entry => entry.Id == destination) is var into and >= 0)
        {
            set[into] = set[into] with { TaskCount = set[into].TaskCount + removed.TaskCount };
            MigrateOnTheBoard(spaceId, projectId, statusId, destination);
        }

        FollowOnTheBoard(spaceId, projectId, set);
    }

    private string Naming(string type, Guid spaceId, IEnumerable<Guid> projectIds) =>
        JsonSerializer.Serialize(
            new { type, projects = projectIds.Select(id => new { id, name = ProjectName(spaceId, id) }) },
            JsonOptions);

    private string Reporting(string type, Guid spaceId, IEnumerable<Guid> projectIds, Guid statusId) =>
        JsonSerializer.Serialize(
            new
            {
                type,
                removal = projectIds.Select(projectId => new
                {
                    projectId,
                    projectName = ProjectName(spaceId, projectId),
                    taskCount = CountIn(ProjectStatuses[(spaceId, projectId)], statusId),
                    destinations = ProjectStatuses[(spaceId, projectId)].Where(entry => entry.Id != statusId).Select(entry => new { id = entry.Id, name = entry.Name }),
                }),
            },
            JsonOptions);

    /// <summary>
    /// The Space-wide destination and the exceptions by Project; <c>false</c> for an unreadable body, an exception that is not
    /// two ids, or one Project named twice.
    /// </summary>
    private static bool TryReadRemoval(string? body, out Guid? destination, out Dictionary<Guid, Guid> exceptions)
    {
        destination = null;
        exceptions = [];

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(body ?? "{}");
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (root.TryGetProperty("destinationStatusId", out var named) && named.ValueKind == JsonValueKind.String)
            {
                destination = named.TryGetGuid(out var id) ? id : Guid.Empty;
            }

            return !root.TryGetProperty("exceptions", out var list) || list.ValueKind != JsonValueKind.Array || ReadExceptions(list, exceptions);
        }
    }

    private static bool ReadExceptions(JsonElement list, Dictionary<Guid, Guid> exceptions)
    {
        foreach (var exception in list.EnumerateArray())
        {
            if (!TryGuid(exception, "projectId", out var projectId) || !TryGuid(exception, "destinationStatusId", out var to) || !exceptions.TryAdd(projectId, to))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryGuid(JsonElement element, string member, out Guid value)
    {
        value = Guid.Empty;

        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(member, out var property))
        {
            return false;
        }

        return property.ValueKind == JsonValueKind.String && property.TryGetGuid(out value);
    }

    /// <summary>
    /// Whether <paramref name="path"/> is <c>/api/v1/spaces/{s}/statuses/{id}/remove</c> - the Space's own, never a Project's.
    /// </summary>
    private static bool TrySpaceStatusRemovePath(string path, out Guid spaceId, out Guid statusId)
    {
        var prefix = SpaceRoutes.Directory + "/";
        var segments = path.StartsWith(prefix, StringComparison.Ordinal) ? path[prefix.Length..].Split('/') : [];

        spaceId = Guid.Empty;
        statusId = Guid.Empty;

        if (segments.Length != 4 || segments[1] != "statuses" || segments[3] != "remove")
        {
            return false;
        }

        return Guid.TryParse(segments[0], out spaceId) && Guid.TryParse(segments[2], out statusId);
    }
}
