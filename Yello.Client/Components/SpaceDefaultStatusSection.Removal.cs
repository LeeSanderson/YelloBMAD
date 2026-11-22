using System.Net;
using Microsoft.AspNetCore.Components;
using Yello.Client.Refusals;
using Yello.Contracts;
using Yello.Contracts.Spaces;

namespace Yello.Client.Components;

/// <summary>
/// The Default Statuses section's removal: sent first with no destination, then - when the server reports Tasks in the
/// Status - asked again through the removal dialog with the Admin's own choices.
/// </summary>
/// <remarks>
/// <para>
/// <b>The report is the server's</b>: the dialog opens from the <c>409</c>'s report, and a refusal at commit that names
/// Projects replaces their entries, so a Project that lost the destination meanwhile is asked about with its picker unset.
/// </para>
/// <para>
/// <b>"Nothing was moved" only when the server answered.</b> A removal that never got an answer may have committed, so the
/// dialog then says it was not sent and claims nothing about the Tasks.
/// </para>
/// </remarks>
public sealed partial class SpaceDefaultStatusSection
{
    private ElementReference _heading;
    private bool _focusHeading;
    private Removal? _removal;
    private bool _removing;
    private int _removalAttempts;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusHeading && _defaults is not null)
        {
            _focusHeading = false;
            await _heading.FocusAsync();
        }
    }

    private void PurgeRemoval()
    {
        _removal = null;
        _removing = false;
        _focusHeading = false;
    }

    private async Task StartRemoveAsync(Guid statusId)
    {
        if (_busy || _defaults?.Find(status => status.Id == statusId) is not { } status)
        {
            return;
        }

        var answer = await WriteAsync(HttpMethod.Post, SpaceRoutes.StatusRemoveOf(SpaceId, statusId), new RemoveSpaceStatusRequest(null, null));

        if (answer.Defaults is not null)
        {
            Say(Format("SpaceStatusRemoved", status.Name));
            _focusHeading = true;
            return;
        }

        if (answer.Problem is { Type: var type, Removal: { Count: > 0 } report } && type == ProblemTypes.StatusDestinationRequired)
        {
            _removal = new Removal(statusId, status.Name, report, Refusal: null);
            return;
        }

        if (answer.Problem?.Type == ProblemTypes.StatusSetEmpty)
        {
            Refuse(LeftEmpty(answer.Problem.Projects));
            return;
        }

        await RefuseAsync(answer);
    }

    private async Task CommitRemovalAsync(RemoveSpaceStatusRequest request)
    {
        if (_removal is not { } removal || _busy)
        {
            return;
        }

        _removing = true;

        var answer = await WriteAsync(HttpMethod.Post, SpaceRoutes.StatusRemoveOf(SpaceId, removal.StatusId), request);

        _removing = false;

        if (_removal is null)
        {
            return;
        }

        if (answer.Defaults is not null)
        {
            var moved = removal.Report.Sum(project => project.TaskCount);

            _removal = null;
            Say(moved == 1 ? Format("SpaceStatusRemovedMovedOne", removal.Name) : Format("SpaceStatusRemovedMovedMany", removal.Name, moved));
            _focusHeading = true;
            return;
        }

        if (answer.Status == HttpStatusCode.NotFound)
        {
            _removal = null;
            await RereadAsync(answer);
            return;
        }

        RefuseRemoval(removal, request, answer);
    }

    /// <summary>
    /// A removal whose Status the server no longer has: the section reads the defaults again, inside the busy guard so no
    /// other write starts against the list being replaced.
    /// </summary>
    private async Task RereadAsync(Answer answer)
    {
        _busy = true;

        try
        {
            await RefuseAsync(answer);
        }
        finally
        {
            _busy = false;
        }
    }

    private void RefuseRemoval(Removal removal, RemoveSpaceStatusRequest request, Answer answer)
    {
        if (answer.Status is null)
        {
            _removal = removal with { Refusal = new SpaceStatusRemovalRefusal(Localizer["TaskEditNotSent"], []) };
            _refusal = null;
            _removalAttempts++;
            return;
        }

        var problem = answer.Problem;
        var report = removal.Report;
        string line;

        if (problem is { Removal: { Count: > 0 } named } && problem.Type == ProblemTypes.StatusProjectDestinationRequired)
        {
            report = [.. report.Where(entry => !named.Any(project => project.ProjectId == entry.ProjectId)), .. named];
            line = NoLongerHas(named, request.DestinationStatusId);
        }
        else if (problem is { Projects: { Count: > 0 } projects } && problem.Type == ProblemTypes.InvalidStatusDestination)
        {
            line = Format("SpaceStatusRemoveExceptionInvalid", string.Join(", ", projects.Select(project => project.Name)));
        }
        else if (problem?.Type == ProblemTypes.StatusSetEmpty)
        {
            line = LeftEmpty(problem.Projects);
        }
        else
        {
            line = RefusalText.Of(Localizer, problem);
        }

        _removal = removal with { Report = report, Refusal = new SpaceStatusRemovalRefusal(Localizer["StatusRemoveRefused"], [line]) };
        _removalAttempts++;
    }

    /// <summary>
    /// Cancelling asks nothing of the server; focus goes back to the row's "Remove".
    /// </summary>
    private void CancelRemoval()
    {
        if (_removal is { } removal && !_removing)
        {
            _removal = null;
            FocusRow(removal.StatusId, SpaceStatusFocus.Remove);
        }
    }

    private string NoLongerHas(IReadOnlyList<SpaceStatusOccupancyResponse> named, Guid? destinationStatusId)
    {
        var destination = _defaults?.Find(status => status.Id == destinationStatusId)?.Name ?? string.Empty;

        return named is [var only]
            ? Format("SpaceStatusRemoveNoLongerHasOne", only.ProjectName, destination)
            : Format("SpaceStatusRemoveNoLongerHasMany", string.Join(", ", named.Select(project => project.ProjectName)), destination);
    }

    private string LeftEmpty(IReadOnlyList<ProjectIdentityResponse>? projects) => projects switch
    {
        null or { Count: 0 } => Localizer["SpaceStatusRemoveLastDefault"],
        [var only] => Format("SpaceStatusRemoveLeavesEmptyOne", only.Name),
        _ => Format("SpaceStatusRemoveLeavesEmptyMany", string.Join(", ", projects.Select(project => project.Name))),
    };

    /// <summary>
    /// A removal the dialog is asking about: the default, its name, the server's report and the last refusal at commit.
    /// </summary>
    private sealed record Removal(Guid StatusId, string Name, IReadOnlyList<SpaceStatusOccupancyResponse> Report, SpaceStatusRemovalRefusal? Refusal);
}

/// <summary>
/// What the removal dialog says after a refused commit.
/// </summary>
/// <param name="Heading">The opening line.</param>
/// <param name="Lines">One line per reason.</param>
public sealed record SpaceStatusRemovalRefusal(string Heading, IReadOnlyList<string> Lines);
