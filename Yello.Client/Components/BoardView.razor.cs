using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Yello.Client.Refusals;
using Yello.Client.Spaces;
using Yello.Contracts;
using Yello.Contracts.Labels;
using Yello.Contracts.Tasks;

namespace Yello.Client.Components;

/// <summary>
/// The Board's Task detail panel and its replica: the product's first optimistic writes. Story 2.3; AC1,
/// AC12, AC14; AD-11, AD-18; open questions 1 (a), 6 (a) and 8. Story 2.4 adds the Labels as a fourth attribute
/// (AC7): the same replica, queue, key and per-attribute revert, the whole set sent as <c>labelIds</c>. Story 2.6 adds the
/// move (AC15): a Status and a position, sent as the Status attribute's change on the move route.
/// </summary>
/// <remarks>
/// <para>
/// <b>The client edits a replica and never blocks</b> (AD-11). A change renders at once on the card and in
/// the panel - a Status change moves the card to the end of its new column and moves both totals - and the
/// server admits or refuses it afterwards. The client is never the arbiter: a refusal reverts.
/// </para>
/// <para>
/// <b>Each attribute keeps its last admitted value</b> (open question 8). A refusal reverts that attribute
/// alone to it - not to whatever the panel showed before, when an edit of another attribute is outstanding or
/// was admitted meanwhile. Edits of one attribute of one Task go one at a time: a second change while the
/// first is out waits, and only the latest waiting value is sent. Edits of different attributes go at once,
/// each carrying only its own member (merge patch, open question 1 (a)).
/// </para>
/// <para>
/// <b>Every edit carries an <c>Idempotency-Key</c></b> (AD-18), and an edit that got no answer keeps its key:
/// the same change sent again reuses it, because the first may have been applied. No answer reverts and says
/// so, in the client's own words - a later Board load shows what the server holds.
/// </para>
/// <para>
/// <b>A <c>404</c> is the Task, or the Space, being gone</b>: the change reverts, the panel closes, and the
/// page renders its one refusal surface. Every other refusal is said inline in the panel, in the client's own
/// words for its type (<see cref="RefusalText"/>), never as a toast and never in the server's prose.
/// </para>
/// <para>
/// <b>A move is the Status attribute's change</b> (story 2.6, open question 7): the Status and the position travel
/// together, so a move and a Status edit of one Task queue one behind the other rather than race. It renders at once -
/// the card leaves its column for the slot after its chosen neighbour, both totals with it - and is sent as
/// <c>POST …/move</c> naming the neighbour, never a key. On admission the card is re-placed by the server's key; a refusal
/// or no answer puts it back in its last admitted slot. Either way the Board says so politely once the answer is in -
/// where the Task is now, with its ordinal and the column's true total, or the slot it returned to (open question 8) - a
/// Status edit included, which lands last. A move made from a card's menu says a refusal on the Board, where it was made.
/// </para>
/// </remarks>
public sealed partial class BoardView
{
    private readonly Dictionary<Guid, TaskReplica> _replicas = [];

    private List<TaskStatusOption> _statusOptions = [];

    // The Space's Labels, in the server's order, and by id for the cards (story 2.4). Purged with the Board.
    private IReadOnlyList<LabelResponse> _labels = [];
    private IReadOnlyDictionary<Guid, LabelResponse> _labelsById = new Dictionary<Guid, LabelResponse>();
    private Guid? _openTaskId;
    private string? _panelRefusal;

    // Story 2.5: the deletion's own refusal, said in the confirm, kept apart from an edit's (review patch).
    private string? _deleteRefusal;
    private Guid? _focusCard;
    private int _focusCardRequests;

    // Story 2.5: the Task whose deletion is with the server; the key of each deletion that got no answer, reused by the
    // identical resend (2.3 open question 8); the Tasks this tab has deleted, whose late edit answers are dropped (open
    // question 7); and the column focus returns to once a card it held is gone (open question 3).
    private readonly Dictionary<Guid, string> _deleteKeys = [];
    private readonly HashSet<Guid> _deleted = [];
    private Guid? _deleting;
    private Guid? _focusColumn;
    private int _focusColumnRequests;

    // Story 2.6: the card whose menu (or the Move picker it led to) is open; the focus request for the card's actions
    // control; the card a menu move is drawing afresh, which takes focus once; the card whose reflow is animated; the Tasks
    // whose outstanding move came from a menu; and a refusal said on the Board for one of those.
    private Guid? _menuTaskId;
    private bool _menuPicking;
    private Guid? _focusActions;
    private int _focusActionsRequests;
    private Guid? _arrivalCard;
    private Guid? _movedCard;
    private readonly HashSet<Guid> _movesFromMenu = [];
    private string? _boardNotice;

    /// <summary>
    /// The four attributes a Task edit can change.
    /// </summary>
    private enum TaskAttribute
    {
        Title,
        Status,
        DueDate,
        Labels,
    }

    /// <summary>
    /// How one sent edit ended.
    /// </summary>
    private enum EditOutcome
    {
        Admitted,
        Refused,
        Unanswered,
        Gone,
    }

    /// <summary>
    /// The open panel's Task, as the replica shows it now; <c>null</c> when no panel is open or its card is gone.
    /// </summary>
    private TaskResponse? OpenTask => _openTaskId is { } id && Locate(id) is { } found ? found.Column.Tasks[found.Index] : null;

    /// <summary>
    /// Opens the panel on a card, replacing any panel already open - never a second one.
    /// </summary>
    private void Open(Guid taskId)
    {
        _menuTaskId = null;
        _menuPicking = false;

        if (_openTaskId == taskId)
        {
            return;
        }

        _openTaskId = taskId;
        _panelRefusal = null;
        _deleteRefusal = null;
        _focusCard = null;
    }

    /// <summary>
    /// Closes the panel and returns focus to the card that opened it.
    /// </summary>
    private void Close()
    {
        if (_openTaskId is { } taskId)
        {
            _focusCard = taskId;
            _focusCardRequests++;
        }

        _openTaskId = null;
        _panelRefusal = null;
        _deleteRefusal = null;
    }

    /// <summary>
    /// The panel's state goes with the Board's purge: nothing about the Space's Tasks outlives it.
    /// </summary>
    private void PurgePanel()
    {
        _replicas.Clear();
        _labels = [];
        _labelsById = new Dictionary<Guid, LabelResponse>();
        _statusOptions = [];
        _openTaskId = null;
        _panelRefusal = null;
        _deleteRefusal = null;
        _focusCard = null;
        _deleteKeys.Clear();
        _deleted.Clear();
        _deleting = null;
        _focusColumn = null;
        _menuTaskId = null;
        _menuPicking = false;
        _focusActions = null;
        _arrivalCard = null;
        _movedCard = null;
        _movesFromMenu.Clear();
        _boardNotice = null;
    }

    /// <summary>
    /// The Board's columns as the Move picker offers them: each Status and the Tasks it holds now, in order.
    /// </summary>
    private List<MoveColumn> MoveColumns => [.. (_columns ?? []).Select(column => new MoveColumn(column.StatusId, column.Name, column.Tasks))];

    /// <summary>
    /// Opens a card's menu (story 2.6, open question 6 (a)) - replacing any other card's; it takes focus itself.
    /// </summary>
    private void OpenMenu(Guid taskId)
    {
        if (!_canManage)
        {
            return;
        }

        _menuTaskId = taskId;
        _menuPicking = false;
        _boardNotice = null;
    }

    /// <summary>
    /// Esc on the menu, or on the picker's first step: nothing is moved, and focus returns to the card's actions control.
    /// </summary>
    private void CloseMenu()
    {
        if (_menuTaskId is { } taskId)
        {
            _focusActions = taskId;
            _focusActionsRequests++;
        }

        _menuTaskId = null;
        _menuPicking = false;
    }

    /// <summary>
    /// The menu's "Open": Task detail, exactly as the card's title opens it.
    /// </summary>
    private void OpenFromMenu()
    {
        if (_menuTaskId is { } taskId)
        {
            Open(taskId);
        }
    }

    /// <summary>
    /// The menu's "Move": the picker takes the menu's place in the card.
    /// </summary>
    private void PickFromMenu() => _menuPicking = true;

    /// <summary>
    /// A position chosen in a card's picker: the menu closes and the move is made from it.
    /// </summary>
    private Task MoveFromMenuAsync(MoveChoice choice)
    {
        if (_menuTaskId is not { } taskId)
        {
            return Task.CompletedTask;
        }

        _menuTaskId = null;
        _menuPicking = false;

        return MoveAsync(taskId, choice, fromMenu: true);
    }

    /// <summary>
    /// A move (story 2.6, AC15): rendered at once, then sent as the Status attribute's change. Made from a card's menu, focus
    /// follows the card to its new place (open question 9); from the panel, it stays on the panel's Move control.
    /// </summary>
    private Task MoveAsync(Guid taskId, MoveChoice choice, bool fromMenu)
    {
        if (!_canManage)
        {
            return Task.CompletedTask;
        }

        _boardNotice = null;
        _movedCard = taskId;

        if (fromMenu)
        {
            _movesFromMenu.Add(taskId);
            FocusCardTitle(taskId);
        }
        else
        {
            _movesFromMenu.Remove(taskId);
        }

        return EditAsync(taskId, TaskAttribute.Status, choice);
    }

    /// <summary>
    /// Focus to a card's title: a card still in its column takes the bumped request, and a card drawn afresh in another
    /// column takes it on arrival, once.
    /// </summary>
    private void FocusCardTitle(Guid taskId)
    {
        _focusCard = taskId;
        _focusCardRequests++;
        _arrivalCard = taskId;
    }

    /// <summary>
    /// Says, once the answer to a Status attribute's change is in, where the Task is now: on admission its ordinal and the
    /// column's true total; on a refusal or no answer the slot it returned to (open question 8). A refused menu move puts
    /// focus back on the card in that slot.
    /// </summary>
    private void AnnounceMove(Guid taskId, EditOutcome outcome)
    {
        var fromMenu = _movesFromMenu.Remove(taskId);

        if (outcome == EditOutcome.Gone || Locate(taskId) is not { } found)
        {
            return;
        }

        var (column, index) = found;

        _announcement = outcome == EditOutcome.Admitted
            ? string.Format(CultureInfo.CurrentCulture, Localizer["TaskMoved"], column.Name, index + 1, column.Total)
            : string.Format(CultureInfo.CurrentCulture, Localizer["TaskReturned"], column.Name, index + 1);

        if (outcome != EditOutcome.Admitted && fromMenu)
        {
            FocusCardTitle(taskId);
        }
    }

    /// <summary>
    /// The confirm opened or was cancelled: the deletion's words go either way; an edit's words go when the confirm opens
    /// over the content they were about, and one that answers while the confirm is open is shown when the content returns.
    /// </summary>
    private void ConfirmToggled(bool confirming)
    {
        _deleteRefusal = null;

        if (confirming)
        {
            _panelRefusal = null;
        }
    }

    /// <summary>
    /// The confirm's danger action (story 2.5, AC11). Not optimistic (UX-DR26): the card and the panel stay while the
    /// server is asked, and only its <c>204</c> removes the Task - from its column, whose true total drops by one - closes
    /// the panel, announces it politely and moves focus to the column that held it, never the page body or a card.
    /// </summary>
    private async Task DeleteTaskAsync(Guid taskId)
    {
        if (!_canManage || _deleting is not null)
        {
            return;
        }

        _deleteRefusal = null;
        _deleting = taskId;

        var generation = _generation;
        var token = ActiveSpace.Token;

        // A deletion sent again after an attempt that got no answer reuses its key: it may have been applied.
        if (!_deleteKeys.TryGetValue(taskId, out var key))
        {
            key = Guid.NewGuid().ToString("D");
            _deleteKeys[taskId] = key;
        }

        try
        {
            using var answer = await Writes.SendWithoutBodyAsync(HttpMethod.Delete, TaskRoutes.TaskOf(SpaceId, taskId), key, token);

            if (generation == _generation)
            {
                await UseDeletionAnswerAsync(taskId, answer);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The tab left this Space, or the Session ended: the purge has run.
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or JsonException)
        {
            // No answer: nothing is removed, the key is kept for the resend, and the confirm says so.
            if (generation == _generation)
            {
                SayInConfirm(taskId, Localizer["TaskEditNotSent"]);
            }
        }
        finally
        {
            if (generation == _generation)
            {
                _deleting = null;
            }
        }
    }

    private async Task UseDeletionAnswerAsync(Guid taskId, SpaceWriteAnswer answer)
    {
        switch (answer.StatusCode)
        {
            case HttpStatusCode.NoContent:
                Deleted(taskId);
                return;

            case >= HttpStatusCode.InternalServerError:
                // The server failed and its outcome is not known: treated as no answer, so the key is kept.
                SayInConfirm(taskId, Localizer["TaskEditNotSent"]);
                return;

            case HttpStatusCode.Unauthorized:
                // YelloApiHandler's: the Session has ended and the sign-in page follows.
                return;

            case HttpStatusCode.NotFound:
                // The Board's existing pattern for a 404: the panel closes and the page's one refusal surface follows -
                // the indistinguishable not-found (deferred-work.md:530, owner 4.4).
                _deleteKeys.Remove(taskId);
                LeavePanelOf(taskId);
                await OnRefused.InvokeAsync(answer.Problem);
                return;

            default:
                if (answer.Problem?.Type != ProblemTypes.IdempotencyKeyInFlight)
                {
                    _deleteKeys.Remove(taskId);
                }

                SayInConfirm(taskId, RefusalText.Of(Localizer, answer.Problem));
                return;
        }
    }

    /// <summary>
    /// Closes the panel only when it is still open on the Task the deletion was about - never another Task's panel opened
    /// since (review patch).
    /// </summary>
    private void LeavePanelOf(Guid taskId)
    {
        if (_openTaskId == taskId)
        {
            _openTaskId = null;
            _panelRefusal = null;
            _deleteRefusal = null;
        }
    }

    /// <summary>
    /// Says the deletion's refusal in its confirm - only when the panel is open on the Task it is about.
    /// </summary>
    private void SayInConfirm(Guid taskId, string words)
    {
        if (_openTaskId == taskId)
        {
            _deleteRefusal = words;
        }
    }

    /// <summary>
    /// The server deleted the Task: its replica, key and queued edits go, its card leaves its column, the panel closes
    /// without returning focus to the card that is gone, and focus moves to the column that held it.
    /// </summary>
    private void Deleted(Guid taskId)
    {
        _deleteKeys.Remove(taskId);
        _deleted.Add(taskId);
        _replicas.Remove(taskId);

        // Focus moves to the column only from the panel the deletion was confirmed in; another panel keeps its focus.
        var fromItsPanel = _openTaskId == taskId;

        LeavePanelOf(taskId);

        if (_focusCard == taskId)
        {
            _focusCard = null;
        }

        if (Remove(taskId) is { } column && fromItsPanel)
        {
            _focusColumn = column.StatusId;
            _focusColumnRequests++;
        }

        _announcement = Localizer["TaskDeleted"];
    }

    private void UseStatuses(IEnumerable<TaskStatusOption> statuses) => _statusOptions = [.. statuses];

    private void UseLabels(IReadOnlyList<LabelResponse> labels)
    {
        _labels = labels;
        _labelsById = labels.ToDictionary(label => label.Id);
    }

    /// <summary>
    /// One change from the panel: rendered at once, then sent - or queued behind an edit of the same attribute
    /// that is still out.
    /// </summary>
    private async Task EditAsync(Guid taskId, TaskAttribute attribute, object? value)
    {
        if (!_canManage || ReplicaOf(taskId) is not { } replica)
        {
            return;
        }

        _panelRefusal = null;

        if (replica.InFlight.ContainsKey(attribute))
        {
            replica.Queued[attribute] = value;
            Place(taskId, replica);
            return;
        }

        replica.InFlight[attribute] = value;
        Place(taskId, replica);

        await SendInTurnAsync(taskId, replica, attribute, value);
    }

    /// <summary>
    /// Sends one attribute's change, then the latest change that waited behind it, until none is waiting -
    /// so one tab never has two writes racing for one field.
    /// </summary>
    private async Task SendInTurnAsync(Guid taskId, TaskReplica replica, TaskAttribute attribute, object? value)
    {
        var generation = _generation;
        var sending = value;
        var more = true;
        var outcome = EditOutcome.Gone;

        while (more)
        {
            outcome = await SendAsync(taskId, replica, attribute, sending, generation);

            more = outcome == EditOutcome.Admitted
                && replica.Queued.Remove(attribute, out sending)
                && !SameValue(sending, ValueOf(replica.Admitted, attribute));

            if (more)
            {
                replica.InFlight[attribute] = sending;
            }
        }

        if (generation == _generation)
        {
            Place(taskId, replica);

            if (attribute == TaskAttribute.Status)
            {
                AnnounceMove(taskId, outcome);
            }
        }
    }

    private async Task<EditOutcome> SendAsync(Guid taskId, TaskReplica replica, TaskAttribute attribute, object? value, int generation)
    {
        var token = ActiveSpace.Token;
        var body = BodyOf(attribute, value);
        var text = JsonSerializer.Serialize(body, JsonOptions);

        // A change sent again after an attempt that got no answer reuses that attempt's key: it may have been
        // applied (2.1 review, R3). Any other change takes a fresh one.
        var key = replica.Unanswered.TryGetValue(attribute, out var unanswered) && string.Equals(unanswered.Body, text, StringComparison.Ordinal)
            ? unanswered.Key
            : Guid.NewGuid().ToString("D");

        replica.Unanswered[attribute] = (text, key);

        try
        {
            // A move goes to its own route (story 2.6); every other change is a merge patch of the Task.
            using var answer = value is MoveChoice
                ? await Writes.SendAsync(HttpMethod.Post, TaskRoutes.MoveOf(SpaceId, taskId), body, key, token)
                : await Writes.SendAsync(HttpMethod.Patch, TaskRoutes.TaskOf(SpaceId, taskId), body, key, token);

            // A Task this tab deleted while the edit was out: its answer (a 404) is dropped - no refusal surface, no
            // revert, no words (story 2.5, open question 7).
            return generation != _generation || _deleted.Contains(taskId)
                ? EditOutcome.Gone
                : await SettleAsync(taskId, replica, attribute, answer, generation, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The tab left this Space, or the Session ended: the purge has run.
            return EditOutcome.Gone;
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or JsonException)
        {
            // No answer, or one that could not be read. The key is kept for a resend; the change reverts, and
            // the person is told it was not sent - unless this tab has deleted the Task meanwhile.
            return _deleted.Contains(taskId) ? EditOutcome.Gone : NotSent(taskId, replica, attribute, generation);
        }
    }

    /// <summary>
    /// Settles one sent edit from its answer. The attribute stays in flight until the answer has been read and
    /// applied, so a change of the same attribute made meanwhile still waits behind it.
    /// </summary>
    private async Task<EditOutcome> SettleAsync(Guid taskId, TaskReplica replica, TaskAttribute attribute, SpaceWriteAnswer answer, int generation, CancellationToken token)
    {
        if (answer.StatusCode >= HttpStatusCode.InternalServerError)
        {
            // The server failed and its outcome is not known: treated as no answer, so the key is kept.
            return NotSent(taskId, replica, attribute, generation);
        }

        var outcome = await UseAnswerAsync(taskId, replica, attribute, answer, generation, token);

        if (outcome is EditOutcome.Admitted or EditOutcome.Refused)
        {
            replica.InFlight.Remove(attribute);

            if (answer.Problem?.Type != ProblemTypes.IdempotencyKeyInFlight)
            {
                replica.Unanswered.Remove(attribute);
            }
        }

        return outcome;
    }

    private async Task<EditOutcome> UseAnswerAsync(Guid taskId, TaskReplica replica, TaskAttribute attribute, SpaceWriteAnswer answer, int generation, CancellationToken token)
    {
        switch (answer.StatusCode)
        {
            case HttpStatusCode.OK:
                var stored = await answer.Response.Content.ReadFromJsonAsync<TaskResponse>(JsonOptions, token);

                if (generation != _generation)
                {
                    return EditOutcome.Gone;
                }

                if (stored is null)
                {
                    return NotSent(taskId, replica, attribute, generation);
                }

                replica.Admitted = Admit(replica.Admitted, attribute, stored);
                return EditOutcome.Admitted;

            case HttpStatusCode.Unauthorized:
                // YelloApiHandler's: the Session has ended and the sign-in page follows.
                replica.Queued.Remove(attribute);
                return EditOutcome.Refused;

            case HttpStatusCode.NotFound:
                // Reverted, then the page's one refusal surface: the panel does not stay open over a Task
                // that is not there.
                replica.Queued.Remove(attribute);

                if (_openTaskId == taskId)
                {
                    _openTaskId = null;
                    _panelRefusal = null;
                }

                await OnRefused.InvokeAsync(answer.Problem);
                return EditOutcome.Refused;

            default:
                replica.Queued.Remove(attribute);
                SayInPanel(taskId, RefusalText.Of(Localizer, answer.Problem));

                if (answer.Problem?.Type == ProblemTypes.InvalidTaskLabels)
                {
                    await RefreshLabelsAsync(generation, token);
                }

                return EditOutcome.Refused;
        }
    }

    /// <summary>
    /// After <c>invalid-task-labels</c> the Space's Label list is read again: a Label in it has been deleted since the
    /// Board loaded, and the picker must stop offering it. A read that fails leaves the list as it was - the refusal
    /// has already been said, and the next Board load reads the list anyway.
    /// </summary>
    private async Task RefreshLabelsAsync(int generation, CancellationToken token)
    {
        try
        {
            using var response = await Http.GetAsync(new Uri(LabelRoutes.LabelsOf(SpaceId), UriKind.Relative), token);

            if (!response.IsSuccessStatusCode || generation != _generation || token.IsCancellationRequested)
            {
                return;
            }

            if (await response.Content.ReadFromJsonAsync<List<LabelResponse>>(JsonOptions, token) is { } labels && generation == _generation)
            {
                UseLabels(labels);
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or JsonException)
        {
            // Left as it was; see above.
        }
    }

    private EditOutcome NotSent(Guid taskId, TaskReplica replica, TaskAttribute attribute, int generation)
    {
        if (generation != _generation)
        {
            return EditOutcome.Gone;
        }

        replica.InFlight.Remove(attribute);
        replica.Queued.Remove(attribute);
        SayInPanel(taskId, Localizer["TaskEditNotSent"]);

        return EditOutcome.Unanswered;
    }

    /// <summary>
    /// Says a refusal in the panel - only when the panel is open on the Task it is about, never in another
    /// Task's panel opened since the edit was sent. A move made from a card's menu says it on the Board instead, where it
    /// was made (story 2.6, open question 8 (c)).
    /// </summary>
    private void SayInPanel(Guid taskId, string words)
    {
        if (_openTaskId == taskId)
        {
            _panelRefusal = words;
        }
        else if (_movesFromMenu.Contains(taskId))
        {
            _boardNotice = words;
        }
        else
        {
            // Said nowhere: the panel it was about has closed, and it was not made from a menu.
        }
    }

    private TaskReplica? ReplicaOf(Guid taskId)
    {
        if (_replicas.TryGetValue(taskId, out var replica))
        {
            return replica;
        }

        if (Locate(taskId) is not { } found)
        {
            return null;
        }

        // A card with nothing outstanding shows what the server last answered.
        replica = new TaskReplica(found.Column.Tasks[found.Index]);
        _replicas[taskId] = replica;

        return replica;
    }

    /// <summary>
    /// Puts the card where the replica says it is now: in place for a title or due date, at the end of its new
    /// column for an outstanding Status change, and back in its place by key when a Status change is reverted.
    /// Both columns' totals move with it.
    /// </summary>
    private void Place(Guid taskId, TaskReplica replica)
    {
        if (Locate(taskId) is not { } found || _columns is not { } columns)
        {
            return;
        }

        var displayed = replica.Displayed();
        var (column, index) = found;

        if (replica.OutstandingMove() is { } move)
        {
            PlaceAfter(found, displayed, move, columns);
            replica.Relocated = true;
            return;
        }

        if (replica.Relocated && !replica.HasOutstanding(TaskAttribute.Status))
        {
            replica.Relocated = false;
            Relocate(found, displayed, columns);
            return;
        }

        if (column.StatusId == displayed.StatusId)
        {
            if (string.Equals(column.Tasks[index].PositionKey, displayed.PositionKey, StringComparison.Ordinal))
            {
                column.Tasks[index] = displayed;
                return;
            }

            // An admitted Status change gave the card a new key in the column it already shows in (moved away
            // and back while the first move was out): it goes where its new key puts it.
            column.Tasks.RemoveAt(index);
            InsertByKey(column, displayed);
            return;
        }

        if (columns.Find(candidate => candidate.StatusId == displayed.StatusId) is not { } destination)
        {
            return;
        }

        column.Tasks.RemoveAt(index);
        column.Total--;
        destination.Total++;

        if (displayed.StatusId == replica.Admitted.StatusId)
        {
            InsertByKey(destination, displayed);
        }
        else
        {
            destination.Tasks.Add(displayed);
        }
    }

    /// <summary>
    /// An outstanding move's slot (story 2.6): out of its column and in after its chosen neighbour - or at the top - with
    /// both totals moved when the column changes. A neighbour this tab no longer shows puts it at the top.
    /// </summary>
    private static void PlaceAfter((ColumnState Column, int Index) found, TaskResponse displayed, MoveChoice move, List<ColumnState> columns)
    {
        if (columns.Find(candidate => candidate.StatusId == move.StatusId) is not { } destination)
        {
            return;
        }

        var (column, index) = found;

        column.Tasks.RemoveAt(index);

        if (!ReferenceEquals(column, destination))
        {
            column.Total--;
            destination.Total++;
        }

        var at = move.AfterTaskId is { } after ? destination.Tasks.FindIndex(task => task.Id == after) + 1 : 0;

        destination.Tasks.Insert(at, displayed);
    }

    /// <summary>
    /// A card a move placed, once nothing is outstanding: by its admitted key in its admitted column - the server's slot
    /// after an admission, its last admitted slot after a refusal - with both totals moved when the column changes.
    /// </summary>
    private static void Relocate((ColumnState Column, int Index) found, TaskResponse displayed, List<ColumnState> columns)
    {
        if (columns.Find(candidate => candidate.StatusId == displayed.StatusId) is not { } destination)
        {
            return;
        }

        var (column, index) = found;

        column.Tasks.RemoveAt(index);

        if (!ReferenceEquals(column, destination))
        {
            column.Total--;
            destination.Total++;
        }

        InsertByKey(destination, displayed);
    }

    private static void InsertByKey(ColumnState column, TaskResponse task)
    {
        var at = column.Tasks.FindIndex(candidate => string.CompareOrdinal(candidate.PositionKey, task.PositionKey) > 0);
        column.Tasks.Insert(at < 0 ? column.Tasks.Count : at, task);
    }

    private (ColumnState Column, int Index)? Locate(Guid taskId)
    {
        foreach (var column in _columns ?? [])
        {
            var index = column.Tasks.FindIndex(task => task.Id == taskId);

            if (index >= 0)
            {
                return (column, index);
            }
        }

        return null;
    }

    /// <summary>
    /// Whether two values of one attribute are the same: a set of Labels by its members in order, anything else by
    /// value.
    /// </summary>
    private static bool SameValue(object? first, object? second) =>
        first is IReadOnlyList<Guid> firstLabels && second is IReadOnlyList<Guid> secondLabels
            ? firstLabels.SequenceEqual(secondLabels)
            : Equals(first, second);

    /// <summary>
    /// The body a change is sent with: a merge patch naming its one member, or - for a move - <c>MoveTaskRequest</c>'s two
    /// members, the destination Status and the neighbour, never a key (story 2.6, open question 2 (a)).
    /// </summary>
    private static Dictionary<string, object?> BodyOf(TaskAttribute attribute, object? value) =>
        value is MoveChoice move
            ? new(StringComparer.Ordinal) { ["statusId"] = move.StatusId, ["afterTaskId"] = move.AfterTaskId }
            : new(StringComparer.Ordinal) { [MemberOf(attribute)] = value };

    private static string MemberOf(TaskAttribute attribute) => attribute switch
    {
        TaskAttribute.Title => "title",
        TaskAttribute.Status => "statusId",
        TaskAttribute.Labels => "labelIds",
        _ => "dueDate",
    };

    private static object? ValueOf(TaskResponse task, TaskAttribute attribute) => attribute switch
    {
        TaskAttribute.Title => task.Title,
        TaskAttribute.Status => task.StatusId,
        TaskAttribute.Labels => task.LabelIds,
        _ => task.DueDate,
    };

    private static TaskResponse Overlay(TaskResponse task, TaskAttribute attribute, object? value) => attribute switch
    {
        TaskAttribute.Title when value is string title => task with { Title = title },
        TaskAttribute.Status when value is Guid statusId => task with { StatusId = statusId },
        TaskAttribute.Status when value is MoveChoice move => task with { StatusId = move.StatusId },
        TaskAttribute.DueDate => task with { DueDate = value as DateTimeOffset? },
        TaskAttribute.Labels when value is IReadOnlyList<Guid> labelIds => task with { LabelIds = labelIds },
        _ => task,
    };

    /// <summary>
    /// The server's answer becomes the attribute's admitted value - its own normalisation included (a trimmed
    /// title, a UTC due date, the Status change's new position key).
    /// </summary>
    private static TaskResponse Admit(TaskResponse admitted, TaskAttribute attribute, TaskResponse stored) => attribute switch
    {
        TaskAttribute.Title => admitted with { Title = stored.Title },
        TaskAttribute.Status => admitted with { StatusId = stored.StatusId, PositionKey = stored.PositionKey },
        TaskAttribute.Labels => admitted with { LabelIds = stored.LabelIds },
        _ => admitted with { DueDate = stored.DueDate },
    };

    /// <summary>
    /// One Task as this tab holds it: its last admitted value, the change out for each attribute, the latest
    /// change waiting behind it, and the key of each change that got no answer.
    /// </summary>
    private sealed class TaskReplica(TaskResponse admitted)
    {
        public TaskResponse Admitted { get; set; } = admitted;

        public Dictionary<TaskAttribute, object?> InFlight { get; } = [];

        public Dictionary<TaskAttribute, object?> Queued { get; } = [];

        public Dictionary<TaskAttribute, (string Body, string Key)> Unanswered { get; } = [];

        /// <summary>
        /// Whether a move put the card in a slot of its own choosing, so it goes back by key once nothing is out.
        /// </summary>
        public bool Relocated { get; set; }

        /// <summary>
        /// The move this Task's Status attribute is waiting on or has out, if that change is a move.
        /// </summary>
        public MoveChoice? OutstandingMove() =>
            Queued.TryGetValue(TaskAttribute.Status, out var queued)
                ? queued as MoveChoice
                : InFlight.GetValueOrDefault(TaskAttribute.Status) as MoveChoice;

        /// <summary>
        /// Whether a change of <paramref name="attribute"/> is waiting or out.
        /// </summary>
        public bool HasOutstanding(TaskAttribute attribute) => Queued.ContainsKey(attribute) || InFlight.ContainsKey(attribute);

        /// <summary>
        /// What the card and the panel show: the admitted Task with each outstanding change laid over it, the
        /// latest waiting change first.
        /// </summary>
        public TaskResponse Displayed()
        {
            var displayed = Admitted;

            foreach (var attribute in Enum.GetValues<TaskAttribute>())
            {
                if (Queued.TryGetValue(attribute, out var queued))
                {
                    displayed = Overlay(displayed, attribute, queued);
                }
                else if (InFlight.TryGetValue(attribute, out var sent))
                {
                    displayed = Overlay(displayed, attribute, sent);
                }
                else
                {
                    // Nothing outstanding for this attribute: the admitted value stands.
                }
            }

            return displayed;
        }
    }
}
