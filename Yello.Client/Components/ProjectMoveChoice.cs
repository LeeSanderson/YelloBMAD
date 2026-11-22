using Yello.Contracts.Tasks;

namespace Yello.Client.Components;

/// <summary>
/// Where a move into another Project puts a Task: the Project, and the Status chosen there when the Project lacks the
/// Task's own. The names travel with the ids so the Board can announce the move and say a refusal without reading them
/// again.
/// </summary>
/// <param name="ProjectId">The destination Project.</param>
/// <param name="ProjectName">Its name, as the picker listed it.</param>
/// <param name="StatusId">The Status chosen there, or <c>null</c> to keep the Task's own.</param>
/// <param name="StatusName">That Status's name, or <c>null</c>.</param>
public sealed record ProjectMoveChoice(Guid ProjectId, string ProjectName, Guid? StatusId, string? StatusName)
{
    /// <summary>
    /// The body the move is sent with: the contract itself, so the wire members are never a second spelling of it.
    /// </summary>
    /// <returns>The request.</returns>
    public MoveTaskToProjectRequest ToRequest() => new(ProjectId, StatusId);
}

/// <summary>
/// How the Board's write of a move into another Project ended, as the picker needs it to choose its next step.
/// </summary>
public enum ProjectMoveAnswer
{
    /// <summary>
    /// The Task is in the destination; the panel is closing.
    /// </summary>
    Moved,

    /// <summary>
    /// The destination lacks the Task's Status: the picker reads its set again and asks for one.
    /// </summary>
    StatusNeeded,

    /// <summary>
    /// The Status chosen is not the destination's any more: the picker reads its set again and asks again.
    /// </summary>
    StatusInvalid,

    /// <summary>
    /// The destination is not another Project of the Space any more: the picker reads the Projects again.
    /// </summary>
    ProjectInvalid,

    /// <summary>
    /// Refused for another reason, or never answered; the panel says why and the picker offers the step again.
    /// </summary>
    Refused,

    /// <summary>
    /// The Task or the Space is gone; the panel is closing.
    /// </summary>
    Gone,
}
