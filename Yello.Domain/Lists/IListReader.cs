using Yello.Domain.Tasks;

namespace Yello.Domain.Lists;

/// <summary>
/// Reads one Project's Tasks as a List View, in the Space the current unit of work is scoped to: the true count under a
/// filter, and one page under a sort after a cursor. FR-30; story 2.10. The Project and the page's Labels are the Board's
/// reads (<see cref="Boards.IBoardReader"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>A pair keyset, never <c>OFFSET</c></b> (AR-22, AD-29): every offered sort column is non-unique, so a page seeks to
/// the first <c>(sort value, TaskId)</c> pair after the cursor on that sort's composite index, with the Task id compared
/// by the database - SQL Server orders a <c>uniqueidentifier</c> as .NET does not - and <c>NULL</c> fixed last.
/// </para>
/// <para>
/// <b>The total is its own read</b> (AC11): a <c>COUNT</c> over the same filter predicate, never the rows served.
/// </para>
/// <para>
/// <b>Never asked about another Space</b>, for the reason <see cref="ITaskStore"/> gives.
/// </para>
/// </remarks>
public interface IListReader
{
    /// <summary>
    /// Which of <paramref name="labelIds"/> name a Label visible in the resolved Space - the handler's narrowing of a
    /// Label filter (AC15).
    /// </summary>
    /// <param name="labelIds">The Label ids a filter was sent.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The visible ones; never another Space's.</returns>
    Task<IReadOnlyList<Guid>> VisibleLabelIdsAsync(IReadOnlyCollection<Guid> labelIds, CancellationToken cancellationToken);

    /// <summary>
    /// How many of the Project's Tasks match <paramref name="filter"/>, counted by the database.
    /// </summary>
    /// <param name="projectId">The Project.</param>
    /// <param name="filter">The narrowed filter.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The true total.</returns>
    Task<int> CountAsync(Guid projectId, ListFilter filter, CancellationToken cancellationToken);

    /// <summary>
    /// One page: the Project's Tasks matching <paramref name="filter"/> whose pair under <paramref name="sort"/> sorts
    /// after <paramref name="after"/>, in that order, at most <paramref name="take"/> of them.
    /// </summary>
    /// <param name="projectId">The Project.</param>
    /// <param name="filter">The narrowed filter.</param>
    /// <param name="sort">The sort.</param>
    /// <param name="statusOrder">
    /// The Statuses to list under <see cref="ListSort.Status"/>, in effective order - the effective set narrowed by the
    /// filter. Unused by the other sorts.
    /// </param>
    /// <param name="after">The cursor, or <c>null</c> for the first page.</param>
    /// <param name="take">The most Tasks to read; a caller asks for one more than a page to learn whether another follows.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The Tasks, materialised, in the sort's order.</returns>
    Task<IReadOnlyList<TaskItem>> PageAsync(
        Guid projectId,
        ListFilter filter,
        ListSort sort,
        IReadOnlyList<Guid> statusOrder,
        ListCursor? after,
        int take,
        CancellationToken cancellationToken);
}
