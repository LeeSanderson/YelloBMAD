using Microsoft.EntityFrameworkCore;
using Yello.Domain.Lists;
using Yello.Domain.Tasks;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Reads one Project's Tasks as a List View inside the request's Space-scoped unit of work: the filtered count, and a page
/// under one of three sorts by pair keyset seek. Story 2.10; FR-30, AR-22, AD-29.
/// </summary>
/// <remarks>
/// <para>
/// <b>No <c>Where</c> on <c>SpaceId</c></b>, for the reason <see cref="StatusDefinitionReader"/> gives: the policy and the
/// query filter supply it, and with it the leading column of every index read here.
/// </para>
/// <para>
/// <b>The pair keyset, compared by the database</b> (AC4): each sort orders by its column then <c>Id</c> and seeks
/// <c>(column &gt; @v) OR (column = @v AND Id &gt; @id)</c> on <c>(SpaceId, ProjectId, column, Id)</c>
/// (<see cref="ListSortColumns"/>). <c>Guid.CompareTo(…) &gt; 0</c> translates to <c>Id &gt; @id</c>, so the tiebreaker is
/// SQL Server's <c>uniqueidentifier</c> order - its last six bytes first - and no Task id is ever ordered or compared in
/// .NET, whose <see cref="Guid"/> order differs (spine <c>:268</c>). Every parameter takes its column's own mapping
/// (<c>datetimeoffset</c>, <c>nvarchar(50)</c>, <c>uniqueidentifier</c>), so no plan converts a column (PG-12's check,
/// <c>ListPagingTests</c> LV-19).
/// </para>
/// <para>
/// <b><c>NULL</c> last, explicitly</b> (AC5, AC13, open question 5 (a)): the due-date and Label sorts read in two
/// segments - the valued Tasks, then, in the same page when they run out, the Tasks with none by <c>Id</c> - each its own
/// bounded seek on the same index. SQL Server's default would put <c>NULL</c> first; neither segment leaves it to that.
/// </para>
/// <para>
/// <b>The Status sort is one seek per Status, in the effective order the handler passes</b> (AC14, open question 7 (a)):
/// within the cursor's Status the Tasks after its id, then each later Status from its start, until the page is full - at
/// most one statement per effective Status, as the Board's first paint is.
/// </para>
/// <para>
/// <b>Filters are residual predicates on the sort's index</b> (open question 4 (a)): a Status set (<c>IN</c>), any of a
/// Label set (an <c>EXISTS</c> on <c>TaskLabel</c>'s primary key), a half-open due range or "no due date". The indexes
/// <c>INCLUDE</c> the other filters' columns. The count is the same predicate with no order (AC11).
/// </para>
/// <para>
/// <b>Each statement reads its own snapshot</b>, as <see cref="BoardReader"/>'s do: no read here waits on a writer.
/// </para>
/// </remarks>
/// <param name="dbContext">The request's unit of work.</param>
internal sealed class ListReader(YelloDbContext dbContext) : IListReader
{
    // The three sorts' indexes, by EF's names (TaskConfiguration; ListSortColumns states each sort's column).
    private const string StatusSortIndex = "SELECT * FROM dbo.[Task] WITH (INDEX(IX_Task_SpaceId_ProjectId_StatusId_Id))";

    private const string DueDateSortIndex = "SELECT * FROM dbo.[Task] WITH (INDEX(IX_Task_SpaceId_ProjectId_DueDate_Id))";

    private const string LabelSortIndex = "SELECT * FROM dbo.[Task] WITH (INDEX(IX_Task_SpaceId_ProjectId_FirstLabelName_Id))";

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> VisibleLabelIdsAsync(IReadOnlyCollection<Guid> labelIds, CancellationToken cancellationToken) =>
        labelIds.Count == 0
            ? []
            : await dbContext.Labels
                .AsNoTracking()
                .Where(label => labelIds.Contains(label.Id))
                .Select(label => label.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<int> CountAsync(Guid projectId, ListFilter filter, CancellationToken cancellationToken) =>
        filter.MatchesNothing
            ? 0
            : await Matching(dbContext.Tasks, projectId, filter).CountAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TaskItem>> PageAsync(
        Guid projectId,
        ListFilter filter,
        ListSort sort,
        IReadOnlyList<Guid> statusOrder,
        ListCursor? after,
        int take,
        CancellationToken cancellationToken)
    {
        if (filter.MatchesNothing)
        {
            return [];
        }

        return sort switch
        {
            // The Status filter is the order itself under this sort: each seek names one admitted Status by equality, so
            // the IN list is left out - beside an equality on the same column it only blurs the optimiser's estimate.
            ListSort.Status => await ByStatusAsync(Matching(Seeking(StatusSortIndex), projectId, filter with { StatusIds = null }), statusOrder, after, take, cancellationToken).ConfigureAwait(false),
            ListSort.DueDate => await ByDueDateAsync(Matching(Seeking(DueDateSortIndex), projectId, filter), filter, after, take, cancellationToken).ConfigureAwait(false),
            ListSort.Label => await ByLabelAsync(Matching(Seeking(LabelSortIndex), projectId, filter), filter, after, take, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Not an offered sort."),
        };
    }

    private static async Task<IReadOnlyList<TaskItem>> ByStatusAsync(
        IQueryable<TaskItem> matching,
        IReadOnlyList<Guid> statusOrder,
        ListCursor? after,
        int take,
        CancellationToken cancellationToken)
    {
        var rows = new List<TaskItem>(take);
        var next = 0;

        if (after is not null)
        {
            // A cursor naming a Status outside the order is not one this route issued for these Statuses: nothing follows it.
            var at = after.StatusId is { } cursorStatus ? IndexOf(statusOrder, cursorStatus) : -1;

            if (at < 0)
            {
                return rows;
            }

            var statusId = statusOrder[at];
            var afterId = after.TaskId;

            rows.AddRange(await matching
                .Where(task => task.StatusId == statusId && task.Id.CompareTo(afterId) > 0)
                .OrderBy(task => task.Id)
                .Take(take)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false));

            next = at + 1;
        }

        for (var index = next; index < statusOrder.Count && rows.Count < take; index++)
        {
            var statusId = statusOrder[index];

            rows.AddRange(await matching
                .Where(task => task.StatusId == statusId)
                .OrderBy(task => task.Id)
                .Take(take - rows.Count)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<TaskItem>> ByDueDateAsync(IQueryable<TaskItem> matching, ListFilter filter, ListCursor? after, int take, CancellationToken cancellationToken)
    {
        var rows = new List<TaskItem>(take);

        // "No due date" admits no dated Task, and a due range no undated one: a segment the filter empties is not read.
        var readsDated = !filter.DueNone && (after is null || after.DueDate is not null);
        var readsUndated = filter.DueFrom is null && filter.DueTo is null;

        if (readsDated)
        {
            rows.AddRange(await Dated(matching, after)
                .OrderBy(task => task.DueDate)
                .ThenBy(task => task.Id)
                .Take(take)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false));

            after = null;
        }

        if (rows.Count < take && readsUndated)
        {
            rows.AddRange(await Undated(matching, after)
                .OrderBy(task => task.Id)
                .Take(take - rows.Count)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<TaskItem>> ByLabelAsync(IQueryable<TaskItem> matching, ListFilter filter, ListCursor? after, int take, CancellationToken cancellationToken)
    {
        var rows = new List<TaskItem>(take);

        // A Label filter admits no unlabelled Task: that segment is not read.
        var readsUnlabelled = filter.LabelIds is null;

        if (after is null || after.FirstLabelName is not null)
        {
            rows.AddRange(await Labelled(matching, after)
                .OrderBy(task => task.FirstLabelName)
                .ThenBy(task => task.Id)
                .Take(take)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false));

            after = null;
        }

        if (rows.Count < take && readsUnlabelled)
        {
            rows.AddRange(await Unlabelled(matching, after)
                .OrderBy(task => task.Id)
                .Take(take - rows.Count)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false));
        }

        return rows;
    }

    /// <summary>
    /// The leading segment of the due-date sort: dated Tasks, after the cursor's pair when the cursor is in it.
    /// </summary>
    private static IQueryable<TaskItem> Dated(IQueryable<TaskItem> matching, ListCursor? after)
    {
        var dated = matching.Where(task => task.DueDate != null);

        if (after?.DueDate is { } value)
        {
            var afterId = after.TaskId;

            dated = dated.Where(task => task.DueDate > value || (task.DueDate == value && task.Id.CompareTo(afterId) > 0));
        }

        return dated;
    }

    /// <summary>
    /// The leading segment of the Label sort: labelled Tasks, after the cursor's pair when the cursor is in it.
    /// </summary>
    private static IQueryable<TaskItem> Labelled(IQueryable<TaskItem> matching, ListCursor? after)
    {
        var labelled = matching.Where(task => task.FirstLabelName != null);

        if (after?.FirstLabelName is { } value)
        {
            var afterId = after.TaskId;

            labelled = labelled.Where(task => string.Compare(task.FirstLabelName, value) > 0
                || (task.FirstLabelName == value && task.Id.CompareTo(afterId) > 0));
        }

        return labelled;
    }

    /// <summary>
    /// The trailing segment of the due-date sort: Tasks with no due date, after the cursor's id when the cursor is in it.
    /// </summary>
    private static IQueryable<TaskItem> Undated(IQueryable<TaskItem> matching, ListCursor? after)
    {
        var undated = matching.Where(task => task.DueDate == null);

        if (after is not null)
        {
            var afterId = after.TaskId;

            undated = undated.Where(task => task.Id.CompareTo(afterId) > 0);
        }

        return undated;
    }

    /// <summary>
    /// The trailing segment of the Label sort: Tasks with no Label, after the cursor's id when the cursor is in it.
    /// </summary>
    private static IQueryable<TaskItem> Unlabelled(IQueryable<TaskItem> matching, ListCursor? after)
    {
        var unlabelled = matching.Where(task => task.FirstLabelName == null);

        if (after is not null)
        {
            var afterId = after.TaskId;

            unlabelled = unlabelled.Where(task => task.Id.CompareTo(afterId) > 0);
        }

        return unlabelled;
    }

    private static int IndexOf(IReadOnlyList<Guid> statusOrder, Guid statusId)
    {
        for (var index = 0; index < statusOrder.Count; index++)
        {
            if (statusOrder[index] == statusId)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>
    /// The Task set read through one sort's own index (story 2.10, AC6): raw SQL composed onto <c>dbContext.Tasks</c>, so
    /// the query filter and the security policy both still narrow it (AD-2), with an <c>INDEX</c> hint naming the sort's
    /// composite index. AC6 makes the index the sort's contract, so the page statement names it rather than leaving it to
    /// a plan the cache may hold from another Space's parameters. Measured (<c>ListPagingTests</c> LV-18, 5,000 Tasks):
    /// with the hint, the filtered due-date and Label pages read 290-580 logical pages against 555-680 without it, and the
    /// unfiltered pages are flat from first to last. One shape stays the optimiser's: under a Label filter, the Status
    /// sort's first statement may drive from <c>TaskLabel</c>'s <c>(SpaceId, LabelId, TaskId)</c> index into this one and
    /// sort - about 13,000 reads at 5,000 Tasks, every seek inside the Space - which the Nightly timing holds to NFR-5.
    /// </summary>
    private IQueryable<TaskItem> Seeking(string indexHint) => dbContext.Tasks.FromSqlRaw(indexHint);

    /// <summary>
    /// The Project's Tasks matching <paramref name="filter"/> - the predicate both the count and every page read.
    /// </summary>
    private IQueryable<TaskItem> Matching(IQueryable<TaskItem> source, Guid projectId, ListFilter filter)
    {
        var tasks = source
            .AsNoTracking()
            .Where(task => task.ProjectId == projectId);

        if (filter.StatusIds is { } statusIds)
        {
            tasks = tasks.Where(task => statusIds.Contains(task.StatusId));
        }

        if (filter.LabelIds is { } labelIds)
        {
            var applications = dbContext.Set<TaskLabel>();

            tasks = tasks.Where(task => applications.Any(application => application.TaskId == task.Id && labelIds.Contains(application.LabelId)));
        }

        if (filter.DueNone)
        {
            tasks = tasks.Where(task => task.DueDate == null);
        }

        if (filter.DueFrom is { } from)
        {
            tasks = tasks.Where(task => task.DueDate >= from);
        }

        if (filter.DueTo is { } to)
        {
            tasks = tasks.Where(task => task.DueDate < to);
        }

        return tasks;
    }
}
