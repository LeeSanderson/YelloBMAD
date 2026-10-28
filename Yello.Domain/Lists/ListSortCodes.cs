namespace Yello.Domain.Lists;

/// <summary>
/// The stable code each <see cref="ListSort"/> has on the wire and in a cursor. The client's copy is
/// <c>Yello.Contracts.Lists.ListSorts</c>; <c>ListViewGateTests</c> holds the two equal.
/// </summary>
public static class ListSortCodes
{
    private const string StatusCode = "status";

    private const string DueDateCode = "due-date";

    private const string LabelCode = "label";

    /// <summary>
    /// The code of <paramref name="sort"/>.
    /// </summary>
    /// <param name="sort">The sort.</param>
    /// <returns>Its code.</returns>
    public static string Of(ListSort sort) => sort switch
    {
        ListSort.Status => StatusCode,
        ListSort.DueDate => DueDateCode,
        ListSort.Label => LabelCode,
        _ => throw new ArgumentOutOfRangeException(nameof(sort), sort, "Not an offered sort."),
    };

    /// <summary>
    /// The sort a code names, compared ordinally - <c>false</c> for anything else, the empty code included.
    /// </summary>
    /// <param name="code">The code as sent.</param>
    /// <param name="sort">The sort, when the code names one.</param>
    /// <returns>Whether it does.</returns>
    public static bool TryParse(string? code, out ListSort sort)
    {
        switch (code)
        {
            case StatusCode:
                sort = ListSort.Status;
                return true;
            case DueDateCode:
                sort = ListSort.DueDate;
                return true;
            case LabelCode:
                sort = ListSort.Label;
                return true;
            default:
                sort = default;
                return false;
        }
    }
}
