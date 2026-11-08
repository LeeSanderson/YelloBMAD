namespace Yello.Contracts.Lists;

/// <summary>
/// What a client asks a List View page for (story 2.10, open question 4 (a)): a sort and the filters, which combine. <see cref="ListRoutes.ListOf(Guid, Guid, ListQuery)"/> writes it as the route's query.
/// </summary>
/// <param name="Sort">A <see cref="ListSorts"/> code.</param>
/// <param name="StatusIds">Statuses, any of which a listed Task is in; empty for every Status.</param>
/// <param name="LabelIds">Labels, any of which a listed Task carries; empty for any Labels or none.</param>
/// <param name="DueFrom">The due-date range's inclusive start, an instant with its offset, or <c>null</c>.</param>
/// <param name="DueTo">The due-date range's exclusive end, an instant with its offset, or <c>null</c>.</param>
/// <param name="DueNone">Only Tasks with no due date - never together with a range.</param>
public sealed record ListQuery(
    string Sort,
    IReadOnlyList<Guid> StatusIds,
    IReadOnlyList<Guid> LabelIds,
    DateTimeOffset? DueFrom,
    DateTimeOffset? DueTo,
    bool DueNone)
{
    /// <summary>
    /// Memberships, any of which a listed Task is assigned to; empty for any Assignee or none, unless
    /// <see cref="Unassigned"/> is set.
    /// </summary>
    public IReadOnlyList<Guid> AssigneeMembershipIds { get; init; } = [];

    /// <summary>
    /// Unassigned Tasks as well: alone, only unassigned Tasks; beside <see cref="AssigneeMembershipIds"/>, any of those.
    /// </summary>
    public bool Unassigned { get; init; }
}
