using System.Globalization;
using System.Text.Json;
using Yello.Contracts;
using Yello.Contracts.Spaces;

namespace Yello.Tests.Client;

/// <summary>
/// Removing a Status from a Project, and adding a removed Space default back, on the fake - answered as the Host answers
/// them - and the Space's own Status read the editor compares with the Project's set to list the defaults it removed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Inert unless a case sends <c>…/statuses/{id}/remove</c>, <c>…/statuses/{id}/restore</c>, or reads the Space's Statuses
/// of a Space whose Project's set was seeded</b>; no case before the removal sends any of them.
/// </para>
/// <para>
/// <b>The removal's decisions, in the Host's order</b>: a Status not in the set is the in-Space <c>404</c>; a set of one is
/// <c>409 status-set-empty</c>; a destination outside the set after the removal is <c>400 invalid-status-destination</c>; an
/// occupied Status with no destination is <c>409 status-destination-required</c>; otherwise its Tasks join the end of the
/// destination's column, the destination's count grows by theirs, and the answer is the whole set. A held removal waits for
/// its release, so a case can see the in-flight state.
/// </para>
/// </remarks>
internal sealed partial class FakeYelloApi
{
    private static readonly string[] RegistrationSeeds = ["Todo", "Doing", "Done"];

    private TaskCompletionSource? _heldStatusRemoval;
    private TaskCompletionSource? _arrivedStatusRemoval;

    // Kept apart from _heldStatusRemoval, which the held request takes (and clears) when it arrives.
    private TaskCompletionSource? _releaseStatusRemoval;

    /// <summary>
    /// Each Space's default Statuses as its own read serves them, in Space order. <see cref="SeedProjectStatuses"/> fills it.
    /// </summary>
    public IDictionary<Guid, List<StatusResponse>> SpaceDefaultStatuses { get; } = new Dictionary<Guid, List<StatusResponse>>();

    /// <summary>
    /// Every removal the page sent, in order.
    /// </summary>
    public IReadOnlyList<FakeRequest> StatusRemovals =>
        [.. _requests.Where(request => request.Method == "POST" && request.Path.EndsWith("/remove", StringComparison.Ordinal))];

    /// <summary>
    /// Every add-back the page sent, in order.
    /// </summary>
    public IReadOnlyList<FakeRequest> StatusRestores =>
        [.. _requests.Where(request => request.Method == "POST" && request.Path.EndsWith("/restore", StringComparison.Ordinal))];

    /// <summary>
    /// Holds the <b>next</b> removal until <see cref="ReleaseStatusRemoval"/>.
    /// </summary>
    /// <returns>A task that completes when the held removal has arrived and is waiting.</returns>
    public Task HoldStatusRemovalAsync()
    {
        _heldStatusRemoval = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _releaseStatusRemoval = _heldStatusRemoval;
        _arrivedStatusRemoval = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        return _arrivedStatusRemoval.Task;
    }

    /// <summary>
    /// Lets the held removal go; it is then decided as any other.
    /// </summary>
    public void ReleaseStatusRemoval() => _releaseStatusRemoval?.TrySetResult();

    /// <summary>
    /// A Space's defaults as its read serves them. A Space the caller holds a Role in that no case seeded starts with the
    /// three registration seeds, as the Host's does: Space settings reads them for every Owner and Admin, and the Host never
    /// answers that read <c>404</c> for a member.
    /// </summary>
    private List<StatusResponse>? DefaultsOf(Guid spaceId)
    {
        if (SpaceDefaultStatuses.TryGetValue(spaceId, out var seeded))
        {
            return seeded;
        }

        if (!Roles.ContainsKey(spaceId))
        {
            return null;
        }

        List<StatusResponse> defaults = [.. RegistrationSeeds.Select((name, position) => new StatusResponse(Guid.NewGuid(), name, position))];

        SpaceDefaultStatuses[spaceId] = defaults;

        return defaults;
    }

    /// <summary>
    /// The answer to a removal, an add-back or a Space's Status read; <c>null</c> when the request is none of them.
    /// </summary>
    private async Task<(int Status, string Body)?> StatusRemovalAsync(FakeRequest request)
    {
        if (request.Method == "GET" && TrySpaceStatuses(request.Path, out var spaceId) && DefaultsOf(spaceId) is { } defaults)
        {
            return (200, JsonSerializer.Serialize(defaults, JsonOptions));
        }

        if (request.Method != "POST" || !TryRemovalPath(request.Path, out var action, out var space, out var project, out var statusId)
            || !ProjectStatuses.TryGetValue((space, project), out var set))
        {
            return null;
        }

        if (action == "remove" && Interlocked.Exchange(ref _heldStatusRemoval, null) is { } held)
        {
            _arrivedStatusRemoval?.TrySetResult();
            await held.Task.ConfigureAwait(false);
        }

        if (NextProjectStatusRefusal is { } refusal)
        {
            NextProjectStatusRefusal = null;

            return (refusal.Status, Problem(refusal.ProblemType));
        }

        return action == "remove"
            ? RemoveProjectStatus(space, project, set, statusId, request.Body)
            : RestoreProjectStatus(space, project, set, statusId);
    }

    private (int Status, string Body) RemoveProjectStatus(Guid spaceId, Guid projectId, List<FakeProjectStatus> set, Guid statusId, string? body)
    {
        var index = set.FindIndex(entry => entry.Id == statusId);

        if (index < 0)
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        if (set.Count == 1)
        {
            return (409, Problem(ProblemTypes.StatusSetEmpty));
        }

        Guid? destination;

        try
        {
            destination = ReadDestination(body);
        }
        catch (JsonException)
        {
            return (400, Problem(ProblemTypes.InvalidStatusDestination));
        }

        if (destination is { } named && (named == statusId || set.TrueForAll(entry => entry.Id != named)))
        {
            return (400, Problem(ProblemTypes.InvalidStatusDestination));
        }

        var removed = set[index];

        if (removed.TaskCount > 0 && destination is null)
        {
            return (409, Problem(ProblemTypes.StatusDestinationRequired));
        }

        set.RemoveAt(index);

        if (destination is { } to)
        {
            var at = set.FindIndex(entry => entry.Id == to);

            set[at] = set[at] with { TaskCount = set[at].TaskCount + removed.TaskCount };
            MigrateOnTheBoard(spaceId, projectId, statusId, to);
        }

        FollowOnTheBoard(spaceId, projectId, set);

        return (200, Entries(set));
    }

    private (int Status, string Body) RestoreProjectStatus(Guid spaceId, Guid projectId, List<FakeProjectStatus> set, Guid statusId)
    {
        if (!SpaceDefaultStatuses.TryGetValue(spaceId, out var defaults)
            || defaults.Find(status => status.Id == statusId) is not { } restored
            || set.Exists(entry => entry.Id == statusId))
        {
            return (404, Problem(ProblemTypes.NotAvailable));
        }

        if (set.Exists(entry => string.Equals(entry.Name, restored.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return (409, Problem(ProblemTypes.StatusNameTaken));
        }

        // Back at its Space slot: after the last default of the set that comes before it in Space order.
        var before = defaults.Where(status => status.Position < restored.Position).Select(status => status.Id).ToHashSet();
        var after = set.FindLastIndex(entry => before.Contains(entry.Id));

        set.Insert(after + 1, new FakeProjectStatus(restored.Id, restored.Name, Yello.Contracts.Projects.ProjectStatusSources.SpaceDefault, DefaultName: null, TaskCount: 0));
        FollowOnTheBoard(spaceId, projectId, set);

        return (200, Entries(set));
    }

    /// <summary>
    /// Moves the removed column's Tasks to the end of the destination's, with keys after its last, and its total with them.
    /// </summary>
    private void MigrateOnTheBoard(Guid spaceId, Guid projectId, Guid fromStatusId, Guid toStatusId)
    {
        if (!Boards.TryGetValue((spaceId, projectId), out var board)
            || board.Columns.FirstOrDefault(column => column.StatusId == fromStatusId) is not { } from
            || board.Columns.FirstOrDefault(column => column.StatusId == toStatusId) is not { } to)
        {
            return;
        }

        var last = to.Tasks.Count == 0 ? "a" : to.Tasks[^1].PositionKey;

        for (var i = 0; i < from.Tasks.Count; i++)
        {
            to.Tasks.Add(from.Tasks[i] with { StatusId = toStatusId, PositionKey = last + "z" + i.ToString("D4", CultureInfo.InvariantCulture) });
        }

        to.Total += from.Total;
        from.Tasks.Clear();
        from.Total = 0;
    }

    private static Guid? ReadDestination(string? body)
    {
        using var document = JsonDocument.Parse(body ?? "{}");

        return document.RootElement.TryGetProperty("destinationStatusId", out var destination) && destination.ValueKind == JsonValueKind.String
            ? destination.GetGuid()
            : null;
    }

    /// <summary>
    /// Whether <paramref name="path"/> is <c>…/projects/{p}/statuses/{s}/remove</c> or <c>…/restore</c>.
    /// </summary>
    private static bool TryRemovalPath(string path, out string action, out Guid spaceId, out Guid projectId, out Guid statusId)
    {
        var cut = path.LastIndexOf('/');

        action = cut < 0 ? string.Empty : path[(cut + 1)..];
        statusId = Guid.Empty;
        spaceId = Guid.Empty;
        projectId = Guid.Empty;

        if (action is not ("remove" or "restore"))
        {
            return false;
        }

        if (!TryProjectStatusPath(path[..cut], out spaceId, out projectId, out var status, out var place) || place || status is not { } named)
        {
            return false;
        }

        statusId = named;

        return true;
    }

    /// <summary>
    /// Whether <paramref name="path"/> is the Space's own Status read, <c>/api/v1/spaces/{id}/statuses</c>.
    /// </summary>
    private static bool TrySpaceStatuses(string path, out Guid spaceId)
    {
        const string leaf = "/statuses";

        spaceId = Guid.Empty;

        return path.EndsWith(leaf, StringComparison.Ordinal) && TryActiveSpace(path[..^leaf.Length], out spaceId);
    }
}
