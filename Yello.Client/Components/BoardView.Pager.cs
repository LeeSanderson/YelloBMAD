using System.Globalization;

namespace Yello.Client.Components;

/// <summary>
/// The Board below 768px (story 2.11): one column at a time behind the Status pager, a tablist over the Project's effective
/// Status set whose panel is the column. AC3-AC5, AC9-AC15, AC20; UX-DR20, UX-DR39, UX-DR41; AD-29.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every column stays mounted</b> (open question 1 (a)). CSS hides each unselected column by construction at the media
/// query, so the first paint at 320px is already one column; the keys module (<see cref="BoardKeys"/>) tells this Board when
/// the viewport crosses 768px, and only then does it render the tablist, the <c>tabpanel</c> roles and <c>hidden</c>. Loaded
/// pages, cursors, true totals and focus requests are never lost to a remount, and a tab change reads nothing (AD-29).
/// </para>
/// <para>
/// <b>The selection</b> (open question 2 (a)) is Space-scoped state: <c>null</c> means the first column; it is purged with the
/// Board and never stored. The person changes it with the tabs' arrows, <c>Home</c>, <c>End</c>, a click or a tap, and only
/// that change says "{Status}, {n} Tasks." with the TRUE total (AC10). The Board changes it itself - following a moved,
/// returned or created Task, a closing panel, or the viewport crossing 768px - and says nothing for it: the move's own
/// sentence is the announcement.
/// </para>
/// </remarks>
public sealed partial class BoardView
{
    // Whether the viewport is below 768px, as the keys module last reported it.
    private bool _narrow;

    // The Status pager's selected column, or null for the first (open question 2 (a)). Purged with the Board.
    private Guid? _selectedStatusId;

    // Bumped to move focus to the selected tab, after an arrow, Home or End on the tablist.
    private int _focusTabRequests;

    /// <summary>
    /// The column the pager shows: the selected one, or the first when none is selected or the selected Status has gone.
    /// </summary>
    private ColumnState? SelectedColumn =>
        _columns is { Count: > 0 } columns ? columns.Find(column => column.StatusId == _selectedStatusId) ?? columns[0] : null;

    /// <summary>
    /// The pager's tabs: each column's Status, name, true total and the total in words (AC9, open question 4 (a)).
    /// </summary>
    private List<(Guid StatusId, string Name, int Total, string CountText)> PagerTabs =>
        (_columns ?? []).ConvertAll(column => (column.StatusId, column.Name, column.Total, CountTextOf(column.Total)));

    private bool IsSelected(ColumnState column) => ReferenceEquals(SelectedColumn, column);

    /// <summary>
    /// The one roving stop below 768px (AC9): the active card when the shown column holds it, else that column's first card,
    /// else the column itself.
    /// </summary>
    private (Guid? TaskId, Guid? StatusId) StopIn(ColumnState shown)
    {
        if (_activeTaskId is { } taskId && Locate(taskId) is { } found && ReferenceEquals(found.Column, shown))
        {
            return (taskId, null);
        }

        return shown.Tasks.Count == 0 ? (null, shown.StatusId) : (shown.Tasks[0].Id, null);
    }

    /// <summary>
    /// "1 Task" or "{n} Tasks": the true total in words, as a column's description says it (story 2.8, AC13).
    /// </summary>
    private string CountTextOf(int total) =>
        total == 1 ? Localizer["ColumnCountOne"] : string.Format(CultureInfo.CurrentCulture, Localizer["ColumnCountMany"], total);

    /// <inheritdoc />
    void IBoardKeysHost.Narrowed(bool narrow, bool fromPager)
    {
        if (narrow == _narrow)
        {
            return;
        }

        _narrow = narrow;

        if (narrow)
        {
            GoNarrow();
        }
        else if (fromPager)
        {
            // The tablist goes with the width: focus moves to the visible column's stop rather than the page body.
            FocusTheActiveStop();
        }
        else
        {
            // Focus was in a column, which stays mounted and visible: nothing moves.
        }

        StateHasChanged();
    }

    /// <summary>
    /// An arrow, <c>Home</c> or <c>End</c> on a tab (AC4, AC9, open question 3 (a)): automatic activation - the tab it lands
    /// on is selected, focus moves to it, and the change is said.
    /// </summary>
    void IBoardKeysHost.PagerKey(BoardKey key, Guid statusId)
    {
        if (_columns is not { Count: > 0 } columns)
        {
            return;
        }

        var at = columns.FindIndex(column => column.StatusId == statusId);

        if (at < 0)
        {
            return;
        }

        var next = BoardTraversal.Pager(columns.Count, at, key);

        if (next != at && Choose(columns[next]))
        {
            _focusTabRequests++;
            StateHasChanged();
        }
    }

    /// <summary>
    /// A tab clicked or tapped (AC10): selected and said; focus is already on it.
    /// </summary>
    private void SelectTab(Guid statusId)
    {
        if (Column(statusId) is { } column)
        {
            _ = Choose(column);
        }
    }

    /// <summary>
    /// The person's own change of tab: the column is selected, the Board's one roving stop moves into it - its first card, or
    /// the column itself when it is empty - so Tab never lands in a hidden column, and the Status and its true total are
    /// said once (AC10). Answers whether the selection changed.
    /// </summary>
    private bool Choose(ColumnState column)
    {
        if (IsSelected(column))
        {
            return false;
        }

        _selectedStatusId = column.StatusId;
        _activeTaskId = null;
        _activeColumnId = column.StatusId;
        _runAt = null;

        Say(string.Format(CultureInfo.CurrentCulture, Localizer["PagerShown"], column.Name, CountTextOf(column.Total)));

        return true;
    }

    /// <summary>
    /// The Board's own change of tab (AC5, AC14, AC15): to the column that now holds <paramref name="taskId"/>'s card - or
    /// counts it, when it is held out past a cursor - and nothing is said.
    /// </summary>
    private void Follow(Guid taskId)
    {
        if (Locate(taskId) is { } found)
        {
            _selectedStatusId = found.Column.StatusId;
        }
        else if (_heldOut.TryGetValue(taskId, out var statusId))
        {
            _selectedStatusId = statusId;
        }
        else
        {
            // Not on this Board any more: the selection stays.
        }
    }

    /// <summary>
    /// Crossing below 768px (AC14, open question 2 (a)): the column holding a carried Task, else the active card's or the
    /// active empty column's, is selected, so focus stays on the same Task or column. A pointer or touch lift ends as a cancel
    /// and says where the Task is (2.7's rule); a carry goes on, its destination back in its own column.
    /// </summary>
    private void GoNarrow()
    {
        if (_drag?.LiftedTaskId is { } lifted)
        {
            _drag.End();
            Returned(lifted);
        }

        if (_carried is { } carried && Locate(carried) is { } held)
        {
            _carryStatusId = held.Column.StatusId;
            _carrySlot = held.Index;
            _carryOrigin = held.Index;
            _selectedStatusId = held.Column.StatusId;
            return;
        }

        if (_activeTaskId is { } taskId && Locate(taskId) is { } active)
        {
            _selectedStatusId = active.Column.StatusId;
        }
        else if (_activeColumnId is { } statusId && Column(statusId) is not null)
        {
            _selectedStatusId = statusId;
        }
        else
        {
            // Focus has not been in the Board: the selected tab stays.
        }
    }

    /// <summary>
    /// Focus to the Board's one roving stop, by its identity.
    /// </summary>
    private void FocusTheActiveStop()
    {
        var (taskId, statusId) = ActiveStop;

        if (taskId is { } task)
        {
            _focusCard = task;
            _focusCardRequests++;
        }
        else if (statusId is { } status)
        {
            _focusColumn = status;
            _focusColumnRequests++;
        }
        else
        {
            // No Board drawn: nowhere to go.
        }
    }
}
