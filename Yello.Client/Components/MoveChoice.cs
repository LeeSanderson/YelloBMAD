using Yello.Contracts.Tasks;

namespace Yello.Client.Components;

/// <summary>
/// Where a move puts a Task: the destination Status and the Task it lands after, or <c>null</c> for the top of the
/// column (story 2.6, open question 2 (a)). Also the Board's replica's value for an outstanding move.
/// </summary>
/// <param name="StatusId">The destination Status.</param>
/// <param name="AfterTaskId">The Task to land after, or <c>null</c> for the top.</param>
public sealed record MoveChoice(Guid StatusId, Guid? AfterTaskId)
{
    /// <summary>
    /// The body the move is sent with: the <see cref="MoveTaskRequest"/> contract itself, so the wire members are the
    /// contract's and never a second spelling of them - <c>afterTaskId</c> written out as <c>null</c> for the top.
    /// </summary>
    /// <returns>The request.</returns>
    public MoveTaskRequest ToRequest() => new(StatusId, AfterTaskId);
}

/// <summary>
/// One column as the Move picker offers it: its Status, its name and the Tasks it has loaded, in the Board's order.
/// </summary>
/// <param name="StatusId">The column's Status.</param>
/// <param name="Name">The Status's name.</param>
/// <param name="Tasks">The Tasks the column shows, in order: its loaded prefix (story 2.9).</param>
/// <param name="LoadMore">
/// Loads the column's next page into the Board's own column state, for a partly loaded column; <c>null</c> once every Task
/// of the column is loaded - and only then is "Bottom of column" a concrete neighbour (story 2.9, AC11).
/// </param>
public sealed record MoveColumn(Guid StatusId, string Name, IReadOnlyList<TaskResponse> Tasks, Func<Task>? LoadMore = null);
