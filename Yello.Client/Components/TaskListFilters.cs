using Yello.Client.Spaces;
using Yello.Contracts.Lists;

namespace Yello.Client.Components;

/// <summary>
/// The List View's filters and sort as the person has set them (story 2.10, open questions 4 (a), 5 (a), 7 (a)): this
/// tab's state only, never the URL's and never browser storage, and cleared with the Space (AC17, UX-DR31, UX-DR33).
/// </summary>
/// <remarks>
/// <b>A due range is two days chosen in the viewer's zone</b>, sent as the start of the first and the start of the day
/// after the second - each an instant with its offset, story 2.3's rule - so the range is the whole of both days. A bound
/// left empty is not sent.
/// </remarks>
public sealed class TaskListFilters
{
    /// <summary>
    /// The due-date choice: every Task.
    /// </summary>
    public static string DueAny => "any";

    /// <summary>
    /// The due-date choice: Tasks with no due date.
    /// </summary>
    public static string DueNone => "none";

    /// <summary>
    /// The due-date choice: a day range.
    /// </summary>
    public static string DueRange => "range";

    private const int EarliestYear = 2000;

    private const int LatestYear = 2100;

    /// <summary>
    /// The chosen Statuses; any of them.
    /// </summary>
    public HashSet<Guid> Statuses { get; } = [];

    /// <summary>
    /// The chosen Labels; any of them.
    /// </summary>
    public HashSet<Guid> Labels { get; } = [];

    /// <summary>
    /// The sort code; Status is the view's initial order.
    /// </summary>
    public string Sort { get; set; } = ListSorts.Status;

    /// <summary>
    /// The due-date choice.
    /// </summary>
    public string Due { get; set; } = DueAny;

    /// <summary>
    /// The range's first day, <c>yyyy-MM-dd</c>, or empty.
    /// </summary>
    public string DueFrom { get; set; } = string.Empty;

    /// <summary>
    /// The range's last day, <c>yyyy-MM-dd</c>, or empty.
    /// </summary>
    public string DueTo { get; set; } = string.Empty;

    /// <summary>
    /// Whether any filter is set: an empty answer is then "No Tasks match." rather than the empty Project's copy.
    /// </summary>
    /// <remarks>
    /// "Between dates" with neither day chosen sends no bound, so it filters nothing.
    /// </remarks>
    public bool Filtered => Statuses.Count > 0 || Labels.Count > 0 || DueFiltered;

    private bool DueFiltered => Due == DueNone || (Due == DueRange && ToQuery() is { DueFrom: not null } or { DueTo: not null });

    /// <summary>
    /// Adds <paramref name="id"/> to <paramref name="chosen"/>, or takes it out if it is there.
    /// </summary>
    /// <param name="chosen">The Statuses or the Labels.</param>
    /// <param name="id">The one toggled.</param>
    public static void Toggle(HashSet<Guid> chosen, Guid id)
    {
        ArgumentNullException.ThrowIfNull(chosen);

        if (!chosen.Remove(id))
        {
            chosen.Add(id);
        }
    }

    /// <summary>
    /// Back to no filter and the initial sort.
    /// </summary>
    public void Clear()
    {
        Statuses.Clear();
        Labels.Clear();
        Sort = ListSorts.Status;
        Due = DueAny;
        DueFrom = string.Empty;
        DueTo = string.Empty;
    }

    /// <summary>
    /// The sort and filters as the List route reads them.
    /// </summary>
    /// <returns>The query.</returns>
    public ListQuery ToQuery()
    {
        DateTimeOffset? from = null;
        DateTimeOffset? to = null;

        if (Due == DueRange)
        {
            from = TaskDates.TryFromInputValue(DueFrom, out var start) ? start : null;
            to = DayAfter(DueTo);
        }

        return new ListQuery(Sort, [.. Statuses], [.. Labels], from, to, Due == DueNone);
    }

    /// <summary>
    /// The start, in the viewer's zone, of the day after <paramref name="value"/> - the range's exclusive end.
    /// </summary>
    private static DateTimeOffset? DayAfter(string value)
    {
        if (!TaskDates.TryFromInputValue(value, out var day) || day is not { } last)
        {
            return null;
        }

        var next = DateOnly.FromDateTime(last.DateTime).AddDays(1);

        if (next.Year is < EarliestYear or > LatestYear)
        {
            return null;
        }

        var startOfDay = next.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        return new DateTimeOffset(startOfDay, TimeZoneInfo.Local.GetUtcOffset(startOfDay));
    }
}
