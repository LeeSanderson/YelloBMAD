namespace Yello.Contracts.Lists;

/// <summary>
/// The List View's sort codes, the <c>sort</c> query value the client and the Host share (FR-30; story 2.10, Task 4 (b)).
/// Stable strings: a code is never renamed or reused (AD-19).
/// </summary>
/// <remarks>
/// Every code here must have its composite index - <c>ListViewGateTests</c> fails the build otherwise.
/// </remarks>
public static class ListSorts
{
    /// <summary>
    /// The Project's effective Status order - the Board's column order - then the Task's id (open question 7 (a)).
    /// </summary>
    public static string Status => "status";

    /// <summary>
    /// Due date, soonest first, then the Task's id; Tasks with no due date last (open question 5 (a)).
    /// </summary>
    public static string DueDate => "due-date";

    /// <summary>
    /// The first Label's name, in the order the Task's chips show, then the Task's id; Tasks with no Label last (open
    /// question 3 (a)).
    /// </summary>
    public static string Label => "label";

    /// <summary>
    /// The Assignee, then the Task's id; unassigned Tasks last. Each person's Tasks are grouped together, but the order
    /// between people is by Membership id - stable, not alphabetical - because a name key would have to be copied onto every
    /// Task and rewritten whenever a name changed.
    /// </summary>
    public static string Assignee => "assignee";
}
