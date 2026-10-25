using System.Globalization;

namespace Yello.Client.Spaces;

/// <summary>
/// A Task's due date between the wire and the person: an instant in UTC on the wire, a day in the viewer's own
/// zone and locale on screen. Story 2.3, AC7, open question 4 (a); UX-DR42.
/// </summary>
/// <remarks>
/// <para>
/// <b>Choosing a day sends the start of that day in the editor's zone</b>, as an ISO 8601 instant with its
/// offset; the server stores it in UTC. Every viewer then sees the day that instant falls on in their own
/// zone - so a viewer far enough away can see the adjacent day, which is what the deferred ledger asks UX to
/// settle before Spaces are shared (Epic 4).
/// </para>
/// <para>
/// <b>The zone is the browser's</b>: WebAssembly's <see cref="TimeZoneInfo.Local"/>, and the locale is
/// <see cref="CultureInfo.CurrentCulture"/>, which the runtime takes from the browser.
/// </para>
/// </remarks>
public static class TaskDates
{
    private const string InputFormat = "yyyy-MM-dd";

    // The server's due-date range (Yello.Domain's TaskDueDate, which the client cannot reference); the server
    // still decides.
    private const int EarliestYear = 2000;

    private const int LatestYear = 2100;

    /// <summary>
    /// The day and month a due date falls on in the viewer's zone, in the viewer's locale's own month-day
    /// pattern - the card's form.
    /// </summary>
    /// <param name="due">The due date, an instant.</param>
    /// <returns>The day and month.</returns>
    public static string DayAndMonth(DateTimeOffset due) =>
        Local(due).ToString("M", CultureInfo.CurrentCulture);

    /// <summary>
    /// The full date a due date falls on in the viewer's zone, in the viewer's locale - the panel's text form.
    /// </summary>
    /// <param name="due">The due date, an instant.</param>
    /// <returns>The date.</returns>
    public static string FullDate(DateTimeOffset due) =>
        Local(due).ToString("D", CultureInfo.CurrentCulture);

    /// <summary>
    /// A date input's value for a due date: the day it falls on in the viewer's zone, as <c>yyyy-MM-dd</c>.
    /// </summary>
    /// <param name="due">The due date, or <c>null</c>.</param>
    /// <returns>The value, or empty.</returns>
    public static string InputValue(DateTimeOffset? due) =>
        due is { } value ? DateOnly.FromDateTime(Local(value).DateTime).ToString("O", CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>
    /// The due date a date input's value stands for: the start of that day in the editor's zone.
    /// </summary>
    /// <param name="value">The input's value, <c>yyyy-MM-dd</c>, or empty.</param>
    /// <param name="due">The instant, with the editor's offset; <c>null</c> for an empty value.</param>
    /// <returns>
    /// <c>true</c> when the value is empty or a date in the years 2000 to 2100 - the server's range, judged on
    /// the day as chosen (a year part-way through being typed, such as 0002, is not one).
    /// </returns>
    public static bool TryFromInputValue(string? value, out DateTimeOffset? due)
    {
        due = null;

        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        if (!DateOnly.TryParseExact(value, InputFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            || day.Year is < EarliestYear or > LatestYear)
        {
            return false;
        }

        var startOfDay = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        due = new DateTimeOffset(startOfDay, TimeZoneInfo.Local.GetUtcOffset(startOfDay));

        return true;
    }

    private static DateTimeOffset Local(DateTimeOffset due) => TimeZoneInfo.ConvertTime(due, TimeZoneInfo.Local);
}
