using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Yello.Client.Refusals;
using Yello.Client.Spaces;
using Yello.Contracts;
using Yello.Contracts.Projects;
using Yello.Contracts.Tasks;

namespace Yello.Client.Components;

/// <summary>
/// The bar's write and its state. It owns the request, so neither host adds a dependency for it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not optimistic.</b> Nothing leaves the column until the server has answered, because the move can be refused for what
/// this tab cannot see - a full destination, a Project deleted, a set changed - and the Tasks moved are the ones the server
/// finds in the Status, which can be more than this tab counted. The announcement takes its number from the answer.
/// </para>
/// <para>
/// <b>The commit is guarded by its own flag</b>, set before the first await, so a second choice arriving while the first is
/// out sends nothing; an answer arriving after the bar was closed or the Space left is dropped.
/// </para>
/// </remarks>
public sealed partial class BulkMoveBar
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private ElementReference _scope;
    private bool _focus = true;
    private bool _inFlight;
    private bool _closed;
    private string? _refusal;
    private IDisposable? _purgeRegistration;
    private (ProjectMoveChoice Choice, string Key)? _kept;

    /// <summary>
    /// The Space the Tasks are in.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public Guid SpaceId { get; set; }

    /// <summary>
    /// The Project the Tasks leave, which the picker never offers.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// The Status whose Tasks move.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public Guid StatusId { get; set; }

    /// <summary>
    /// The Status's name.
    /// </summary>
    [Parameter]
    public string StatusName { get; set; } = string.Empty;

    /// <summary>
    /// How many Tasks the host shows in the Status: the scope the person chose, not the number the server moves.
    /// </summary>
    [Parameter]
    public int Count { get; set; }

    /// <summary>
    /// Raised with whether the move is out, so the host blocks the Tasks it is moving and only while it is.
    /// </summary>
    [Parameter]
    public EventCallback<bool> OnBusy { get; set; }

    /// <summary>
    /// Raised with the sentence to announce once the move has landed; the host closes the bar.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnMoved { get; set; }

    /// <summary>
    /// Raised when the bar is closed with nothing sent.
    /// </summary>
    [Parameter]
    public EventCallback OnCancelled { get; set; }

    /// <summary>
    /// Raised with the sentence to announce when the Status is no longer in the Project: nothing moved, and the host reads
    /// again and closes the bar as on a cancel.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnGone { get; set; }

    private string ScopeText => Count == 1
        ? Format("BulkMoveScopeOne", StatusName)
        : Format("BulkMoveScopeMany", Count, StatusName);

    private string InFlightText => Count == 1 ? Localizer["BulkMoveInFlightOne"] : Format("BulkMoveInFlightMany", Count);

    /// <inheritdoc />
    public void Dispose()
    {
        _closed = true;
        _purgeRegistration?.Dispose();
    }

    /// <inheritdoc />
    protected override void OnInitialized() => _purgeRegistration = ActiveSpace.RegisterPurge(Purge);

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Focus is in the bar from the first frame; the picker takes it onto its list once the Projects are read.
        if (_focus && !_inFlight)
        {
            _focus = false;
            await _scope.FocusAsync();
        }
    }

    private void Purge()
    {
        _closed = true;
        _kept = null;
        _refusal = null;
    }

    private Task KeyDownAsync(KeyboardEventArgs args) =>
        args.Key == "Escape" ? CancelAsync() : Task.CompletedTask;

    private Task CancelAsync() =>
        _inFlight || _closed ? Task.CompletedTask : OnCancelled.InvokeAsync();

    private async Task<ProjectMoveAnswer> CommitAsync(ProjectMoveChoice choice)
    {
        if (_inFlight || _closed)
        {
            return ProjectMoveAnswer.Refused;
        }

        _inFlight = true;
        _refusal = null;
        StateHasChanged();

        var token = ActiveSpace.Token;
        var key = _kept is { } kept && kept.Choice == choice ? kept.Key : Guid.NewGuid().ToString("D");

        _kept = (choice, key);

        try
        {
            await OnBusy.InvokeAsync(true);

            using var answer = await Writes.SendAsync(
                HttpMethod.Post,
                ProjectStatusRoutes.TasksToProjectOf(SpaceId, ProjectId, StatusId),
                new MoveStatusTasksToProjectRequest(choice.ProjectId, choice.StatusId),
                key,
                token);

            return _closed ? ProjectMoveAnswer.Gone : await UseAnswerAsync(choice, answer, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The tab left this Space, or the Session ended: the purge has run.
            return ProjectMoveAnswer.Gone;
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or JsonException)
        {
            // No answer: the move may have been applied, so nothing is said to have moved or not, and the key is kept.
            _refusal = Localizer["TaskEditNotSent"];
            return ProjectMoveAnswer.Refused;
        }
        finally
        {
            _inFlight = false;

            if (!_closed)
            {
                // The picker re-renders itself; the bar's own refusal line and Cancel come back only on a render of its own.
                StateHasChanged();
                await OnBusy.InvokeAsync(false);
            }
        }
    }

    private async Task<ProjectMoveAnswer> UseAnswerAsync(ProjectMoveChoice choice, SpaceWriteAnswer answer, CancellationToken token)
    {
        switch (answer.StatusCode)
        {
            case HttpStatusCode.OK:
                var moved = await answer.Response.Content.ReadFromJsonAsync<MoveStatusTasksToProjectResponse>(JsonOptions, token);

                _kept = null;
                _closed = true;
                await OnMoved.InvokeAsync(MovedText(moved?.MovedCount ?? 0, choice));
                return ProjectMoveAnswer.Moved;

            case >= HttpStatusCode.InternalServerError:
                // The outcome is not known: treated as no answer, so the key is kept.
                _refusal = Localizer["TaskEditNotSent"];
                return ProjectMoveAnswer.Refused;

            case HttpStatusCode.Unauthorized:
                // The Session has ended and the sign-in page follows.
                return ProjectMoveAnswer.Gone;

            case HttpStatusCode.NotFound:
                _kept = null;
                _closed = true;
                await OnGone.InvokeAsync(Format("BulkMoveStatusGone", StatusName));
                return ProjectMoveAnswer.Gone;

            default:
                if (answer.Problem?.Type != ProblemTypes.IdempotencyKeyInFlight)
                {
                    _kept = null;
                }

                return Refusal(choice, answer.Problem);
        }
    }

    private ProjectMoveAnswer Refusal(ProjectMoveChoice choice, ProblemResponse? problem)
    {
        var type = problem?.Type;

        if (type == ProblemTypes.StatusDestinationRequired)
        {
            // The Status step the picker shows next says it.
            return ProjectMoveAnswer.StatusNeeded;
        }

        if (type == ProblemTypes.BoundReached && problem!.Limit is { } limit)
        {
            var most = limit.ToString("N0", CultureInfo.CurrentCulture);

            _refusal = Count == 1 ? Format("BulkMoveFullOne", choice.ProjectName, most) : Format("BulkMoveFullMany", choice.ProjectName, Count, most);
            return ProjectMoveAnswer.Refused;
        }

        var reason = RefusalText.Of(Localizer, problem);
        string nothingMoved = Localizer["StatusRemoveRefused"];

        _refusal = reason.EndsWith(nothingMoved, StringComparison.Ordinal) ? reason : reason + " " + nothingMoved;

        if (type == ProblemTypes.InvalidStatusDestination)
        {
            return ProjectMoveAnswer.StatusInvalid;
        }

        return type == ProblemTypes.InvalidTaskProject ? ProjectMoveAnswer.ProjectInvalid : ProjectMoveAnswer.Refused;
    }

    private string MovedText(int count, ProjectMoveChoice choice) => (count == 1, choice.StatusName) switch
    {
        (true, null) => Format("BulkMovedOne", choice.ProjectName),
        (false, null) => Format("BulkMovedMany", count, choice.ProjectName),
        (true, { } status) => Format("BulkMovedInStatusOne", choice.ProjectName, status),
        (false, { } status) => Format("BulkMovedInStatusMany", count, choice.ProjectName, status),
    };

    private string Format(string key, params object[] values) =>
        string.Format(CultureInfo.CurrentCulture, Localizer[key], values);
}
