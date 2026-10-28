using Yello.Contracts.Tasks;

namespace Yello.Client.Components;

/// <summary>
/// Where a move puts a Task: the destination Status and the Task it lands after, or <c>null</c> for the top of the
/// column (story 2.6, open question 2 (a)). Also the Board's replica's value for an outstanding move.
/// </summary>
/// <param name="StatusId">The destination Status.</param>
/// <param name="AfterTaskId">The Task to land after, or <c>null</c> for the top.</param>
public sealed record MoveChoice(Guid StatusId, Guid? AfterTaskId);

/// <summary>
/// One column as the Move picker offers it: its Status, its name and the Tasks it holds now, in the Board's order.
/// </summary>
/// <param name="StatusId">The column's Status.</param>
/// <param name="Name">The Status's name.</param>
/// <param name="Tasks">The Tasks the column shows, in order.</param>
public sealed record MoveColumn(Guid StatusId, string Name, IReadOnlyList<TaskResponse> Tasks);
