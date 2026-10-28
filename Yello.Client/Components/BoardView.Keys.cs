using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Yello.Client.Components;

/// <summary>
/// The Board's keyboard (story 2.8): one roving tab stop, the arrow grammar, and the pick-up, carry, drop and cancel of a
/// Task - its drop story 2.6's move. AC1-AC12, AC17; UX-DR24, UX-DR25, UX-DR27, UX-DR38, UX-DR39, UX-DR42; NFR-9; AD-29.
/// </summary>
/// <remarks>
/// <para>
/// <b>One tab stop</b> (AC7, open questions 1 (a), 2 (a), 3 (a)): exactly one element of the row of columns is
/// <c>tabindex="0"</c> - the active card's title, with its actions control, or an empty column - and every other card is
/// <c>-1</c>. The active stop follows focus however it moved (a click, a panel closing, a move's arrival, a deletion), as
/// the module reports it. The structure stays native - a section, a heading and a list per column - so screen-reader browse
/// mode keeps the Board and the Move control, which is the conformance path; the arrows are an accelerator, and nothing is
/// <c>role="application"</c> (AC7, UX-DR21).
/// </para>
/// <para>
/// <b>The keyboard drives the window</b> (story 2.9, AC7, AC11): <c>↓</c> on a partly loaded column's last loaded card, or a
/// carried step past it, fetches the next page first and then lands on the next Task by its identity. While that page is
/// out, a further <c>↓</c> in the column is ignored, never queued past the boundary. <see cref="BoardTraversal"/> stays a
/// pure function over the loaded counts; the boundary is handled here. Nothing is announced for the page.
/// </para>
/// <para>
/// <b>Focus by identity</b> (AD-29): every focus move is a request naming a Task (or a column's Status), honoured by whichever
/// card is drawn with that Task next - never a stored element.
/// </para>
/// <para>
/// <b>The carry</b> (AC3, AC5, AC9, AC10): Space on a mover's card picks the Task up and says so; the arrows step its
/// destination slot - counted without it, at the sticky ordinal clamped to each column - and say each step; the drop zone
/// marks the slot and the card stays where it is, marked carried (open question 12 (a)). Space drops it through
/// <see cref="MoveAsync"/> - the one write path, the same neighbour rule as the drag - or, on its own slot, sends nothing and
/// says the return; Esc or Tab cancels. A Viewer's Space is never a pick-up (UX-DR27): the module prevents it only on a
/// mover's card, and the Board picks up only for a Role that moves Tasks.
/// </para>
/// </remarks>
public sealed partial class BoardView
{
    private BoardKeys? _keys;
    private ElementReference _boardElement;

    // The roving tab stop: the card or the empty column focus was last in (null until focus has been in the Board).
    private Guid? _activeTaskId;
    private Guid? _activeColumnId;

    // The traversal run: the stop the arrows last landed on, and its sticky origin index (AC2).
    private Guid? _runAt;
    private int _runOrigin;

    // The carry: the Task in hand, its destination column and slot (counted without it), and its sticky origin index.
    private Guid? _carried;
    private Guid _carryStatusId;
    private int _carrySlot;
    private int _carryOrigin;
    private bool _revealDropZone;

    /// <summary>
    /// The one tab stop of the row of columns: the active card while it is on the Board; an active empty column while it is
    /// empty (its first card once it holds one); otherwise the first card of the first column holding one, or the first
    /// column of an empty Board.
    /// </summary>
    private (Guid? TaskId, Guid? StatusId) ActiveStop
    {
        get
        {
            if (_columns is not { Count: > 0 } columns)
            {
                return (null, null);
            }

            if (_activeTaskId is { } taskId && Locate(taskId) is not null)
            {
                return (taskId, null);
            }

            if (_activeColumnId is { } statusId && columns.Find(column => column.StatusId == statusId) is { } active)
            {
                return active.Tasks.Count == 0 ? (null, statusId) : (active.Tasks[0].Id, null);
            }

            return columns.Find(column => column.Tasks.Count > 0) is { } first
                ? (first.Tasks[0].Id, null)
                : (null, columns[0].StatusId);
        }
    }

    /// <summary>
    /// Each column with the Board's one tab stop, worked out once per render rather than once per column.
    /// </summary>
    private IEnumerable<(ColumnState Column, (Guid? TaskId, Guid? StatusId) Stop)> WithActiveStop(List<ColumnState> columns)
    {
        var stop = ActiveStop;

        return columns.Select(column => (column, stop));
    }

    /// <inheritdoc />
    void IBoardKeysHost.Focused(Guid? taskId, Guid? statusId)
    {
        var changed = false;

        EndRunUnlessAt(taskId, statusId);

        // Focus never leaves with a Task in hand: anywhere but the carried card ends the carry, said as a return.
        if (_carried is { } carried && taskId != carried)
        {
            _carried = null;
            Returned(carried);
            changed = true;
        }

        if (taskId is { } task && Locate(task) is not null && _activeTaskId != task)
        {
            _activeTaskId = task;
            _activeColumnId = null;
            changed = true;
        }
        else if (taskId is null && statusId is { } status && _activeColumnId != status)
        {
            _activeColumnId = status;
            _activeTaskId = null;
            changed = true;
        }
        else
        {
            // Already the active stop, or focus left the Board: the stop stays where it was.
        }

        if (changed)
        {
            StateHasChanged();
        }
    }

    /// <summary>
    /// A focus move the arrows did not make - a click, Tab out and back, a panel closing - starts a new run (AC2): the
    /// arrows' own moves land focus on the stop they recorded.
    /// </summary>
    private void EndRunUnlessAt(Guid? taskId, Guid? statusId)
    {
        if ((taskId ?? statusId) != _runAt)
        {
            _runAt = null;
        }
    }

    /// <inheritdoc />
    async Task IBoardKeysHost.KeyAsync(BoardKey key, Guid? taskId, Guid? statusId)
    {
        if (_columns is null)
        {
            return;
        }

        if (_carried is not null)
        {
            await CarryKeyAsync(key);
            return;
        }

        switch (key)
        {
            case BoardKey.Up or BoardKey.Down or BoardKey.InlineStart or BoardKey.InlineEnd:
                await TraverseOrPageAsync(key, taskId, statusId);
                break;

            case BoardKey.Space when taskId is { } id:
                PickUp(id);
                break;

            default:
                // Any other key ends the run: the next arrow starts from where focus is.
                _runAt = null;
                break;
        }
    }

    /// <summary>
    /// The keyboard is attached while the Board is drawn, for every Role.
    /// </summary>
    private async Task AttachKeysAsync()
    {
        if (_keys is not { } keys)
        {
            return;
        }

        await (_columns is not null ? keys.AttachAsync(_boardElement) : keys.DetachAsync());

        if (_revealDropZone)
        {
            _revealDropZone = false;
            await keys.RevealAsync();
        }
    }

    /// <summary>
    /// One arrow on a stop: <c>↓</c> on the last loaded card of a partly loaded column fetches the next page first (story
    /// 2.9, AC7, AC11); every other arrow is the grammar's.
    /// </summary>
    private Task TraverseOrPageAsync(BoardKey key, Guid? taskId, Guid? statusId)
    {
        if (key == BoardKey.Down && taskId is { } id && LastLoadedOfAPartlyLoadedColumn(id) is { } partly)
        {
            return StepPastTheLoadedAsync(partly, id);
        }

        Traverse(key, taskId, statusId);

        return Task.CompletedTask;
    }

    /// <summary>
    /// The column whose last loaded card is <paramref name="taskId"/>'s, when more Tasks follow it (story 2.9).
    /// </summary>
    private ColumnState? LastLoadedOfAPartlyLoadedColumn(Guid taskId) =>
        Locate(taskId) is { } found && found.Column.After is not null && found.Index == found.Column.Tasks.Count - 1 ? found.Column : null;

    /// <summary>
    /// One arrow on a stop (AC1, AC2): focus moves to the card or empty column the grammar names, by its identity.
    /// </summary>
    private void Traverse(BoardKey key, Guid? taskId, Guid? statusId)
    {
        if (_columns is not { } columns || StopOf(columns, taskId, statusId) is not { } stop)
        {
            return;
        }

        var here = taskId ?? statusId;
        var origin = _runAt == here ? _runOrigin : stop.Index.GetValueOrDefault();
        var (next, nextOrigin) = BoardTraversal.Traverse(columns.ConvertAll(column => column.Tasks.Count), stop, key, origin);
        var column = columns[next.Column];

        if (next.Index is { } index)
        {
            var id = column.Tasks[index].Id;

            _activeTaskId = id;
            _activeColumnId = null;
            _focusCard = id;
            _focusCardRequests++;
            _runAt = id;
        }
        else
        {
            _activeColumnId = column.StatusId;
            _activeTaskId = null;
            _focusColumn = column.StatusId;
            _focusColumnRequests++;
            _runAt = column.StatusId;
        }

        _runOrigin = nextOrigin;
        StateHasChanged();
    }

    /// <summary>
    /// <c>↓</c> on a partly loaded column's last loaded card (story 2.9, AC7, AC11): the next page is appended, then focus
    /// lands on the Task after <paramref name="taskId"/>, by its identity, the run's origin with it. While the page is out
    /// the key is ignored; a page that brought nothing leaves focus where it is. Nothing is announced (AC6).
    /// </summary>
    private async Task StepPastTheLoadedAsync(ColumnState column, Guid taskId)
    {
        if (column.Loading || !await LoadMoreAsync(column) || Locate(taskId) is not { } found || found.Index + 1 >= found.Column.Tasks.Count)
        {
            return;
        }

        var next = found.Column.Tasks[found.Index + 1].Id;

        _activeTaskId = next;
        _activeColumnId = null;
        FocusCardTitle(next);
        _runAt = next;
        _runOrigin = found.Index + 1;
        StateHasChanged();
    }

    /// <summary>
    /// The stop a card or a column is, as the grammar counts it: the column's position, and the card's index or <c>null</c>.
    /// </summary>
    private (int Column, int? Index)? StopOf(List<ColumnState> columns, Guid? taskId, Guid? statusId)
    {
        if (taskId is { } task && Locate(task) is { } found)
        {
            return (columns.IndexOf(found.Column), found.Index);
        }

        var at = statusId is { } status ? columns.FindIndex(column => column.StatusId == status) : -1;

        return at < 0 ? null : (at, null);
    }

    /// <summary>
    /// Whether Space may pick <paramref name="taskId"/> up: the Role moves Tasks, and no panel, menu or pointer lift is in
    /// the way (a lift and a carry never coexist).
    /// </summary>
    private bool CanPickUp(Guid taskId) => _canManage && NothingInTheWay && Locate(taskId) is not null;

    private bool NothingInTheWay => _openTaskId is null && _menuTaskId is null && _drag?.LiftedTaskId is null;

    /// <summary>
    /// Space on a mover's card (AC3): the Task is picked up - only for a Role that moves Tasks, with no panel, menu or pointer
    /// lift in the way - and the pick-up sentence says the Task, its Status, position and the column's total (open question 5).
    /// </summary>
    private void PickUp(Guid taskId)
    {
        if (!CanPickUp(taskId) || Locate(taskId) is not { } found)
        {
            return;
        }

        var (column, index) = found;

        _carried = taskId;
        _carryStatusId = column.StatusId;
        _carrySlot = index;
        _carryOrigin = index;
        _settlingCard = null;
        _movedCard = null;
        _runAt = null;
        _revealDropZone = true;

        Say(string.Format(CultureInfo.CurrentCulture, Localizer["TaskPickedUp"], column.Tasks[index].Title, column.Name, index + 1, column.Total));
        StateHasChanged();
    }

    /// <summary>
    /// A key during a carry: an arrow steps the destination, Space drops, Esc and Tab cancel; anything else does nothing.
    /// </summary>
    private async Task CarryKeyAsync(BoardKey key)
    {
        if (key is BoardKey.Up or BoardKey.Down or BoardKey.InlineStart or BoardKey.InlineEnd)
        {
            await StepAsync(key);
        }
        else if (key == BoardKey.Space)
        {
            await DropCarriedAsync();
        }
        else if (key is BoardKey.Escape or BoardKey.Tab)
        {
            // Nothing is sent; the card never left its slot, and focus stays on it (or goes on, for Tab).
            Returned(EndHeld());
            StateHasChanged();
        }
        else
        {
            // Enter, the context-menu key and Shift+F10 do nothing while a Task is in hand (the module prevents them).
        }
    }

    /// <summary>
    /// One carried step (AC5, AC10): the slot moves by the grammar, the drop zone follows it into view, and the step sentence
    /// says the Status, the ordinal and the total the column would have with the Task in it. A <c>↓</c> from the slot after
    /// a partly loaded column's last loaded card fetches the next page first, then steps (story 2.9, AC11); the sentence
    /// still carries the true total.
    /// </summary>
    private async Task StepAsync(BoardKey key)
    {
        if (_columns is not { } columns || _carried is not { } carried)
        {
            return;
        }

        var at = columns.FindIndex(column => column.StatusId == _carryStatusId);

        if (at < 0 || !await ReachTheNextSlotAsync(key, columns[at], carried) || Locate(carried) is not { } found)
        {
            return;
        }

        var others = columns.ConvertAll(column => column.Tasks.Count(task => task.Id != carried));
        var (next, slot, origin) = BoardTraversal.Carry(others, at, _carrySlot, key, _carryOrigin);

        _carryOrigin = origin;

        if (next == at && slot == _carrySlot)
        {
            return;
        }

        var destination = columns[next];
        var total = destination.Total + (ReferenceEquals(destination, found.Column) ? 0 : 1);

        _carryStatusId = destination.StatusId;
        _carrySlot = slot;
        _revealDropZone = true;

        Say(string.Format(CultureInfo.CurrentCulture, Localizer["TaskCarried"], destination.Name, slot + 1, total));
        StateHasChanged();
    }

    /// <summary>
    /// Before a carried <c>↓</c> (story 2.9, AC11): from the slot after a partly loaded column's last loaded card, the next
    /// page is appended first. Answers whether the step may go on - not while a page is out, nor when none came, nor when
    /// the carry ended meanwhile.
    /// </summary>
    private async Task<bool> ReachTheNextSlotAsync(BoardKey key, ColumnState column, Guid carried)
    {
        if (key != BoardKey.Down || column.After is null || _carrySlot < column.Tasks.Count(task => task.Id != carried))
        {
            return true;
        }

        return !column.Loading && await LoadMoreAsync(column) && _carried == carried && Locate(carried) is not null;
    }

    /// <summary>
    /// Space during a carry (AC9): the Task goes to the slot through story 2.6's move - optimistic, announced on its answer,
    /// focus following the card by identity - or, on its own slot, nothing is sent and its return is said.
    /// </summary>
    private async Task DropCarriedAsync()
    {
        var taskId = _carried;
        var statusId = _carryStatusId;
        var slot = _carrySlot;

        EndHeld();

        if (taskId is not { } id
            || Locate(id) is not { } found
            || _columns?.Find(column => column.StatusId == statusId) is not { } destination)
        {
            Returned(taskId);
            StateHasChanged();
            return;
        }

        var (after, ownSlot) = DropChoice(found, destination, id, slot);

        if (ownSlot)
        {
            Returned(id);
            StateHasChanged();
            return;
        }

        var moving = MoveAsync(id, new MoveChoice(statusId, after), MoveOrigin.Keyboard);

        StateHasChanged();
        await moving;
        StateHasChanged();
    }
}
