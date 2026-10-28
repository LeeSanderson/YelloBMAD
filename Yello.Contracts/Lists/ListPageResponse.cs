using Yello.Contracts.Boards;
using Yello.Contracts.Tasks;

namespace Yello.Contracts.Lists;

/// <summary>
/// One page of a Project's List View: the Project, its effective Statuses, the true filtered total, at most a page of
/// Tasks in the sort's order, and the cursor. FR-30; story 2.10, open question 1 (a); AC11, AC22.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every page answers the same shape</b>, the first and every one after a cursor, so the client reads one type.
/// <see cref="Statuses"/> are the Project's effective set in effective order - the Status filter's choices and each row's
/// Status name - so the List View needs no second read.
/// </para>
/// <para>
/// <b><see cref="Total"/> is its own number, never <c>Tasks.Count</c></b> (AD-29, AC11): the true number of the
/// Project's Tasks matching the filter, counted by the server over the same predicate. The client's "Showing n of N", its
/// announced count and its <c>aria-rowcount</c> all come from it.
/// </para>
/// <para>
/// <b><see cref="NextAfter"/> is opaque</b>: the server issues it for the page's sort, and the next page is
/// <see cref="ListRoutes.List"/> with the same query and <c>after</c> set to it. <c>null</c> exactly when nothing follows.
/// </para>
/// <para>
/// <b>Each row is <see cref="TaskResponse"/>, unchanged</b> - the same Task the Board serves. Nothing here carries a
/// <c>SpaceId</c>, as <c>StatusResponse</c>'s remarks explain.
/// </para>
/// </remarks>
/// <param name="Project">The Project the list belongs to.</param>
/// <param name="Statuses">The Project's effective Statuses, in effective order.</param>
/// <param name="Total">The true number of the Project's Tasks matching the filter.</param>
/// <param name="Tasks">At most one page of Tasks, in the sort's order.</param>
/// <param name="NextAfter">The cursor when another Task follows, otherwise <c>null</c>.</param>
public sealed record ListPageResponse(
    BoardProjectResponse Project,
    IReadOnlyList<ListStatusResponse> Statuses,
    int Total,
    IReadOnlyList<TaskResponse> Tasks,
    string? NextAfter);

/// <summary>
/// One of the Project's effective Statuses, as a List View names it.
/// </summary>
/// <param name="Id">Its id.</param>
/// <param name="Name">Its name.</param>
public sealed record ListStatusResponse(Guid Id, string Name);
