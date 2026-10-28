using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Yello.Client.Spaces;
using Yello.Contracts.Boards;
using Yello.Contracts.Tasks;

namespace Yello.Client.Components;

/// <summary>
/// The Board at five thousand Tasks (story 2.9): each column is a contiguous loaded prefix, read a page at a time, appended
/// and never recycled. AC3-AC7, AC10-AC12; AD-29, AR-22; UX-DR24, UX-DR26, UX-DR38, UX-DR39; open questions 2-8.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why append and never recycle</b> (AC3): a recycled row re-points keyboard focus at a different Task, so the next
/// keyboard move operates on the wrong one - a data-corrupting defect reachable only by keyboard and invisible to pointer
/// testing. Every card is keyed by its Task's id, a page only ever adds cards (or moves the card of a Task it holds), and
/// no element is reused for another Task, so focus following the element is focus following the Task.
/// </para>
/// <para>
/// <b>The loaded prefix</b> (AC10): a column whose cursor is not <c>null</c> draws exactly the Tasks up to and including
/// the cursor's key, in key order. A Task whose key sorts after the cursor - a creation, a Status edit (it lands last), a
/// card a refusal sends back past it - is held out: counted in the column's total at once, drawn only when paging reaches
/// it. So every drawn card's <c>aria-posinset</c> (its index + 1) is its true ordinal. A move placed directly after the
/// last loaded card is drawn at once, and its admitted key becomes the cursor - nothing lies between the old cursor and it.
/// </para>
/// <para>
/// <b>Newer read wins, never twice</b> (AC5, open question 4 (a)): a page holding a Task already drawn moves that card to
/// the page's place - the same element, the same Task - unless this tab's own write for it is unsettled, when the replica
/// wins. A page's total is adopted only when no write touching the column is unsettled (open question 2 (a)).
/// </para>
/// <para>
/// <b>Nothing is announced on loading or windowing</b> (AC6, UX-DR38): a page fetch, an append, "Show more", a cursor
/// advance and a re-read say nothing. The cold load's one "Board loaded." stays (open question 8 (a)).
/// </para>
/// </remarks>
public sealed partial class BoardView
{
    // The Tasks this tab counts in a partly loaded column without drawing them (held out past the cursor), by the Status
    // whose total counts them. A panel may be open on one (AC12).
    private readonly Dictionary<Guid, Guid> _heldOut = [];

    // Where each drawn Task's card was last found: checked against the column before use and rebuilt when stale, so finding
    // a card among 5,000 is a lookup, not a scan of every column (story 2.9, open question 12 (a)).
    private Dictionary<Guid, (ColumnState Column, int Index)> _cardIndex = [];

    /// <summary>
    /// Whether a key falls inside a column's loaded prefix: the column is fully loaded, or the key sorts at or before its
    /// cursor (ordinal, as the server's BIN2 seek compares).
    /// </summary>
    private static bool Fits(ColumnState column, string key) =>
        column.After is not { } after || string.CompareOrdinal(key, after) <= 0;

    /// <summary>
    /// Appends the next page of a partly loaded column, unless one is already out for it (the keyboard drives the window,
    /// and never queues past the boundary). Returns whether a page was appended; nothing is announced (AC6).
    /// </summary>
    private async Task<bool> LoadMoreAsync(ColumnState column)
    {
        if (column.After is not { } after || column.Loading)
        {
            return false;
        }

        column.Loading = true;

        try
        {
            // Purged, read again or advanced meanwhile: a page is used only while it is still the one the column needs.
            if (await ReadPageAsync(column.StatusId, after) is not { } page || !ReferenceEquals(Column(column.StatusId), column) || column.After != after)
            {
                return false;
            }

            Append(column, page.Tasks, page.NextAfter, page.Total);

            return true;
        }
        finally
        {
            column.Loading = false;
        }
    }

    /// <summary>
    /// The page of a column after a cursor, or <c>null</c> when it was refused, unanswered or unreadable - the column then
    /// keeps its cursor, and the next route to it asks again - or the tab left the Space meanwhile. A <c>404</c> raises the
    /// page's one refusal surface, as the Board's own read does: the Space, the Project or the Status is not there (story
    /// 2.9 code review - the control used to stay, doing nothing).
    /// </summary>
    private async Task<BoardColumnPageResponse?> ReadPageAsync(Guid statusId, string after)
    {
        var generation = _generation;
        var token = ActiveSpace.Token;

        try
        {
            using var response = await Http.GetAsync(new Uri(BoardRoutes.ColumnOf(SpaceId, ProjectId, statusId, after), UriKind.Relative), token);

            if (generation != _generation || token.IsCancellationRequested)
            {
                return null;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                await OnRefused.InvokeAsync(await SpaceWrites.ProblemOfAsync(response, token));
                return null;
            }

            return response.IsSuccessStatusCode
                ? await response.Content.ReadFromJsonAsync<BoardColumnPageResponse>(JsonOptions, token)
                : null;
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// One page into a column (AC10): each Task appended at the column's end - or, when this tab already draws it, its card
    /// moved there (newer read wins) unless its own write is unsettled - then the cursor, then the total when nothing of
    /// this tab's is unsettled in the column.
    /// </summary>
    private void Append(ColumnState column, IReadOnlyList<TaskResponse> tasks, string? nextAfter, int total)
    {
        // One index of every drawn card for the whole page: a page's Tasks are mostly not drawn yet, and a miss must not
        // rebuild the index fifty times over thousands of cards (measured: 3.7 s per page appended at 5,000).
        RebuildCardIndex();

        foreach (var task in tasks)
        {
            if (!_deleted.Contains(task.Id) && Drawn(task, column) is { } drawn)
            {
                column.Tasks.Add(drawn);
            }
        }

        column.After = nextAfter;

        if (!WriteUnsettledIn(column.StatusId))
        {
            column.Total = total;
        }
    }

    /// <summary>
    /// What a page's Task draws at the column's end, or <c>null</c> when this tab's unsettled write keeps it where it is: a
    /// card already drawn is taken from its place (newer read wins, never twice); a Task held out is released from the count
    /// that held it.
    /// </summary>
    private TaskResponse? Drawn(TaskResponse task, ColumnState column)
    {
        var replica = Unsettled(task.Id);

        if (Locate(task.Id, rebuildOnMiss: false) is { } found)
        {
            return replica is null ? TakenFrom(found, column, task) : null;
        }

        if (!_heldOut.TryGetValue(task.Id, out var countedIn))
        {
            return task;
        }

        return replica is not null && countedIn != column.StatusId ? null : Released(task.Id, countedIn, column, replica?.Displayed() ?? task);
    }

    /// <summary>
    /// A card already drawn, taken from its place for the page's: its old column's count loses it when that is another.
    /// </summary>
    private static TaskResponse TakenFrom((ColumnState Column, int Index) found, ColumnState column, TaskResponse task)
    {
        found.Column.Tasks.RemoveAt(found.Index);

        if (!ReferenceEquals(found.Column, column))
        {
            found.Column.Total--;
        }

        return task;
    }

    /// <summary>
    /// A held-out Task the page reaches: no longer held, and no longer counted in another column.
    /// </summary>
    private TaskResponse Released(Guid taskId, Guid countedIn, ColumnState column, TaskResponse drawn)
    {
        _heldOut.Remove(taskId);

        if (countedIn != column.StatusId && Column(countedIn) is { } counted)
        {
            counted.Total--;
        }

        return drawn;
    }

    /// <summary>
    /// The Task's replica while one of its writes is out or waiting; otherwise <c>null</c>.
    /// </summary>
    private TaskReplica? Unsettled(Guid taskId) =>
        _replicas.TryGetValue(taskId, out var replica) && (replica.InFlight.Count > 0 || replica.Queued.Count > 0) ? replica : null;

    /// <summary>
    /// Whether any of this tab's writes touching a column - a Task shown in it or leaving it - is still unsettled.
    /// </summary>
    private bool WriteUnsettledIn(Guid statusId) =>
        _replicas.Values.Any(replica =>
            (replica.InFlight.Count > 0 || replica.Queued.Count > 0)
            && (replica.Admitted.StatusId == statusId || replica.Displayed().StatusId == statusId));

    /// <summary>
    /// The column of a Status, or <c>null</c>.
    /// </summary>
    private ColumnState? Column(Guid statusId) => _columns?.Find(column => column.StatusId == statusId);

    /// <summary>
    /// Takes a Task off the Board's cards and counts it in <paramref name="column"/> without drawing it: its key sorts after
    /// the column's cursor (AC10).
    /// </summary>
    private void HoldOut(Guid taskId, ColumnState column) => _heldOut[taskId] = column.StatusId;

    /// <summary>
    /// "Show more" at a partly loaded column's foot (AC11, open question 5 (a)): the next page is appended and focus moves to
    /// its first Task, by identity; nothing is announced.
    /// </summary>
    private async Task ShowMoreAsync(Guid statusId)
    {
        if (Column(statusId) is not { } column)
        {
            return;
        }

        var before = column.Tasks.Count;

        if (await LoadMoreAsync(column) && column.Tasks.Count > before)
        {
            var first = column.Tasks[before].Id;

            _activeTaskId = first;
            _activeColumnId = null;
            FocusCardTitle(first);
        }

        StateHasChanged();
    }

    /// <summary>
    /// The Move picker's "Show more positions" (AC11): the column's next page, loaded into the Board's own column state -
    /// one data source - so the picker's list grows with it.
    /// </summary>
    private async Task MorePositionsAsync(Guid statusId)
    {
        if (Column(statusId) is { } column)
        {
            await LoadMoreAsync(column);
            StateHasChanged();
        }
    }

    /// <summary>
    /// After the Board is read again (open question 3 (a)): every column holds its first page, and the column that holds the
    /// focused, active or open Task is paged until that Task is drawn again, so focus never loses its identity (AC7).
    /// </summary>
    private async Task RestoreDepthAsync(Guid? taskId, Guid? statusId, int generation)
    {
        if (taskId is not { } id || statusId is not { } status || Column(status) is not { } column)
        {
            return;
        }

        while (generation == _generation && Locate(id) is null && column.After is not null && await LoadMoreAsync(column))
        {
            // One page at a time, until the Task is drawn or the column has no more.
        }
    }

    /// <summary>
    /// A Board read again (story 2.6, open question 3 (a); story 2.9, open question 3 (a)). A drag or a carry under it ends
    /// with nothing moved: the slots it was over may be gone (story 2.7 AC13). Each column takes the server's first page,
    /// cursor and total; the column holding the carried, active or focused Task is paged until that Task is drawn again; the
    /// replicas are re-admitted; and where a carried Task is now is said once the Board is drawn again (story 2.8, open
    /// question 10). Nothing else is announced (AC6).
    /// </summary>
    private async Task UseRereadBoardAsync(BoardResponse board, int generation)
    {
        var held = EndHeld();
        var kept = held ?? _activeTaskId ?? _focusCard;
        var keptIn = kept is { } keptId ? CountedIn(keptId) : null;

        _columns = [.. board.Columns.Select(column => new ColumnState(column.StatusId, column.Name, column.Total, [.. column.Tasks.Where(task => !_deleted.Contains(task.Id))], column.NextAfter))];
        _heldOut.Clear();
        UseStatuses(board.Columns.Select(column => new TaskStatusOption(column.StatusId, column.Name)));
        await RestoreDepthAsync(kept, keptIn, generation);

        if (generation != _generation)
        {
            return;
        }

        Readmit();
        KeepFocusIdentity(kept, keptIn);
        Returned(held);
    }

    /// <summary>
    /// After a re-read, a kept Task that could not be drawn again sends focus to the column that held it, never to the page
    /// body (story 2.9, AC12) - unless a panel is open, which keeps focus.
    /// </summary>
    private void KeepFocusIdentity(Guid? taskId, Guid? statusId)
    {
        if (taskId is not { } id || statusId is not { } status || _openTaskId is not null)
        {
            return;
        }

        if (Locate(id) is not null || Column(status) is null)
        {
            return;
        }

        if (_activeTaskId == id)
        {
            _activeTaskId = null;
            _activeColumnId = status;
        }

        _focusColumn = status;
        _focusColumnRequests++;
    }

    /// <summary>
    /// Where a Task is counted: the column that draws its card, or the one holding it out.
    /// </summary>
    private Guid? CountedIn(Guid taskId)
    {
        if (Locate(taskId) is { } found)
        {
            return found.Column.StatusId;
        }

        return _heldOut.TryGetValue(taskId, out var statusId) ? statusId : null;
    }

    /// <summary>
    /// The card of a Task: its column and index, from the index when it is still right, otherwise from a rebuilt index.
    /// </summary>
    private (ColumnState Column, int Index)? Locate(Guid taskId) => Locate(taskId, rebuildOnMiss: true);

    /// <summary>
    /// <see cref="Locate(Guid)"/>; with <paramref name="rebuildOnMiss"/> false, a Task the index does not hold is taken as
    /// not drawn - right only straight after <see cref="RebuildCardIndex"/>. An entry that has moved is still found.
    /// </summary>
    private (ColumnState Column, int Index)? Locate(Guid taskId, bool rebuildOnMiss)
    {
        if (_cardIndex.TryGetValue(taskId, out var known))
        {
            if (StillAt(known, taskId))
            {
                return known;
            }
        }
        else if (!rebuildOnMiss)
        {
            return null;
        }
        else
        {
            // Not indexed: drawn since the index was built, or not drawn at all.
        }

        RebuildCardIndex();

        return _cardIndex.TryGetValue(taskId, out known) && StillAt(known, taskId) ? known : null;
    }

    /// <summary>
    /// Indexes every drawn card by its Task.
    /// </summary>
    private void RebuildCardIndex()
    {
        _cardIndex = [];

        foreach (var column in _columns ?? [])
        {
            for (var index = 0; index < column.Tasks.Count; index++)
            {
                _cardIndex[column.Tasks[index].Id] = (column, index);
            }
        }
    }

    private bool StillAt((ColumnState Column, int Index) known, Guid taskId) =>
        known.Index < known.Column.Tasks.Count
        && known.Column.Tasks[known.Index].Id == taskId
        && _columns is { } columns
        && columns.Contains(known.Column);
}
