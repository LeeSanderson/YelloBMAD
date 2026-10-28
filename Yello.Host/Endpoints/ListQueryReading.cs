using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;
using Yello.Application.Lists;
using Yello.Contracts.Lists;
using Yello.Domain.Lists;
using Yello.Domain.Tasks;

namespace Yello.Host.Endpoints;

/// <summary>
/// Reads a List View request's query and cursor as sent, before anything is read from the database (story 2.10, Task 4
/// (c); AC12). Every check here depends on the request alone.
/// </summary>
/// <remarks>
/// <para>
/// <b>The query</b>: exactly one <c>sort</c> naming an offered code; every <c>status</c> and <c>label</c> an id (at most
/// <see cref="MostFilterIds"/> of each); each due bound at most once, an instant that states its offset, inside story 2.3's
/// 2000-2100 window; <c>dueNone</c> a boolean, never together with a bound. Anything else the query holds is ignored -
/// there is no size parameter to honour.
/// </para>
/// <para>
/// <b>The cursor</b>: at most one, and one this route issued for the requested sort (<see cref="ListCursorText"/>).
/// </para>
/// </remarks>
internal static partial class ListQueryReading
{
    /// <summary>
    /// The most ids a Status or Label filter may name.
    /// </summary>
    internal const int MostFilterIds = 200;

    private const int LongestDueBound = 64;

    /// <summary>
    /// Reads the sort and the filters.
    /// </summary>
    /// <param name="query">The request's query.</param>
    /// <param name="sort">The sort, when the query reads.</param>
    /// <param name="filter">The filter as sent, ids not yet narrowed.</param>
    /// <returns>Whether the query reads.</returns>
    internal static bool TryReadQuery(IQueryCollection query, out ListSort sort, out ListFilter filter)
    {
        filter = ListFilter.None;

        if (!TrySingle(query[ListRoutes.SortParameter], out var code) || !ListSortCodes.TryParse(code, out sort))
        {
            sort = default;

            return false;
        }

        if (!TryIds(query[ListRoutes.StatusParameter], out var statusIds) || !TryIds(query[ListRoutes.LabelParameter], out var labelIds))
        {
            return false;
        }

        if (!TryDue(query, out var dueFrom, out var dueTo, out var dueNone))
        {
            return false;
        }

        filter = new ListFilter(statusIds, labelIds, dueFrom, dueTo, dueNone);

        return true;
    }

    /// <summary>
    /// Reads the cursor, if one was sent, for the requested sort.
    /// </summary>
    /// <param name="sent">The <c>after</c> values sent.</param>
    /// <param name="sort">The requested sort.</param>
    /// <param name="after">The cursor, or <c>null</c> when none was sent.</param>
    /// <returns>Whether no cursor was sent or the one sent reads.</returns>
    internal static bool TryReadCursor(StringValues sent, ListSort sort, out ListCursor? after)
    {
        after = null;

        if (sent.Count == 0)
        {
            return true;
        }

        return TrySingle(sent, out var text) && ListCursorText.TryRead(sort, text!, out after);
    }

    private static bool TryDue(IQueryCollection query, out DateTimeOffset? dueFrom, out DateTimeOffset? dueTo, out bool dueNone)
    {
        dueTo = null;
        dueNone = false;

        if (!TryDueBound(query[ListRoutes.DueFromParameter], out dueFrom) || !TryDueBound(query[ListRoutes.DueToParameter], out dueTo))
        {
            return false;
        }

        if (!TryDueNone(query[ListRoutes.DueNoneParameter], out dueNone))
        {
            return false;
        }

        // "No due date" and a range are two different questions; asked together, neither is.
        return !dueNone || (dueFrom is null && dueTo is null);
    }

    /// <summary>
    /// Exactly one value, never repeated - a repeated scalar is not a query this route reads.
    /// </summary>
    private static bool TrySingle(StringValues values, out string? value)
    {
        value = values.Count == 1 ? values[0] : null;

        return value is not null;
    }

    /// <summary>
    /// Every value an id; <c>null</c> when the parameter is absent (not filtered).
    /// </summary>
    private static bool TryIds(StringValues values, out IReadOnlyList<Guid>? ids)
    {
        ids = null;

        if (values.Count == 0)
        {
            return true;
        }

        if (values.Count > MostFilterIds)
        {
            return false;
        }

        var parsed = new List<Guid>(values.Count);

        foreach (var value in values)
        {
            if (!Guid.TryParseExact(value, "D", out var id))
            {
                return false;
            }

            parsed.Add(id);
        }

        ids = parsed;

        return true;
    }

    /// <summary>
    /// An instant with an offset inside story 2.3's 2000-2100 window, as a due date is; <c>null</c> when absent.
    /// </summary>
    private static bool TryDueBound(StringValues values, out DateTimeOffset? bound)
    {
        bound = null;

        if (values.Count == 0)
        {
            return true;
        }

        if (!TrySingle(values, out var text) || text!.Length > LongestDueBound || !OffsetSuffix.IsMatch(text))
        {
            return false;
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            && TaskDueDate.TryNormalise(parsed, out bound);
    }

    private static bool TryDueNone(StringValues values, out bool dueNone)
    {
        dueNone = false;

        if (values.Count == 0)
        {
            return true;
        }

        return TrySingle(values, out var text) && bool.TryParse(text, out dueNone);
    }

    [GeneratedRegex(@"(?:[Zz]|[+-][0-9]{2}:[0-9]{2})\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex OffsetSuffix { get; }
}
