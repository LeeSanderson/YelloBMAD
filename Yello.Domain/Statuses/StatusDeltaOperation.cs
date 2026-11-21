namespace Yello.Domain.Statuses;

/// <summary>
/// One operation of a Project's Status delta: add, rename, place or remove one Status, keyed by the Status's id.
/// </summary>
/// <remarks>
/// <para>
/// <b>Keyed by the id, never by the name.</b> A Space-level rename has to tell whether a Project renamed the same
/// Status itself, so that the Project's own name wins. A delta keyed by name cannot express "this Status" once either
/// level has renamed it, so the id is the key at both levels and a rename changes no key.
/// </para>
/// <para>
/// <b>At most one operation per Status and kind.</b> Writing an operation again replaces the earlier one and takes the
/// next <see cref="Sequence"/>, so the delta stays bounded by the Project's Statuses times the four kinds.
/// </para>
/// <para>
/// <b>Only a Space default is removed by an operation.</b> A default must stay "removed in this Project" against later
/// Space-level changes, which needs a row keyed on its id. A Status the Project added exists nowhere else, so its removal
/// deletes its definition and its operations instead: keeping them would hold its name in the owned-name index.
/// </para>
/// <para>
/// <b>A place names an anchor, never an ordinal.</b> A Status with no <see cref="StatusDeltaKind.Place"/> keeps its slot
/// in Space order, so a Space reorder still reaches every Project that has not placed that Status itself. An ordinal per
/// Status would be the Project's whole order stored, which is the materialised set the delta exists to avoid.
/// </para>
/// </remarks>
public sealed class StatusDeltaOperation
{
    /// <summary>
    /// The operation's identity, generated application-side.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The Space the Project belongs to. Non-nullable, and set once.
    /// </summary>
    public required Guid SpaceId { get; init; }

    /// <summary>
    /// The Project whose delta this is. Set once.
    /// </summary>
    public required Guid ProjectId { get; init; }

    /// <summary>
    /// The Status the operation applies to: a Space default, or a definition this Project owns.
    /// </summary>
    public required Guid StatusId { get; init; }

    /// <summary>
    /// What the operation does.
    /// </summary>
    public required StatusDeltaKind Kind { get; init; }

    /// <summary>
    /// The Status's name in this Project, for a <see cref="StatusDeltaKind.Rename"/>; otherwise <c>null</c>.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The Status a <see cref="StatusDeltaKind.Place"/> puts this one after, or <c>null</c> for first.
    /// </summary>
    public Guid? AfterStatusId { get; set; }

    /// <summary>
    /// Where the operation falls in the delta's order. Assigned under the Project's lock, never from a clock.
    /// </summary>
    public required int Sequence { get; set; }

    /// <summary>
    /// When the operation was written, in UTC.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// What a <see cref="StatusDeltaOperation"/> does.
/// </summary>
public enum StatusDeltaKind
{
    /// <summary>
    /// Appends a Status this Project owns to its set.
    /// </summary>
    Add,

    /// <summary>
    /// Gives a Status a name in this Project.
    /// </summary>
    Rename,

    /// <summary>
    /// Moves a Status after another, or to the front.
    /// </summary>
    Place,

    /// <summary>
    /// Takes a Space default out of this Project's set, keyed by its id so the default can be added back.
    /// </summary>
    Remove,
}
