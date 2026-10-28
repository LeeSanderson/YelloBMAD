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
/// <para>
/// <b><see cref="Total"/> is its own number, never <c>Tasks.Count</c></b> (AD-29, AC5): the true total for
/// the Status, counted by the server over the indexed predicate. The Board answers each column's first page only
/// (story 2.9, AC9), so a client that counted the rows would be wrong.
/// </para>
/// <para>
/// <b><see cref="NextAfter"/> is appended</b> (AD-19, story 2.9 open question 1 (a)): the last served Task's position
/// key - readable, as FR-35 already makes it - when another Task follows, and <c>null</c> exactly when none does. The
/// next page is <see cref="BoardRoutes.Column"/> with <c>after</c> set to it.
/// </para>
/// </remarks>
/// <param name="StatusId">The column's Status.</param>
/// <param name="Name">The Status's name.</param>
/// <param name="Total">The true number of the Project's Tasks in that Status.</param>
/// <param name="Tasks">The column's first page of Tasks - at most one page - by position key.</param>
/// <param name="NextAfter">The cursor: the last served Task's key when another follows, otherwise <c>null</c>.</param>
public sealed record BoardColumnResponse(Guid StatusId, string Name, int Total, IReadOnlyList<TaskResponse> Tasks, string? NextAfter = null);

/// <summary>
/// One page of one Board column: the Tasks after a cursor, by position key (story 2.9, AC1, AC9, open question 1 (a)).
/// </summary>
/// <remarks>
/// <b><see cref="Total"/> is the column's true total</b>, counted over the same indexed predicate as the page (AC4, open
/// question 2 (a)), so a client keeps its chip and <c>aria-setsize</c> true while other Sessions change the column.
/// Nothing here carries a <c>SpaceId</c>.
/// </remarks>
/// <param name="StatusId">The column's Status.</param>
/// <param name="Total">The true number of the Project's Tasks in that Status.</param>
/// <param name="Tasks">At most one page of Tasks whose key sorts after the cursor, by position key.</param>
/// <param name="NextAfter">The cursor: the last served Task's key when another follows, otherwise <c>null</c>.</param>
public sealed record BoardColumnPageResponse(Guid StatusId, int Total, IReadOnlyList<TaskResponse> Tasks, string? NextAfter);
