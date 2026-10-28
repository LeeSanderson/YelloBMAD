using System.Globalization;
using System.Text;

namespace Yello.Contracts.Lists;

/// <summary>
/// The List View endpoint's path and query, stated once for the client and the Host, beside the Board's. FR-30; story
/// 2.10, open questions 1 (a) and 4 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>One route answers every page</b> with a <see cref="ListPageResponse"/>. The query: <c>sort</c> (a
/// <see cref="ListSorts"/> code), <c>status</c> and <c>label</c> (repeatable ids; any of them), <c>dueFrom</c> and
/// <c>dueTo</c> (instants with an offset, a half-open range), <c>dueNone</c> (<c>true</c> for "No due date"), and
/// <c>after</c> (a cursor the route issued for the same sort).
/// </para>
/// <para>
/// <b>No size parameter</b> (AC12): the server owns the page size, because a client-chosen size is a lever on an
/// unbounded read. A query carries ids only - never a Status or Label name (UX-DR33).
/// </para>
/// </remarks>
public static class ListRoutes
{
    /// <summary>
    /// <c>GET</c> here for one page of one Project's <see cref="ListPageResponse"/>.
    /// </summary>
    public static string List => "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}/list";

    /// <summary>
    /// The query parameter naming the sort.
    /// </summary>
    public static string SortParameter => "sort";

    /// <summary>
    /// The repeatable query parameter naming a Status to filter by.
    /// </summary>
    public static string StatusParameter => "status";

    /// <summary>
    /// The repeatable query parameter naming a Label to filter by.
    /// </summary>
    public static string LabelParameter => "label";

    /// <summary>
    /// The query parameter holding the due-date range's inclusive start.
    /// </summary>
    public static string DueFromParameter => "dueFrom";

    /// <summary>
    /// The query parameter holding the due-date range's exclusive end.
    /// </summary>
    public static string DueToParameter => "dueTo";

    /// <summary>
    /// The query parameter asking for Tasks with no due date only.
    /// </summary>
    public static string DueNoneParameter => "dueNone";

    /// <summary>
    /// The query parameter holding the cursor.
    /// </summary>
    public static string AfterParameter => "after";

    /// <summary>
    /// The first page's path, as a client calls it. Every value is escaped; repeatable parameters repeat.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <param name="query">The sort and filters.</param>
    /// <returns>The path and query.</returns>
    public static string ListOf(Guid spaceId, Guid projectId, ListQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var path = new StringBuilder();

        path.Append(CultureInfo.InvariantCulture, $"/api/v1/spaces/{spaceId:D}/projects/{projectId:D}/list?{SortParameter}={Uri.EscapeDataString(query.Sort)}");

        foreach (var statusId in query.StatusIds)
        {
            path.Append(CultureInfo.InvariantCulture, $"&{StatusParameter}={statusId:D}");
        }

        foreach (var labelId in query.LabelIds)
        {
            path.Append(CultureInfo.InvariantCulture, $"&{LabelParameter}={labelId:D}");
        }

        if (query.DueFrom is { } from)
        {
            path.Append(CultureInfo.InvariantCulture, $"&{DueFromParameter}={Uri.EscapeDataString(from.ToString("O", CultureInfo.InvariantCulture))}");
        }

        if (query.DueTo is { } to)
        {
            path.Append(CultureInfo.InvariantCulture, $"&{DueToParameter}={Uri.EscapeDataString(to.ToString("O", CultureInfo.InvariantCulture))}");
        }

        if (query.DueNone)
        {
            path.Append(CultureInfo.InvariantCulture, $"&{DueNoneParameter}=true");
        }

        return path.ToString();
    }

    /// <summary>
    /// The path of the page after a cursor, as a client calls it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <param name="query">The sort and filters - the same as the page that issued the cursor.</param>
    /// <param name="after">The cursor.</param>
    /// <returns>The path and query.</returns>
    public static string ListOf(Guid spaceId, Guid projectId, ListQuery query, string after)
    {
        ArgumentNullException.ThrowIfNull(after);

        return ListOf(spaceId, projectId, query) + $"&{AfterParameter}={Uri.EscapeDataString(after)}";
    }
}
