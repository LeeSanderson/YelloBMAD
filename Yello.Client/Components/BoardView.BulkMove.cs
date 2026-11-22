namespace Yello.Client.Components;

/// <summary>
/// The Board's side of moving every Task of one Status into another Project: which column's bar is open, which column is
/// blocked while the move is out, and what happens once it settles. The bar itself sends the request.
/// </summary>
/// <remarks>
/// <para>
/// <b>Blocking never destroys focus.</b> While the move is out only the source column is busy: <c>aria-busy</c> on it, and
/// the Board's own handlers ignore it - no drag in or out, no carry, no create, no card menu, no detail. Nothing is made
/// <c>inert</c> and the bar is drawn above the columns, so focus on the bar's status line survives.
/// </para>
/// <para>
/// <b>Focus goes back to the originating column</b> on a commit as on a cancel. The destination column is on another
/// Project's Board, so the announcement names it instead, and the person can go on emptying this Project Status by Status.
/// </para>
/// <para>
/// <b>After a commit the Board is read again.</b> A column holds only its loaded prefix, so the source column is not emptied
/// card by card: its first page and true total come back from the server, with any Task created into it since.
/// </para>
/// </remarks>
public sealed partial class BoardView
{
    private Guid? _bulkMoveStatusId;
    private bool _bulkMoveBusy;

    private ColumnState? BulkMoveColumn => _bulkMoveStatusId is { } statusId ? Column(statusId) : null;

    /// <summary>
    /// Whether a column offers the bulk move: a Role that manages Tasks, Tasks to move, and no bar already open.
    /// </summary>
    /// <remarks>
    /// A bar whose column a reread dropped is no longer drawn and never settles, so it counts as closed rather than holding
    /// the offer off for the rest of the Space.
    /// </remarks>
    private bool OffersBulkMove(ColumnState column) => _canManage && column.Total > 0 && BulkMoveColumn is null;

    /// <summary>
    /// Whether the column is blocked: the bulk move out of it has been sent and has not settled.
    /// </summary>
    private bool BulkMoveBlocks(Guid statusId) => _bulkMoveBusy && _bulkMoveStatusId == statusId;

    private bool BulkMoveBlocksTask(Guid taskId) =>
        _bulkMoveBusy && Locate(taskId) is { } found && found.Column.StatusId == _bulkMoveStatusId;

    private void OpenBulkMove(Guid statusId)
    {
        if (!_canManage || BulkMoveColumn is not null)
        {
            return;
        }

        _ = CloseBulkMove();

        // A drag or a carry ends with nothing moved, and a menu or a create field closes: the bar is the one thing in hand.
        Returned(EndHeld());
        _menuTaskId = null;
        _menuPicking = false;
        _creating = false;
        _bulkMoveStatusId = statusId;
    }

    private void BulkMoveBusy(bool busy) => _bulkMoveBusy = busy;

    private void BulkMoveCancelled() => FocusColumnAfterBulkMove(CloseBulkMove());

    /// <summary>
    /// The move landed, or the Status had left the Project and nothing moved: either way the Board is read again and the
    /// bar's sentence said.
    /// </summary>
    private async Task BulkMoveSettledAsync(string sentence)
    {
        var statusId = CloseBulkMove();

        await RereadBoardAsync(_generation);
        FocusColumnAfterBulkMove(statusId);
        Say(sentence);
    }

    /// <summary>
    /// Closes the bar and unblocks its column; returns the column it was opened from.
    /// </summary>
    private Guid? CloseBulkMove()
    {
        var statusId = _bulkMoveStatusId;

        _bulkMoveStatusId = null;
        _bulkMoveBusy = false;

        return statusId;
    }

    private void FocusColumnAfterBulkMove(Guid? statusId)
    {
        // A Status that left the Project took its column with it on the reread: the first column is the nearest one left, so
        // focus never falls to the page body.
        var target = statusId is { } status && Column(status) is not null ? status : _columns?.FirstOrDefault()?.StatusId;

        if (target is { } column)
        {
            _focusColumn = column;
            _focusColumnRequests++;
        }
    }
}
