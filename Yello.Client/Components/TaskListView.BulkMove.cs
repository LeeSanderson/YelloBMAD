using Microsoft.AspNetCore.Components;
using Yello.Client.Spaces;

namespace Yello.Client.Components;

/// <summary>
/// The List View's one write: every Task of the one Status it is filtered to, moved into another Project through the bulk
/// move bar drawn above the rows.
/// </summary>
/// <remarks>
/// <para>
/// <b>Offered only when the Status is the sole filter</b>, for a Role that manages Tasks and a list that is not empty. With
/// a Label, an Assignee or a due date also set, the rows shown are a part of the Status while the move takes all of it, so
/// what moved would not be what was shown.
/// </para>
/// <para>
/// <b>Focus when it settles</b>: on a commit the list is read again and focus goes to its heading, since the rows it was
/// among are gone; on a cancel, or a Status that has left the Project, back to the control that opened the bar.
/// </para>
/// </remarks>
public sealed partial class TaskListView
{
    private bool _canManage;
    private Guid? _bulkMoveStatusId;
    private bool _bulkMoveBusy;
    private ElementReference _bulkMoveControl;
    private bool _focusBulkMoveControl;
    private bool _focusHeading;

    private Guid? BulkMoveOffered => _canManage && _bulkMoveStatusId is null && ListShown ? _filters.SoleStatus : null;

    private bool ListShown => _rows is not null && _total > 0;

    /// <inheritdoc />
    protected override void OnParametersSet() => _canManage = TaskCapabilities.PermitsManageTasks(RoleCode);

    private void OpenBulkMove() => _bulkMoveStatusId = BulkMoveOffered;

    private void BulkMoveBusy(bool busy) => _bulkMoveBusy = busy;

    private void BulkMoveCancelled()
    {
        CloseBulkMove();
        _focusBulkMoveControl = true;
    }

    private async Task BulkMovedAsync(string sentence)
    {
        CloseBulkMove();
        await RereadAsync();
        Say(sentence);
        _focusHeading = true;
    }

    private async Task BulkMoveGoneAsync(string sentence)
    {
        CloseBulkMove();
        await RereadAsync();
        Say(sentence);
        _focusBulkMoveControl = true;
    }

    private void CloseBulkMove()
    {
        _bulkMoveStatusId = null;
        _bulkMoveBusy = false;
    }

    private async Task FocusAfterBulkMoveAsync()
    {
        if (_focusHeading && Heading.Current is { } heading)
        {
            _focusHeading = false;
            await heading.FocusAsync();
        }

        if (_focusBulkMoveControl && BulkMoveOffered is not null)
        {
            _focusBulkMoveControl = false;
            await _bulkMoveControl.FocusAsync();
        }
    }
}
