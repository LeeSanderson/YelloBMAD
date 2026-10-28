using Yello.Domain.Tasks;

namespace Yello.Domain.Lists;

/// <summary>
/// Where a List View page ends: the last served Task's pair <c>(sort value, TaskId)</c> under one sort (AR-22, AD-29;
/// story 2.10, AC4, AC12). The next page is the rows whose pair sorts after it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A pair, not an id</b>: the next page seeks to the first pair after this one whether or not its Task still exists or
/// still holds that value, so nothing is skipped or repeated when the Task is deleted or edited between pages (AC12).
/// </para>
/// <para>
/// <b>The sort value for the cursor's sort is the only one that matters</b>; for the due-date and Label sorts a
/// <c>null</c> value means the cursor is in the trailing segment of Tasks with none (AC5, AC13).
/// </para>
/// </remarks>
/// <param name="TaskId">The last served Task's id - the tiebreaker, compared in SQL Server's <c>uniqueidentifier</c> order.</param>
/// <param name="StatusId">Its Status, under <see cref="ListSort.Status"/>.</param>
/// <param name="DueDate">Its due date, under <see cref="ListSort.DueDate"/>; <c>null</c> in the undated segment.</param>
/// <param name="FirstLabelName">Its first Label's name, under <see cref="ListSort.Label"/>; <c>null</c> in the unlabelled segment.</param>
public sealed record ListCursor(Guid TaskId, Guid? StatusId = null, DateTimeOffset? DueDate = null, string? FirstLabelName = null)
{
    /// <summary>
    /// The cursor after <paramref name="task"/>: its pair under every sort, so one value serves whichever is in use.
    /// </summary>
    /// <param name="task">The last served Task.</param>
    /// <returns>The cursor.</returns>
    public static ListCursor After(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);

        return new ListCursor(task.Id, task.StatusId, task.DueDate, task.FirstLabelName);
    }
}
