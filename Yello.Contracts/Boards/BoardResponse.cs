using Yello.Contracts.Tasks;

namespace Yello.Contracts.Boards;

/// <summary>
/// One Project's Board: the Project, and one column per Status of its effective set, in effective order.
/// FR-28; story 2.2.
/// </summary>
/// <remarks>
/// <b>Nothing here carries a <c>SpaceId</c></b>, as <c>StatusResponse</c>'s remarks explain.
/// </remarks>
/// <param name="Project">The Project the Board belongs to.</param>
/// <param name="Columns">The columns, in effective order.</param>
public sealed record BoardResponse(BoardProjectResponse Project, IReadOnlyList<BoardColumnResponse> Columns);

/// <summary>
/// The Project a Board belongs to, as the Board names it - the context bar's source for the Project's name.
/// </summary>
/// <param name="Id">Its id.</param>
/// <param name="Name">Its name.</param>
public sealed record BoardProjectResponse(Guid Id, string Name);

/// <summary>
/// One column of a Board.
/// </summary>
/// <remarks>
/// <b><see cref="Total"/> is its own number, never <c>Tasks.Count</c></b> (AD-29, AC5): the true total for
/// the Status, counted by the server over the indexed predicate. Story 2.9 returns fewer Tasks than the
/// total; a client that counted the rows would then be wrong.
/// </remarks>
/// <param name="StatusId">The column's Status.</param>
/// <param name="Name">The Status's name.</param>
/// <param name="Total">The true number of the Project's Tasks in that Status.</param>
/// <param name="Tasks">The Tasks in the column, by position key and then id.</param>
public sealed record BoardColumnResponse(Guid StatusId, string Name, int Total, IReadOnlyList<TaskResponse> Tasks);
