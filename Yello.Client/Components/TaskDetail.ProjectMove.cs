using Microsoft.AspNetCore.Components;

namespace Yello.Client.Components;

/// <summary>
/// The panel's "Move to Project" control: the picker opens in place below it, one picker at a time; Esc out of the picker
/// returns focus to the control; and while a move is with the server the control does nothing, so a second press never
/// sends a second move.
/// </summary>
public sealed partial class TaskDetail
{
    private ElementReference _projectMoveButton;
    private bool _projectMoveOpen;
    private bool _projectMoving;
    private bool _focusProjectMoveButton;

    private void ToggleProjectMove()
    {
        if (_projectMoving)
        {
            return;
        }

        _projectMoveOpen = !_projectMoveOpen;
        _moveOpen = false;
        _pickerOpen = false;
        _labelPickerOpen = false;
        _assigneePickerOpen = false;
    }

    /// <summary>
    /// Closes the picker when another control takes the panel, unless a move it sent is still out.
    /// </summary>
    private void CloseProjectMove()
    {
        if (!_projectMoving)
        {
            _projectMoveOpen = false;
        }
    }

    /// <summary>
    /// Esc from outside the picker: it closes first, unless the move it sent is still out.
    /// </summary>
    private void EscapeProjectMove()
    {
        if (!_projectMoving)
        {
            ProjectMoveDismissed();
        }
    }

    private void ProjectMoveDismissed()
    {
        _projectMoveOpen = false;
        _focusProjectMoveButton = true;
    }

    private async Task<ProjectMoveAnswer> CommitProjectMoveAsync(ProjectMoveChoice choice)
    {
        if (OnMoveToProject is not { } move)
        {
            return ProjectMoveAnswer.Refused;
        }

        _projectMoving = true;

        try
        {
            return await move(choice);
        }
        finally
        {
            _projectMoving = false;
        }
    }

    private async Task FocusProjectMoveButtonAsync()
    {
        if (!_focusProjectMoveButton)
        {
            return;
        }

        _focusProjectMoveButton = false;

        if (!_confirming && CanManage)
        {
            await _projectMoveButton.FocusAsync();
        }
    }
}
