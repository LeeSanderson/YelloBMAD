using System.Globalization;
using System.Net;
using System.Text.Json;
using Yello.Client.Refusals;
using Yello.Client.Spaces;
using Yello.Contracts;
using Yello.Contracts.Tasks;

namespace Yello.Client.Components;

/// <summary>
/// The Board's write of a move into another Project, made from Task detail. Not optimistic: the card stays until the server
/// admits the move, because it can be refused for what this tab cannot see - a full destination, a Project deleted, a set
/// changed - and a card that left and came back would say something false.
/// </summary>
/// <remarks>
/// <para>
/// <b>Admitted</b>: the card leaves its column, the panel closes, focus goes to the column that held the Task - as for a
/// deletion, since the card focus would return to no longer exists - and the move is announced politely, with the Status
/// when one was chosen.
/// </para>
/// <para>
/// <b>Refused</b>: nothing moved, the panel stays open and says why, and the picker takes the step the answer calls for.
/// A move that got no answer keeps its <c>Idempotency-Key</c> for the same move sent again, which may have been applied, and
/// never says nothing moved.
/// </para>
/// </remarks>
public sealed partial class BoardView
{
    private readonly Dictionary<Guid, (ProjectMoveChoice Choice, string Key)> _projectMoveKeys = [];

    private async Task<ProjectMoveAnswer> MoveToProjectAsync(Guid taskId, ProjectMoveChoice choice)
    {
        if (!_canManage)
        {
            return ProjectMoveAnswer.Refused;
        }

        _panelRefusal = null;

        var generation = _generation;
        var token = ActiveSpace.Token;
        var title = OpenTask?.Title ?? string.Empty;
        var key = _projectMoveKeys.TryGetValue(taskId, out var kept) && kept.Choice == choice ? kept.Key : Guid.NewGuid().ToString("D");

        _projectMoveKeys[taskId] = (choice, key);

        try
        {
            using var answer = await Writes.SendAsync(HttpMethod.Post, TaskRoutes.ProjectMoveOf(SpaceId, taskId), choice.ToRequest(), key, token);

            if (generation != _generation)
            {
                return ProjectMoveAnswer.Gone;
            }

            var outcome = await UseProjectMoveAnswerAsync(taskId, title, choice, answer);

            StateHasChanged();

            return outcome;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The tab left this Space, or the Session ended: the purge has run.
            return ProjectMoveAnswer.Gone;
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or JsonException)
        {
            if (generation == _generation)
            {
                SayInPanel(taskId, Localizer["TaskEditNotSent"]);
                StateHasChanged();
            }

            return ProjectMoveAnswer.Refused;
        }
    }

    private async Task<ProjectMoveAnswer> UseProjectMoveAnswerAsync(Guid taskId, string title, ProjectMoveChoice choice, SpaceWriteAnswer answer)
    {
        switch (answer.StatusCode)
        {
            case HttpStatusCode.OK:
                _projectMoveKeys.Remove(taskId);
                Departed(taskId);
                Say(choice.StatusName is { } status
                    ? string.Format(CultureInfo.CurrentCulture, Localizer["ProjectMovedInStatus"], title, choice.ProjectName, status)
                    : string.Format(CultureInfo.CurrentCulture, Localizer["ProjectMoved"], title, choice.ProjectName));
                return ProjectMoveAnswer.Moved;

            case >= HttpStatusCode.InternalServerError:
                // The outcome is not known: treated as no answer, so the key is kept.
                SayInPanel(taskId, Localizer["TaskEditNotSent"]);
                return ProjectMoveAnswer.Refused;

            case HttpStatusCode.Unauthorized:
                // The Session has ended and the sign-in page follows.
                return ProjectMoveAnswer.Gone;

            case HttpStatusCode.NotFound:
                // As the panel answers a 404 for any write: it closes, and the page's one refusal surface follows.
                _projectMoveKeys.Remove(taskId);
                LeavePanelOf(taskId);
                await OnRefused.InvokeAsync(answer.Problem);
                return ProjectMoveAnswer.Gone;

            default:
                if (answer.Problem?.Type != ProblemTypes.IdempotencyKeyInFlight)
                {
                    _projectMoveKeys.Remove(taskId);
                }

                return ProjectMoveRefusal(taskId, choice, answer.Problem);
        }
    }

    private ProjectMoveAnswer ProjectMoveRefusal(Guid taskId, ProjectMoveChoice choice, ProblemResponse? problem)
    {
        var type = problem?.Type;

        if (type == ProblemTypes.StatusDestinationRequired)
        {
            // The Status step the picker shows next says it; no words of its own.
            return ProjectMoveAnswer.StatusNeeded;
        }

        if (type == ProblemTypes.BoundReached && problem!.Limit is { } limit)
        {
            SayInPanel(taskId, string.Format(CultureInfo.CurrentCulture, Localizer["ProjectMoveFull"], choice.ProjectName, limit.ToString("N0", CultureInfo.CurrentCulture)));
            return ProjectMoveAnswer.Refused;
        }

        SayInPanel(taskId, RefusalText.Of(Localizer, problem));

        if (type == ProblemTypes.InvalidStatusDestination)
        {
            return ProjectMoveAnswer.StatusInvalid;
        }

        return type == ProblemTypes.InvalidTaskProject ? ProjectMoveAnswer.ProjectInvalid : ProjectMoveAnswer.Refused;
    }

    /// <summary>
    /// Says a refusal in the panel - only while it is open on the Task the write was about.
    /// </summary>
    private void SayInPanel(Guid taskId, string words)
    {
        if (_openTaskId == taskId)
        {
            _panelRefusal = words;
        }
    }
}
