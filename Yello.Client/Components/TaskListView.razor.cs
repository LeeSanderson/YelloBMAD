using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Yello.Client.Spaces;
using Yello.Contracts;
using Yello.Contracts.Labels;
using Yello.Contracts.Lists;
using Yello.Contracts.Tasks;

namespace Yello.Client.Components;

/// <summary>
/// The List View's state: what was read, the filters and sort that read it, the roving row, and the reads themselves.
/// Story 2.10; see the component's markup for the behaviour it renders.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two counters drop a stale answer</b>: <c>_generation</c> is bumped by a purge and by disposal, and <c>_read</c> by
/// every new list (a filter or sort change). An answer that started before either moved is dropped - a superseded filter's
/// page never lands on the list that replaced it (AC9).
/// </para>
/// <para>
/// <b>A page appends, never replaces</b> (AD-29, AC18): rows already drawn stay where they are, the same elements for the
/// same Tasks. A Task a concurrent edit moved past the cursor may come back in a later page; it is drawn once.
/// </para>
/// </remarks>
public sealed partial class TaskListView
{

    private readonly TaskListFilters _filters = new();

    private IDisposable? _purgeRegistration;
    private ListKeys? _keys;
    private ElementReference _listElement;

    private int _generation;
    private int _read;

    private List<ListStatusResponse>? _statuses;
    private IReadOnlyList<LabelResponse> _labels = [];
    private Dictionary<Guid, LabelResponse> _labelsById = [];
    private string _projectName = string.Empty;

    private List<TaskResponse>? _rows;
    private int _total;
    private string? _after;
    private bool _loading;


    private string? _announcement;
    private int _announcements;

    private Guid? _activeTaskId;
    private Guid? _focusTaskId;

    /// <summary>
    /// The Space the Project is in.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public Guid SpaceId { get; set; }

    /// <summary>
    /// The Project whose List View this is.
    /// </summary>
    [Parameter]
    [EditorRequired]
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Raised for a refused read - a <c>404</c> (the Project matches nothing in the Space, or the Space is refused) or a
    /// <c>400</c> the client should never have caused - so the page renders its one refusal surface.
    /// </summary>
    [Parameter]
    public EventCallback<ProblemResponse?> OnRefused { get; set; }

    private string DueId => $"list_due_{ProjectId:N}";

    private string DueFromId => $"list_due_from_{ProjectId:N}";

    private string DueToId => $"list_due_to_{ProjectId:N}";

    private string SortId => $"list_sort_{ProjectId:N}";

    /// <summary>
    /// The row that is the one tab stop: the active row while it is loaded, else the first.
    /// </summary>
    private Guid? ActiveTaskId
    {
        get
        {
            if (_rows is not { Count: > 0 } rows)
            {
                return null;
            }

            return _activeTaskId is { } active && rows.Exists(row => row.Id == active) ? active : rows[0].Id;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _generation++;
        _purgeRegistration?.Dispose();
        ActiveSpace.ClearProject();

        if (_keys is not null)
        {
            await _keys.DisposeAsync();
        }
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        _keys = new ListKeys(JS, KeyAsync, Focused);
        _purgeRegistration = ActiveSpace.RegisterPurge(Purge);

        await LoadAsync(_generation);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_keys is null || _statuses is null)
        {
            return;
        }

        await _keys.AttachAsync(_listElement);

        if (_focusTaskId is { } taskId && _rows is { } rows && rows.Exists(row => row.Id == taskId))
        {
            _focusTaskId = null;
            await _keys.FocusAsync(taskId);
        }
    }

    /// <summary>
    /// The Space is being left: everything about this list goes, synchronously, before any render (AC17, UX-DR31).
    /// </summary>
    private void Purge()
    {
        _generation++;
        _read++;
        _statuses = null;
        _labels = [];
        _labelsById = [];
        _projectName = string.Empty;
        _rows = null;
        _total = 0;
        _after = null;
        _loading = false;
        _filters.Clear();
        _activeTaskId = null;
        _focusTaskId = null;
        Say(null);
    }

    /// <summary>
    /// The cold load: the Space's Labels first - a row names the Labels it carries from them - then the first page.
    /// </summary>
    private async Task LoadAsync(int generation)
    {
        var token = ActiveSpace.Token;

        try
        {
            using var labels = await Http.GetAsync(new Uri(LabelRoutes.LabelsOf(SpaceId), UriKind.Relative), token);

            if (generation != _generation || token.IsCancellationRequested)
            {
                return;
            }

            if (!labels.IsSuccessStatusCode)
            {
                await RefuseAsync(labels, token);
                return;
            }

            var spaceLabels = await labels.Content.ReadFromJsonAsync<List<LabelResponse>>(token) ?? [];

            if (await ReadPageAsync(generation, _read, after: null, token) is not { } page || !ActiveSpace.ShowProject(SpaceId, page.Project.Name))
            {
                return;
            }

            _labels = spaceLabels;
            _labelsById = spaceLabels.ToDictionary(label => label.Id);
            _projectName = page.Project.Name;
            _statuses = [.. page.Statuses];
            Take(page, append: false);

            // Said once, on the cold load only (UX-DR35): a page appended or a list read again says nothing else (AC19).
            Say(Localizer["ListLoaded"]);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The tab left this Space, or the Session ended: both purges have run.
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or JsonException)
        {
            await RefuseUnlessLeftAsync(generation);
        }
    }

    /// <summary>
    /// A new list for the changed filters or sort (AC9): the rows go at once - removed, never re-pointed - the first page is
    /// read, and its true total is said once it settles. A later change supersedes this one's answer.
    /// </summary>
    private async Task RereadAsync()
    {
        var generation = _generation;
        var read = ++_read;
        var token = ActiveSpace.Token;

        _rows = null;
        _after = null;
        _loading = false;
        _activeTaskId = null;
        _focusTaskId = null;

        try
        {
            if (await ReadPageAsync(generation, read, after: null, token) is { } page)
            {
                Take(page, append: false);
                Say(CountSentence(page.Total));
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Left: the purge has run.
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or JsonException)
        {
            await RefuseUnlessLeftAsync(generation);
        }
    }

    /// <summary>
    /// The next page after the cursor, appended (AC7, AC18). From the keyboard or the button, focus lands on its first Task
    /// by identity; nothing is said (AC19). A second request while one is out is ignored.
    /// </summary>
    private Task ShowMoreAsync() =>
        _loading || _after is not { } after || _rows is null ? Task.CompletedTask : ReadMoreAsync(after, _rows.Count);

    private async Task ReadMoreAsync(string after, int loaded)
    {
        var generation = _generation;
        var read = _read;
        var token = ActiveSpace.Token;

        _loading = true;

        try
        {
            if (await ReadPageAsync(generation, read, after, token) is { } page)
            {
                Take(page, append: true);
                FocusFirstAppended(loaded);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Left: the purge has run.
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpRequestException or JsonException)
        {
            await RefuseUnlessLeftAsync(generation);
        }
        finally
        {
            if (generation == _generation && read == _read)
            {
                _loading = false;
            }
        }
    }

    /// <summary>
    /// Focus to the first row a page appended, by its Task's identity (AC18).
    /// </summary>
    private void FocusFirstAppended(int loaded)
    {
        if (_rows is { } rows && rows.Count > loaded)
        {
            _activeTaskId = rows[loaded].Id;
            _focusTaskId = rows[loaded].Id;
        }
    }

    /// <summary>
    /// One page of the current query, or <c>null</c> when it was refused (the refusal surface has been raised) or superseded.
    /// </summary>
    private async Task<ListPageResponse?> ReadPageAsync(int generation, int read, string? after, CancellationToken token)
    {
        var query = _filters.ToQuery();
        var path = after is null
            ? ListRoutes.ListOf(SpaceId, ProjectId, query)
            : ListRoutes.ListOf(SpaceId, ProjectId, query, after);

        using var response = await Http.GetAsync(new Uri(path, UriKind.Relative), token);

        if (generation != _generation || read != _read || token.IsCancellationRequested)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            await RefuseAsync(response, token);
            return null;
        }

        var page = await response.Content.ReadFromJsonAsync<ListPageResponse>(token);

        return generation == _generation && read == _read ? page : null;
    }

    private async Task RefuseAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            await OnRefused.InvokeAsync(await SpaceWrites.ProblemOfAsync(response, token));
        }
    }

    private async Task RefuseUnlessLeftAsync(int generation)
    {
        if (generation == _generation)
        {
            await OnRefused.InvokeAsync(null);
        }
    }

    /// <summary>
    /// A page's rows into the list - its total and cursor too. Appended rows skip a Task already drawn.
    /// </summary>
    private void Take(ListPageResponse page, bool append)
    {
        _total = page.Total;
        _after = page.NextAfter;

        if (!append || _rows is null)
        {
            _rows = [.. page.Tasks];
            return;
        }

        var drawn = _rows.Select(row => row.Id).ToHashSet();

        _rows.AddRange(page.Tasks.Where(task => drawn.Add(task.Id)));
    }

    private Task ToggleStatusAsync(Guid statusId)
    {
        TaskListFilters.Toggle(_filters.Statuses, statusId);

        return RereadAsync();
    }

    private Task ToggleLabelAsync(Guid labelId)
    {
        TaskListFilters.Toggle(_filters.Labels, labelId);

        return RereadAsync();
    }

    private Task ChangeDueAsync(ChangeEventArgs change)
    {
        _filters.Due = change.Value as string ?? TaskListFilters.DueAny;

        return RereadAsync();
    }

    private Task ChangeDueFromAsync(ChangeEventArgs change)
    {
        _filters.DueFrom = change.Value as string ?? string.Empty;

        return RereadAsync();
    }

    private Task ChangeDueToAsync(ChangeEventArgs change)
    {
        _filters.DueTo = change.Value as string ?? string.Empty;

        return RereadAsync();
    }

    private Task ChangeSortAsync(ChangeEventArgs change)
    {
        _filters.Sort = change.Value as string ?? ListSorts.Status;

        return RereadAsync();
    }

    /// <summary>
    /// A key on a row's stop (Task 6): another loaded row by identity, or the next page.
    /// </summary>
    private async Task KeyAsync(string key, Guid? taskId)
    {
        if (_rows is not { } rows || taskId is not { } id)
        {
            return;
        }

        var (step, index) = ListTraversal.Next(rows.Count, rows.FindIndex(row => row.Id == id), key, _after is not null, _loading);

        if (step == ListStep.Move)
        {
            _activeTaskId = rows[index].Id;
            _focusTaskId = rows[index].Id;
        }
        else if (step == ListStep.LoadMore)
        {
            await ShowMoreAsync();
        }
        else
        {
            return;
        }

        StateHasChanged();
    }

    /// <summary>
    /// Focus moved into a row's stop, by whatever route: that row becomes the one tab stop.
    /// </summary>
    private void Focused(Guid? taskId)
    {
        if (taskId is { } id && id != _activeTaskId)
        {
            _activeTaskId = id;
            StateHasChanged();
        }
    }

    private bool Loaded => _statuses is not null;

    /// <summary>
    /// Says a sentence politely - the same one again included - or, with <c>null</c>, clears the regions.
    /// </summary>
    private void Say(string? words)
    {
        _announcement = words;
        _announcements++;
    }

    private string CountSentence(int total) => total switch
    {
        0 => Localizer["ListMatchNone"],
        1 => Localizer["ListMatchOne"],
        _ => string.Format(CultureInfo.CurrentCulture, Localizer["ListMatchMany"], total),
    };

    private string StatusName(Guid statusId) =>
        _statuses?.Find(status => status.Id == statusId)?.Name ?? string.Empty;
}
