using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Yello.Client.Refusals;
using Yello.Client.Spaces;
using Yello.Contracts;
using Yello.Contracts.Tasks;

namespace Yello.Client.Components;

/// <summary>
/// The Board's Task detail panel and its replica: the product's first optimistic writes. Story 2.3; AC1,
/// AC12, AC14; AD-11, AD-18; open questions 1 (a), 6 (a) and 8.
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
/// </remarks>
public sealed partial class BoardView
{
    private readonly Dictionary<Guid, TaskReplica> _replicas = [];

    private List<TaskStatusOption> _statusOptions = [];
    private Guid? _openTaskId;
    private string? _panelRefusal;
    private Guid? _focusCard;
    private int _focusCardRequests;

    /// <summary>
    /// The three attributes a Task edit can change.
    /// </summary>
    private enum TaskAttribute
    {
        Title,
        Status,
        DueDate,
    }

    /// <summary>
    /// How one sent edit ended.
    /// </summary>
    private enum EditOutcome
    {
        Admitted,
        Refused,
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
        if (_openTaskId == taskId)
        {
            return;
        }

        _openTaskId = taskId;
        _panelRefusal = null;
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
    }

    /// <summary>
    /// The panel's state goes with the Board's purge: nothing about the Space's Tasks outlives it.
    /// </summary>
    private void PurgePanel()
    {
        _replicas.Clear();
        _statusOptions = [];
        _openTaskId = null;
        _panelRefusal = null;
        _focusCard = null;
    }

    private void UseStatuses(IEnumerable<TaskStatusOption> statuses) => _statusOptions = [.. statuses];

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

        if (attribute == TaskAttribute.Status && value is Guid statusId)
        {
            var name = _statusOptions.Find(status => status.Id == statusId)?.Name ?? string.Empty;
            _announcement = string.Format(CultureInfo.CurrentCulture, Localizer["TaskMoved"], name);
        }

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

        while (more)
        {
            var outcome = await SendAsync(taskId, replica, attribute, sending, generation);

            more = outcome == EditOutcome.Admitted
                && replica.Queued.Remove(attribute, out sending)
                && !Equals(sending, ValueOf(replica.Admitted, attribute));

            if (more)
            {
                replica.InFlight[attribute] = sending;
            }
        }

        if (generation == _generation)
        {
            Place(taskId, replica);
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
            using var answer = await Writes.SendAsync(HttpMethod.Patch, TaskRoutes.TaskOf(SpaceId, taskId), body, key, token);

            if (generation != _generation)
            {
                return EditOutcome.Gone;
            }

            if (answer.Problem?.Type != ProblemTypes.IdempotencyKeyInFlight)
            {
                replica.Unanswered.Remove(attribute);
            }

            replica.InFlight.Remove(attribute);

            return await UseAnswerAsync(replica, attribute, answer, generation, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The tab left this Space, or the Session ended: the purge has run.
            return EditOutcome.Gone;
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or JsonException)
        {
            // No answer. The key is kept for a resend; the change reverts, and the person is told it was not sent.
            return NotSent(replica, attribute, generation);
        }
    }

    private async Task<EditOutcome> UseAnswerAsync(TaskReplica replica, TaskAttribute attribute, SpaceWriteAnswer answer, int generation, CancellationToken token)
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
                    return NotSent(replica, attribute, generation);
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
                _openTaskId = null;
                _panelRefusal = null;
                await OnRefused.InvokeAsync(answer.Problem);
                return EditOutcome.Refused;

            default:
                replica.Queued.Remove(attribute);
                _panelRefusal = RefusalText.Of(Localizer, answer.Problem);
                return EditOutcome.Refused;
        }
    }

    private EditOutcome NotSent(TaskReplica replica, TaskAttribute attribute, int generation)
    {
        if (generation != _generation)
        {
            return EditOutcome.Gone;
        }

        replica.InFlight.Remove(attribute);
        replica.Queued.Remove(attribute);
        _panelRefusal = Localizer["TaskEditNotSent"];

        return EditOutcome.Refused;
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

        if (column.StatusId == displayed.StatusId)
        {
            column.Tasks[index] = displayed;
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
            var at = destination.Tasks.FindIndex(task => string.CompareOrdinal(task.PositionKey, displayed.PositionKey) > 0);
            destination.Tasks.Insert(at < 0 ? destination.Tasks.Count : at, displayed);
        }
        else
        {
            destination.Tasks.Add(displayed);
        }
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

    private static Dictionary<string, object?> BodyOf(TaskAttribute attribute, object? value) =>
        new(StringComparer.Ordinal) { [MemberOf(attribute)] = value };

    private static string MemberOf(TaskAttribute attribute) => attribute switch
    {
        TaskAttribute.Title => "title",
        TaskAttribute.Status => "statusId",
        _ => "dueDate",
    };

    private static object? ValueOf(TaskResponse task, TaskAttribute attribute) => attribute switch
    {
        TaskAttribute.Title => task.Title,
        TaskAttribute.Status => task.StatusId,
        _ => task.DueDate,
    };

    private static TaskResponse Overlay(TaskResponse task, TaskAttribute attribute, object? value) => attribute switch
    {
        TaskAttribute.Title when value is string title => task with { Title = title },
        TaskAttribute.Status when value is Guid statusId => task with { StatusId = statusId },
        TaskAttribute.DueDate => task with { DueDate = value as DateTimeOffset? },
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
