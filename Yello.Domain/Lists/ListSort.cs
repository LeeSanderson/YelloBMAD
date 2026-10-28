namespace Yello.Domain.Lists;

/// <summary>
/// The orders a List View offers (FR-30; story 2.10, open questions 3 (a), 5 (a) and 7 (a)). Each is ascending, by a
/// non-unique sort value and then the Task's id - the pair keyset AR-22 requires - and each has its composite index.
/// </summary>
/// <remarks>
/// <b>No Assignee</b> (AC2): no Task holds one until FR-21 (Epic 4), and story 4.6 adds that sort with its index.
/// </remarks>
public enum ListSort
{
    /// <summary>
    /// The Project's effective Status order - the Board's column order (AD-16) - then the Task's id.
    /// </summary>
    Status,

    /// <summary>
    /// The due date, soonest first, then the Task's id; Tasks with no due date last (AC5).
    /// </summary>
    DueDate,

    /// <summary>
    /// The first Label's name in chip order (<see cref="Tasks.TaskItem.FirstLabelName"/>), then the Task's id; Tasks with
    /// no Label last (AC13).
    /// </summary>
    Label,
}
