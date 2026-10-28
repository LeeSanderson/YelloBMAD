namespace Yello.Domain.Tasks;

/// <summary>
/// A Task: one piece of work in one Project, in one Status, belonging permanently to one Space. FR-19;
/// AD-2, AD-15, AD-17.
/// </summary>
/// <remarks>
/// <para>
/// <b>Named <c>TaskItem</c> in code, "Task" everywhere a person or the schema reads it</b> (story 2.2,
/// open question 1 (b)). <c>Task</c> collides with <see cref="System.Threading.Tasks.Task"/> under the
/// implicit usings, so every store, handler and endpoint would need an alias and <c>async Task&lt;Task&gt;</c>
/// signatures. The Glossary governs product language and the schema - the table is <c>Task</c>, the
/// namespace is <c>Yello.Domain.Tasks</c>, the routes and copy say "Task" - not CLR identifiers.
/// </para>
/// <para>
/// <b>Its Space is fixed at creation.</b> <see cref="SpaceId"/> is <c>init</c>-only; the database refuses
/// the same change with a <c>BLOCK ... AFTER UPDATE</c> predicate; and no request contract names a Space.
/// </para>
/// <para>
/// <b>No description</b> (<c>epics.md:437</c>; FR-31 is Epic 7) and no navigation properties - see
/// <c>YelloDbContext</c>'s remarks.
/// </para>
/// </remarks>
public sealed class TaskItem
{
    /// <summary>
    /// The Task's identity, generated application-side by the sequential identifier port (AR-34).
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The Space the Task belongs to. Non-nullable and carried on the Task itself (AC8), so the row-level
    /// security predicate never needs a join; set once.
    /// </summary>
    public required Guid SpaceId { get; init; }

    /// <summary>
    /// The Project the Task is in. Settable only because FR-41 (story 6.5) moves a Task between Projects
    /// within its Space; nothing in story 2.2 writes it after creation.
    /// </summary>
    public required Guid ProjectId { get; set; }

    /// <summary>
    /// The Task's Status, one of its Project's effective set (AD-17).
    /// </summary>
    public required Guid StatusId { get; set; }

    /// <summary>
    /// The Task's title - the only content on its card. Settable for story 2.3's edit.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// When the Task is due, as an instant in UTC, or <c>null</c> when it has none (<c>prd.md:387</c>,
    /// "optionally ... a due date"). Story 2.3; AR-34. Always a <see cref="DateTimeOffset"/> normalised by
    /// <see cref="TaskDueDate.TryNormalise"/>, never a zone-less date.
    /// </summary>
    public DateTimeOffset? DueDate { get; set; }

    /// <summary>
    /// Where the Task sits in its column (AD-15): ordered by ordinal comparison, unique within its
    /// (Project, Status), readable over the API and never writable through it.
    /// </summary>
    public required string PositionKey { get; set; }

    /// <summary>
    /// When the Task was created, in UTC.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// The name of the first Label the Task carries, in chip order (Label name, then id, in the database's collation -
    /// story 2.4, open question 12), or <c>null</c> when it carries none: the List View's Label sort key (story 2.10, open
    /// question 3 (a); AR-22). At most <c>LabelName.MaxLength</c> characters.
    /// </summary>
    /// <remarks>
    /// <b>Maintained by Infrastructure, never by a request</b>: the Labels edit recomputes it from the final set in the
    /// same transaction that replaces the set, and a new Task starts with none. No request contract carries it, and no
    /// response does - a row shows the Labels themselves. Kept on the Task because a many-to-many value has no single key
    /// an index can serve until one is chosen (AC6: a sort without its composite index is a defect).
    /// </remarks>
    public string? FirstLabelName { get; set; }
}
