using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Yello.Client.Refusals;
using Yello.Client.Spaces;
using Yello.Contracts;
using Yello.Contracts.Spaces;

namespace Yello.Client.Components;

/// <summary>
/// The Default Statuses section's writes: add, rename - with the cascade question when Projects renamed the Status
/// themselves - and move.
/// </summary>
/// <remarks>
/// <para>
/// <b>The rename field stays open while the cascade dialog asks</b>, so cancelling the question returns to the field as it
/// was, and answering it sends the same rename again with the one decision.
/// </para>
/// <para>
/// <b>A write that never got an answer reuses its key</b> when it is sent again, so the server replays it rather than
/// meeting its own name as taken.
/// </para>
/// </remarks>
public sealed partial class SpaceDefaultStatusSection
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Answer Unanswered = new(null, null, null);

    // A field name no browser's autofill would match to a saved value: random per page.
    private readonly string _nameFieldName = Guid.NewGuid().ToString("N");

    private IDisposable? _purgeRegistration;

    /// <summary>
    /// Bumped by every purge and by disposal. An answer that started before one is dropped when it arrives.
    /// </summary>
    private int _generation;

    private List<StatusResponse>? _defaults;
    private string? _announcement;
    private int _announcements;
    private string _name = string.Empty;
    private string? _nameMessage;
    private string? _refusal;
    private int _attempts;
    private bool _busy;
    private (string Signature, string Key)? _unanswered;
    private Guid? _renaming;
    private Guid? _focusId;
    private int _focusRequests;
    private SpaceStatusFocus _focusTarget;
    private Cascade? _cascade;

    /// <summary>
    /// The Space whose defaults these are.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public Guid SpaceId { get; set; }

    /// <summary>
    /// Raised for a <c>404</c> the Space itself answers, so the page renders its one refusal surface.
    /// </summary>
    [Parameter]
    public EventCallback<ProblemResponse?> OnSpaceRefused { get; set; }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        _purgeRegistration = ActiveSpace.RegisterPurge(Purge);

        await LoadAsync(_generation);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _generation++;
        _purgeRegistration?.Dispose();
    }

    /// <summary>
    /// The Space is being left: its defaults, and everything half-typed about them, go synchronously.
    /// </summary>
    private void Purge()
    {
        _generation++;
        _defaults = null;
        _announcement = null;
        _announcements++;
        _name = string.Empty;
        _nameMessage = null;
        _refusal = null;
        _busy = false;
        _unanswered = null;
        _renaming = null;
        _focusId = null;
        _cascade = null;
    }

    /// <returns><c>true</c> when the defaults answered.</returns>
    private async Task<bool> LoadAsync(int generation)
    {
        var token = ActiveSpace.Token;

        try
        {
            using var response = await Http.GetAsync(new Uri(SpaceRoutes.StatusesOf(SpaceId), UriKind.Relative), token);

            if (generation != _generation || token.IsCancellationRequested)
            {
                return false;
            }

            if (response.IsSuccessStatusCode)
            {
                _defaults = await response.Content.ReadFromJsonAsync<List<StatusResponse>>(JsonOptions, token);

                return _defaults is not null;
            }

            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                await OnSpaceRefused.InvokeAsync(await SpaceWrites.ProblemOfAsync(response, token));
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The tab left this Space, or the Session ended: both purges have run.
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or JsonException)
        {
            if (generation == _generation)
            {
                await OnSpaceRefused.InvokeAsync(null);
            }
        }

        return false;
    }

    private async Task AddAsync()
    {
        _nameMessage = null;

        var name = _name.Trim();

        if (!StatusNameRule.IsStatusName(name))
        {
            _nameMessage = Localizer["StatusNameInvalid"];
            return;
        }

        var answer = await WriteAsync(HttpMethod.Post, SpaceRoutes.StatusesOf(SpaceId), new AddSpaceStatusRequest(name, null));

        if (answer.Defaults is not null)
        {
            _name = string.Empty;
            Say(Format("SpaceStatusAdded", name));
            return;
        }

        if (NameRefusal(answer.Problem) is { } words)
        {
            _nameMessage = words;
            return;
        }

        await RefuseAsync(answer);
    }

    private void StartRename(Guid statusId)
    {
        _renaming = statusId;
        _refusal = null;
    }

    private void CancelRename()
    {
        if (_renaming is { } statusId)
        {
            _renaming = null;
            FocusRow(statusId, SpaceStatusFocus.Rename);
        }
    }

    private async Task RenameAsync((Guid StatusId, string Name) rename)
    {
        var name = rename.Name.Trim();

        if (_busy || _defaults?.Find(status => status.Id == rename.StatusId) is not { } status)
        {
            return;
        }

        if (!StatusNameRule.IsStatusName(name))
        {
            _renaming = null;
            Refuse(Localizer["StatusNameInvalid"]);
            return;
        }

        if (string.Equals(name, status.Name, StringComparison.Ordinal))
        {
            CancelRename();
            return;
        }

        await SendRenameAsync(new Cascade(status.Id, status.Name, name, []), replaceProjectNames: null);
    }

    private Task DecideAsync(bool replaceProjectNames) =>
        _cascade is { } cascade ? SendRenameAsync(cascade, replaceProjectNames) : Task.CompletedTask;

    /// <summary>
    /// Cancelling the question renames nothing anywhere; focus goes back to the rename field, which is still open.
    /// </summary>
    private void CancelCascade()
    {
        if (_cascade is { } cascade)
        {
            _cascade = null;
            FocusRow(cascade.StatusId, SpaceStatusFocus.Rename);
        }
    }

    private async Task SendRenameAsync(Cascade rename, bool? replaceProjectNames)
    {
        var answer = await WriteAsync(HttpMethod.Patch, SpaceRoutes.StatusOf(SpaceId, rename.StatusId), new RenameSpaceStatusRequest(rename.NewName, replaceProjectNames));

        if (answer.Problem?.Type == ProblemTypes.StatusRenameConflict && answer.Problem.Conflicts is { Count: > 0 } conflicts)
        {
            _cascade = rename with { Conflicts = conflicts };
            return;
        }

        if (answer.Status is null)
        {
            return;
        }

        _cascade = null;
        _renaming = null;
        FocusRow(rename.StatusId, SpaceStatusFocus.Rename);

        if (answer.Defaults is null)
        {
            await RefuseAsync(answer);
            return;
        }

        var kept = replaceProjectNames == false ? rename.Conflicts.Count : 0;

        Say(kept switch
        {
            0 => Format("SpaceStatusRenamedEverywhere", rename.OldName, rename.NewName),
            1 => Format("SpaceStatusRenamedKeptOne", rename.OldName, rename.NewName),
            _ => Format("SpaceStatusRenamedKeptMany", rename.OldName, rename.NewName, kept),
        });
    }

    /// <summary>
    /// Moves one default a place up or down: a place after the default that will be above it, or first.
    /// </summary>
    private async Task MoveAsync((Guid StatusId, int Step) move)
    {
        if (AnchorFor(move) is not { } anchor)
        {
            return;
        }

        var answer = await WriteAsync(HttpMethod.Post, SpaceRoutes.StatusPlaceOf(SpaceId, move.StatusId), new PlaceSpaceStatusRequest(anchor.After));
        var position = answer.Defaults?.FindIndex(status => status.Id == move.StatusId) ?? -1;

        if (answer.Defaults is not { } placed || position < 0)
        {
            await RefuseAsync(answer);
            return;
        }

        FocusRow(move.StatusId, move.Step < 0 ? SpaceStatusFocus.Up : SpaceStatusFocus.Down);
        Say(Format("StatusMoved", placed[position].Name, position + 1, placed.Count));
    }

    /// <summary>
    /// The place a move asks for: after the default that will sit above the moved one, or <c>null</c> for first. <c>null</c>
    /// as a whole when there is nowhere to go.
    /// </summary>
    private (Guid? After, int Target)? AnchorFor((Guid StatusId, int Step) move)
    {
        var index = _defaults?.FindIndex(status => status.Id == move.StatusId) ?? -1;
        var target = index + move.Step;

        if (_defaults is not { } defaults || index < 0 || target < 0 || target >= defaults.Count)
        {
            return null;
        }

        if (move.Step > 0)
        {
            return (defaults[target].Id, target);
        }

        return (target == 0 ? null : defaults[target - 1].Id, target);
    }

    /// <summary>
    /// Sends one write and re-renders from its answer, the Space's whole default list. A write while another is out is
    /// ignored, as is an answer that arrives after the Space was left.
    /// </summary>
    private async Task<Answer> WriteAsync(HttpMethod method, string path, object body)
    {
        if (_busy || _defaults is null)
        {
            return Unanswered;
        }

        _busy = true;
        _refusal = null;

        var generation = _generation;
        var token = ActiveSpace.Token;
        var key = KeyFor(method, path, body);

        try
        {
            using var answer = await Writes.SendAsync(method, path, body, key, token);

            if (answer.Problem?.Type != ProblemTypes.IdempotencyKeyInFlight)
            {
                _unanswered = null;
            }

            return await UseAsync(answer, generation, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The tab left this Space, or the Session ended, while the answer was on its way.
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or JsonException)
        {
            Refuse(Localizer["TaskEditNotSent"]);
        }
        finally
        {
            _busy = false;
        }

        return Unanswered;
    }

    /// <summary>
    /// An answer to a write: the defaults it carries are rendered, unless the Space was left while it was on its way.
    /// </summary>
    private async Task<Answer> UseAsync(SpaceWriteAnswer answer, int generation, CancellationToken token)
    {
        if (generation != _generation)
        {
            return Unanswered;
        }

        if (!answer.Response.IsSuccessStatusCode)
        {
            return new Answer(answer.StatusCode, null, answer.Problem);
        }

        var defaults = await answer.Response.Content.ReadFromJsonAsync<List<StatusResponse>>(JsonOptions, token);

        if (defaults is null || generation != _generation)
        {
            return Unanswered;
        }

        _defaults = defaults;

        return new Answer(answer.StatusCode, defaults, null);
    }

    private string KeyFor(HttpMethod method, string path, object body)
    {
        var signature = string.Join('|', method.Method, path, JsonSerializer.Serialize(body, body.GetType(), JsonOptions));
        var key = _unanswered is { } unanswered && unanswered.Signature == signature ? unanswered.Key : Guid.NewGuid().ToString("D");

        _unanswered = (signature, key);

        return key;
    }

    /// <summary>
    /// A refused write, said inline. A <c>404</c> is read again first: the Space itself refused raises the page's refusal
    /// surface, and a Status no longer among the defaults is said here. A 401 is YelloApiHandler's.
    /// </summary>
    private async Task RefuseAsync(Answer answer)
    {
        switch (answer.Status)
        {
            case null or HttpStatusCode.Unauthorized:
                return;

            case HttpStatusCode.NotFound:
                if (await LoadAsync(_generation))
                {
                    Refuse(Localizer["SpaceStatusNoLongerHere"]);
                }

                return;

            default:
                Refuse(NameRefusal(answer.Problem) ?? RefusalText.Of(Localizer, answer.Problem));
                return;
        }
    }

    /// <summary>
    /// The words for a refused name: another default holds it, Projects already use it - named - or it is not a name.
    /// </summary>
    private string? NameRefusal(ProblemResponse? problem)
    {
        if (problem?.Type == ProblemTypes.InvalidStatusName)
        {
            return Localizer["StatusNameInvalid"];
        }

        if (problem?.Type != ProblemTypes.StatusNameTaken)
        {
            return null;
        }

        return problem.Projects switch
        {
            null or { Count: 0 } => Localizer["SpaceStatusNameTaken"],
            [var only] => Format("SpaceStatusNameTakenInProjectOne", only.Name),
            var projects => Format("SpaceStatusNameTakenInProjectsMany", string.Join(", ", projects.Select(project => project.Name))),
        };
    }

    private void Refuse(string words)
    {
        _refusal = words;
        _attempts++;
    }

    private void FocusRow(Guid statusId, SpaceStatusFocus target)
    {
        _focusId = statusId;
        _focusTarget = target;
        _focusRequests++;
    }

    private static IEnumerable<(StatusResponse Status, int Index)> Indexed(List<StatusResponse> defaults) =>
        defaults.Select((status, index) => (status, index));

    private string Format(string key, params object[] values) =>
        string.Format(CultureInfo.CurrentCulture, Localizer[key], values);

    /// <summary>
    /// Says a sentence politely - the same one again included.
    /// </summary>
    private void Say(string words)
    {
        _announcement = words;
        _announcements++;
    }

    /// <summary>
    /// One answer to a write: its status (<c>null</c> when there was none to use), the defaults it carries, or its problem.
    /// </summary>
    private sealed record Answer(HttpStatusCode? Status, List<StatusResponse>? Defaults, ProblemResponse? Problem);

    /// <summary>
    /// A rename being sent, and the Projects the server said renamed the Status themselves.
    /// </summary>
    private sealed record Cascade(Guid StatusId, string OldName, string NewName, IReadOnlyList<StatusRenameConflictResponse> Conflicts);
}

/// <summary>
/// Which control of a <see cref="SpaceStatusRow"/> a focus request lands on.
/// </summary>
public enum SpaceStatusFocus
{
    /// <summary>
    /// The row's rename control, or its rename field while it is open.
    /// </summary>
    Rename,

    /// <summary>
    /// The row's up control, or its down control once the row is first.
    /// </summary>
    Up,

    /// <summary>
    /// The row's down control, or its up control once the row is last.
    /// </summary>
    Down,
}
