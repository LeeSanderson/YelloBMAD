using Yello.Domain.Boards;
using Yello.Domain.Lists;
using Yello.Domain.Projects;
using Yello.Domain.Statuses;
using Yello.Domain.Tasks;

namespace Yello.Application.Lists.ViewList;

/// <summary>
/// Reads one page of a Project's List View: the Project's Tasks matching a filter, under a sort, after a cursor, with the
/// true filtered total and the Project's effective Statuses. FR-30 (partial: no Assignee until 4.6); story 2.10, AC1,
/// AC3-AC5, AC10-AC15, open questions 1 (a), 4 (a) and 7 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>No Role, no Space, no lock</b> (AR-3). The pipeline resolved the Space and checked the read capability every Role
/// holds, so a Viewer is served exactly a Member's List (AC16). Nothing here writes.
/// </para>
/// <para>
/// <b>Filter ids are narrowed before any read, never refused</b> (AC15, open question 4 (a)): Status ids to the Project's
/// effective set, Label ids to the Space's Labels. An id outside them - random, another Space's real one, deleted since it
/// was chosen - drops out, and a filter left with none matches nothing. So a random id and another Space's real id answer
/// the same bytes, and a stale Label never breaks a filter.
/// </para>
/// <para>
/// <b>The Status sort is the effective order, applied here</b> (AD-16, AC14, open question 7 (a)): effective order is
/// never stored, so the reader is handed the Statuses in that order and seeks each in turn.
/// </para>
/// <para>
/// <b>Statements, not one snapshot</b> (story 2.9, open question 10 (a)): under <c>READ_COMMITTED_SNAPSHOT</c> the
/// Statuses, the narrowing, the total, the page and its Labels each read their own committed snapshot, never waiting on a
/// writer. They may disagree by the writes that commit between them - bounded to concurrent change, and corrected by the
/// next read.
/// </para>
/// </remarks>
/// <param name="boards">Reads the Project and the page's Labels, as the Board does.</param>
/// <param name="reader">Reads the List's count and page.</param>
/// <param name="statuses">Reads the Space's Status definitions.</param>
public sealed class ViewListHandler(IBoardReader boards, IListReader reader, IStatusDefinitionReader statuses)
{
    /// <summary>
    /// Reads the page.
    /// </summary>
    /// <param name="projectId">The Project, from the route.</param>
    /// <param name="sort">The sort.</param>
    /// <param name="filter">The filter as sent - ids not yet narrowed.</param>
    /// <param name="after">The cursor, already read for this sort, or <c>null</c> for the first page.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The page, or <c>null</c> when no Project in the active Space has that id.</returns>
    public Task<ListView?> HandleAsync(Guid projectId, ListSort sort, ListFilter filter, ListCursor? after, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return ReadAsync(projectId, sort, filter, after, cancellationToken);
    }

    private async Task<ListView?> ReadAsync(Guid projectId, ListSort sort, ListFilter filter, ListCursor? after, CancellationToken cancellationToken)
    {
        if (await boards.FindProjectAsync(projectId, cancellationToken).ConfigureAwait(false) is not { } project)
        {
            return null;
        }

        var effective = EffectiveStatusSet.Of(await statuses.ListAsync(cancellationToken).ConfigureAwait(false));

        var narrowed = filter with
        {
            StatusIds = filter.StatusIds is { } statusIds
                ? [.. statusIds.Distinct().Where(statusId => EffectiveStatusSet.Contains(effective, statusId))]
                : null,
            LabelIds = filter.LabelIds is { Count: > 0 } labelIds
                ? await reader.VisibleLabelIdsAsync([.. labelIds.Distinct()], cancellationToken).ConfigureAwait(false)
                : filter.LabelIds,
        };

        var statusOrder = effective
            .Select(status => status.Id)
            .Where(statusId => narrowed.StatusIds is null || narrowed.StatusIds.Contains(statusId))
            .ToList();

        var total = await reader.CountAsync(projectId, narrowed, cancellationToken).ConfigureAwait(false);
        var (tasks, nextAfter) = await ListPage.ReadAsync(reader, projectId, narrowed, sort, statusOrder, after, cancellationToken).ConfigureAwait(false);
        var labels = await boards.LabelIdsOfAsync([.. tasks.Select(task => task.Id)], cancellationToken).ConfigureAwait(false);

        return new ListView(project, effective, total, tasks, nextAfter, labels);
    }
}

/// <summary>
/// One page of a List View.
/// </summary>
/// <param name="Project">The Project.</param>
/// <param name="Statuses">The Project's effective Statuses, in effective order - the filter's choices and the rows' Status names.</param>
/// <param name="Total">The true number of the Project's Tasks matching the filter, counted by the database.</param>
/// <param name="Tasks">The page's Tasks, in the sort's order.</param>
/// <param name="NextAfter">The cursor when another Task follows, otherwise <c>null</c>.</param>
/// <param name="LabelIds">The Labels applied to each of the page's Tasks, in chip order; a Task carrying none is absent.</param>
public sealed record ListView(
    Project Project,
    IReadOnlyList<StatusDefinition> Statuses,
    int Total,
    IReadOnlyList<TaskItem> Tasks,
    string? NextAfter,
    IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> LabelIds);
