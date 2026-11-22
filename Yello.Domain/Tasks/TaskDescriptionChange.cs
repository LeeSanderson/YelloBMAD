namespace Yello.Domain.Tasks;

/// <summary>
/// One row of a Task's description log: a change one Account made to the description, or a snapshot of the whole log up to
/// its place. The log is the description's truth; <see cref="TaskItem.Description"/> is only its projection.
/// </summary>
/// <remarks>
/// <para>
/// <b>Append-only.</b> Every property is <c>init</c>-only and nothing in the product updates a row. The one change the
/// database makes is Account erasure's <c>SET NULL</c> on <see cref="AuthorAccountId"/>, which leaves the captured
/// <see cref="AuthorName"/> and the time in place: the record that someone changed the description at that time survives.
/// </para>
/// <para>
/// <b>A snapshot never replaces a change row.</b> It bounds what a rebuild reads; the change rows before it stay, with their
/// authors and times, so compaction keeps every per-author count and timestamp. Only a superseded snapshot, which has no
/// author, is ever deleted.
/// </para>
/// <para>
/// <b><see cref="Payload"/> is opaque outside the merge adapter</b>: an encoded change, or encoded replica state.
/// </para>
/// </remarks>
public sealed class TaskDescriptionChange
{
    private const int LongestAuthorName = 128;

    /// <summary>
    /// The longest author name the column holds: the Account display name's 128.
    /// </summary>
    public static int AuthorNameMaxLength => LongestAuthorName;

    /// <summary>
    /// The row's identity, generated application-side by the sequential identifier port.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The Space the Task belongs to, carried on the row itself so the isolation predicate never needs a join.
    /// </summary>
    public required Guid SpaceId { get; init; }

    /// <summary>
    /// The Task whose description this row belongs to.
    /// </summary>
    public required Guid TaskId { get; init; }

    /// <summary>
    /// The row's place in its Task's log, unique per Task. Assigned under the Task's row lock.
    /// </summary>
    public required long Sequence { get; init; }

    /// <summary>
    /// Whether the row is a change or a snapshot.
    /// </summary>
    public required TaskDescriptionChangeKind Kind { get; init; }

    /// <summary>
    /// The encoded change, or the encoded replica state for a snapshot.
    /// </summary>
    public required byte[] Payload { get; init; }

    /// <summary>
    /// The Account that made the change, or <c>null</c> for a snapshot or once that Account is erased.
    /// </summary>
    public Guid? AuthorAccountId { get; init; }

    /// <summary>
    /// The author's display name as it stood when the change was written, or <c>null</c> for a snapshot.
    /// </summary>
    /// <remarks>
    /// Captured at write time, never looked up live: a later rename must not reach a Space the reader no longer shares with
    /// that person, and the name outlives the Account.
    /// </remarks>
    public string? AuthorName { get; init; }

    /// <summary>
    /// When the row was written, in UTC.
    /// </summary>
    public required DateTimeOffset RecordedAt { get; init; }
}
