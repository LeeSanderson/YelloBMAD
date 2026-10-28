namespace Yello.Domain.Lists;

/// <summary>
/// What a List View narrows a Project's Tasks to (FR-30; story 2.10, open question 4 (a)). The three filters combine:
/// a Task is listed when it matches every one that is set.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>null</c> means "not filtered"; an empty list means "matches nothing"</b>. A handler narrows the ids it was sent
/// to this Project's effective Statuses and this Space's Labels before any read, so an id that names nothing here - a
/// random one, another Space's real one, a Label deleted since it was chosen - simply drops out, and a filter left with
/// none matches no Task (AC15). Never a refusal.
/// </para>
/// <para>
/// <b>Due date</b>: a half-open range <c>[DueFrom, DueTo)</c> of instants, either bound optional, or
/// <see cref="DueNone"/> for "No due date" - never both (the endpoint refuses that combination).
/// </para>
/// </remarks>
/// <param name="StatusIds">Statuses, any of which a listed Task is in; <c>null</c> for every Status.</param>
/// <param name="LabelIds">Labels, any of which a listed Task carries; <c>null</c> for any Labels or none.</param>
/// <param name="DueFrom">The due-date range's inclusive start, or <c>null</c>.</param>
/// <param name="DueTo">The due-date range's exclusive end, or <c>null</c>.</param>
/// <param name="DueNone">Only Tasks with no due date.</param>
public sealed record ListFilter(
    IReadOnlyList<Guid>? StatusIds = null,
    IReadOnlyList<Guid>? LabelIds = null,
    DateTimeOffset? DueFrom = null,
    DateTimeOffset? DueTo = null,
    bool DueNone = false)
{
    /// <summary>
    /// No filter: every Task of the Project.
    /// </summary>
    public static ListFilter None { get; } = new();

    /// <summary>
    /// Whether a filter set to match nothing - a Status or Label filter left empty by narrowing - makes the read
    /// pointless.
    /// </summary>
    public bool MatchesNothing => StatusIds is { Count: 0 } || LabelIds is { Count: 0 };
}
