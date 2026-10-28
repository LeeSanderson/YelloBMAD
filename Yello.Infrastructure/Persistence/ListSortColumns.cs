using Yello.Domain.Lists;
using Yello.Domain.Tasks;

namespace Yello.Infrastructure.Persistence;

/// <summary>
/// Which <c>Task</c> column each List View sort orders by, stated once (story 2.10, Task 9; AR-22, AC6). Keyed by the
/// sort's stable code.
/// </summary>
/// <remarks>
/// <b>Held to the schema by a gate</b>: <c>ListViewGateTests</c> reads this map and fails unless every offered sort code
/// (<c>Yello.Contracts.Lists.ListSorts</c>) is a key here and the EF model holds an index whose key columns are exactly
/// <c>(SpaceId, ProjectId, &lt;column&gt;, Id)</c> - "a List View sort offered in the interface without a matching
/// composite index is a defect, not a slow query" (<c>epics.md:210</c>). <see cref="ListReader"/> seeks those indexes;
/// <c>ListPagingTests</c> LV-19 reads each sort's plan for its seek.
/// </remarks>
public static class ListSortColumns
{
    /// <summary>
    /// Sort code to the <see cref="TaskItem"/> property - the column - it orders by.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ByCode = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [ListSortCodes.Of(ListSort.Status)] = nameof(TaskItem.StatusId),
        [ListSortCodes.Of(ListSort.DueDate)] = nameof(TaskItem.DueDate),
        [ListSortCodes.Of(ListSort.Label)] = nameof(TaskItem.FirstLabelName),
    };
}
