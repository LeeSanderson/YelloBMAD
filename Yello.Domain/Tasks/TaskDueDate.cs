namespace Yello.Domain.Tasks;

/// <summary>
/// What a Task's due date may be: an instant, held in UTC, between the start of 2000 and the end of 2100.
/// Story 2.3, open question 4 (a); AR-34.
/// </summary>
/// <remarks>
/// <para>
/// <b>An instant, not a calendar day.</b> The client sends the start of the chosen day in the editor's own
/// zone, and every viewer renders the date part in theirs; the server stores whatever instant it is given,
/// converted to UTC (offset zero), so the wire always answers ISO 8601 with a <c>+00:00</c> offset. Whether a
/// due date should become a zone-free day once Spaces are shared is a UX question in the deferred ledger.
/// </para>
/// <para>
/// <b>The range is a sanity bound, not a product rule.</b> It refuses values a date picker never produces
/// (a year typed as 20261, an epoch zero) before they reach the column.
/// </para>
/// </remarks>
public static class TaskDueDate
{
    private const int EarliestYear = 2000;

    private const int FirstYearAfterTheRange = 2101;

    /// <summary>
    /// The earliest due date accepted, inclusive.
    /// </summary>
    public static DateTimeOffset Earliest { get; } = new(EarliestYear, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The first instant after the accepted range: the start of 2101, exclusive.
    /// </summary>
    public static DateTimeOffset FirstAfterTheRange { get; } = new(FirstYearAfterTheRange, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Converts a submitted due date to UTC and says whether it is one. <c>null</c> - no due date - is one.
    /// </summary>
    /// <param name="submitted">The due date as submitted, or <c>null</c> to clear it.</param>
    /// <param name="dueDate">The same instant at offset zero, or <c>null</c>.</param>
    /// <returns><c>true</c> when the due date is absent or inside the accepted range.</returns>
    public static bool TryNormalise(DateTimeOffset? submitted, out DateTimeOffset? dueDate)
    {
        dueDate = null;

        if (submitted is not { } value)
        {
            return true;
        }

        var utc = value.ToUniversalTime();

        if (utc < Earliest || utc >= FirstAfterTheRange)
        {
            return false;
        }

        dueDate = utc;

        return true;
    }
}
