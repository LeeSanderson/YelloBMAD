using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Yello.Contracts;
using Yello.Contracts.Projects;
using Yello.Contracts.Spaces;

namespace Yello.Client.Components;

/// <summary>
/// The Status delta editor's removal and add-back: a Status no Task holds goes directly; one that Tasks hold goes through the
/// destination dialog; a removed Space default is listed with "Add back".
/// </summary>
/// <remarks>
/// <para>
/// <b>The count on the page can be stale.</b> A direct removal the server answers <c>409 status-destination-required</c> is
/// not a refusal to show: the set is read again and the dialog opens with the server's count.
/// </para>
/// <para>
/// <b>A write's <c>404</c> is read again before it is believed</b>: another Admin may have removed the Status. When the set
/// still reads, the editor says so inline and renders it; only a set that no longer reads is the page's refusal.
/// </para>
/// <para>
/// <b>Removed defaults are derived, never stored</b>: the Space's own Status read, less the ids the Project's set holds.
/// </para>
/// </remarks>
public sealed partial class StatusDeltaEditor
{
    // The add-back carries no body; the write path sends one anyway, and the endpoint reads none.
    private static readonly object NoBody = new();

    private List<StatusResponse>? _spaceDefaults;
    private ProjectStatusResponse? _removing;
    private string? _inFlightTo;
    private Guid? _removeFocusId;
    private int _removeFocusRequests;
    private bool _removalPending;
    private bool _destinationWanted;

    private List<StatusResponse> RemovedDefaults =>
        _spaceDefaults is { } defaults && _statuses is { } statuses
            ? [.. defaults.Where(status => !statuses.Exists(shown => shown.Id == status.Id)).OrderBy(status => status.Position)]
            : [];

    private void PurgeRemoval()
    {
        _spaceDefaults = null;
        _removing = null;
        _inFlightTo = null;
        _removeFocusId = null;
        _removalPending = false;
        _destinationWanted = false;
        _refusalHeading = null;
    }

    /// <summary>
    /// The Space's defaults, for the add-back list. Only a Role that may shape the set sees that list, and a failure leaves it
    /// empty rather than refusing the page.
    /// </summary>
    private async Task LoadSpaceDefaultsAsync(int generation, CancellationToken token)
    {
        if (!CanEdit)
        {
            return;
        }

        try
        {
            using var response = await Http.GetAsync(new Uri(SpaceRoutes.StatusesOf(SpaceId), UriKind.Relative), token);

            if (response.IsSuccessStatusCode && generation == _generation)
            {
                _spaceDefaults = await response.Content.ReadFromJsonAsync<List<StatusResponse>>(JsonOptions, token);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or JsonException)
        {
            // The list stays empty; the editor itself is unaffected.
        }
    }

    private async Task StartRemoveAsync(Guid statusId)
    {
        if (_busy || _statuses?.Find(status => status.Id == statusId) is not { } status)
        {
            return;
        }

        if (NeedsDestination(status))
        {
            Open(status);
            return;
        }

        var answered = await RemoveAsync(status.Id, destinationStatusId: null);

        if (answered is not null)
        {
            Say(string.Format(CultureInfo.CurrentCulture, Localizer["StatusRemoved"], status.Name));
            await _heading.FocusAsync();
            return;
        }

        if (_destinationWanted && await RereadAsync() && _statuses?.Find(entry => entry.Id == statusId) is { } recounted)
        {
            Open(recounted);
        }
    }

    // The only Status has nowhere to send its Tasks: it goes to the server, whose never-empty refusal is said inline.
    private bool NeedsDestination(ProjectStatusResponse status) => status.TaskCount > 0 && _statuses is { Count: > 1 };

    private async Task CommitRemovalAsync(Guid destinationStatusId)
    {
        if (_removing is not { } removing || _statuses?.Find(status => status.Id == destinationStatusId) is not { } destination)
        {
            return;
        }

        _inFlightTo = destination.Name;

        var answered = await RemoveAsync(removing.Id, destinationStatusId);

        _removing = null;
        _inFlightTo = null;

        if (answered is null)
        {
            return;
        }

        Say(string.Format(CultureInfo.CurrentCulture, Localizer["StatusRemovedMoved"], removing.Name, removing.TaskCount, destination.Name));
        _removeFocusId = destinationStatusId;
        _removeFocusRequests++;
    }

    private void CancelRemoval()
    {
        if (_removing is { } removing)
        {
            _removing = null;
            _removeFocusId = removing.Id;
            _removeFocusRequests++;
        }
    }

    private async Task RestoreAsync(Guid statusId)
    {
        var name = _spaceDefaults?.Find(status => status.Id == statusId)?.Name;

        if (await WriteAsync(HttpMethod.Post, ProjectStatusRoutes.RestoreOf(SpaceId, ProjectId, statusId), NoBody) is null)
        {
            return;
        }

        // The pressed "Add back" leaves with its entry; focus goes to the restored row rather than the page.
        _removeFocusId = statusId;
        _removeFocusRequests++;

        if (name is not null)
        {
            Say(string.Format(CultureInfo.CurrentCulture, Localizer["StatusAddedBack"], name));
        }
    }

    private void Open(ProjectStatusResponse status)
    {
        _refusal = null;
        _removing = status;
        _inFlightTo = null;
    }

    private async Task<List<ProjectStatusResponse>?> RemoveAsync(Guid statusId, Guid? destinationStatusId)
    {
        _removalPending = true;
        _destinationWanted = false;

        try
        {
            return await WriteAsync(HttpMethod.Post, ProjectStatusRoutes.RemoveOf(SpaceId, ProjectId, statusId), new RemoveProjectStatusRequest(destinationStatusId));
        }
        finally
        {
            _removalPending = false;
        }
    }

    /// <summary>
    /// A write answered <c>404</c>: the Status may have left the set under another person's removal. The set read again
    /// decides - it reads, and the editor says so inline; it does not, and the page refuses.
    /// </summary>
    private async Task RefuseNotFoundAsync(ProblemResponse? problem)
    {
        if (await RereadAsync())
        {
            Refuse(Localizer["StatusNoLongerHere"]);
            return;
        }

        await OnRefused.InvokeAsync(problem);
    }

    private async Task<bool> RereadAsync()
    {
        var generation = _generation;
        var token = ActiveSpace.Token;

        try
        {
            using var response = await Http.GetAsync(new Uri(ProjectStatusRoutes.CollectionOf(SpaceId, ProjectId), UriKind.Relative), token);

            if (!response.IsSuccessStatusCode || generation != _generation)
            {
                return false;
            }

            _statuses = await response.Content.ReadFromJsonAsync<List<ProjectStatusResponse>>(JsonOptions, token);

            return _statuses is not null;
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or JsonException)
        {
            return false;
        }
    }
}
