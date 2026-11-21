namespace Yello.Domain.Statuses;

/// <summary>
/// One named Status a Task may sit in, defined for one Space.
/// </summary>
/// <remarks>
/// <para>
/// <b>Identity-bearing, so a rename is a rename rather than a delete and an add</b> (AR-23,
/// <c>epics.md:193</c>). Tasks reference the <see cref="Id"/>, so changing <see cref="Name"/>
/// moves nothing - which is what lets Epic 6 cascade a rename across a Space without touching a
/// single Task row.
/// </para>
/// <para>
/// <b>Registration seeds exactly the three FR-24 defaults</b>, named in
/// <see cref="DefaultStatusSet.Names"/> and - as that class explains - nowhere in prose. This
/// story <i>seeds</i> that set; it does not own it. Configuring the Status set - the editor,
/// removal by migration, the Space-default cascade - is Epic 6's, at stories 6.1 to 6.4.
/// </para>
/// <para>
/// <b>No per-Project effective Status set is materialised, and none may be.</b> No table stores
/// one: a Project's effective set is derived from its Space's defaults, the definitions it owns and
/// its delta at the point of use (<see cref="EffectiveStatusSet"/>). Materialising it would create a
/// second copy that a cascade has to keep in step, which is the shape Epic 6's stories exist to avoid.
/// </para>
/// <para>
/// <b>A Status a Project adds is a row of this type too</b>, owned by that Project
/// (<see cref="OwnerProjectId"/>), so a Task references every Status it can hold through the one key
/// <c>(SpaceId, Id)</c>.
/// </para>
/// </remarks>
public sealed class StatusDefinition
{
    /// <summary>
    /// The Status's identity. Stable across a rename, which is the whole point of the entity
    /// carrying one.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The Space these Statuses belong to. Non-nullable, by AD-2 - see
    /// <see cref="Memberships.Membership"/>'s remarks for why.
    /// </summary>
    public required Guid SpaceId { get; init; }

    /// <summary>
    /// The Project that added this Status and alone holds it, or <c>null</c> for one of the Space's defaults.
    /// </summary>
    public Guid? OwnerProjectId { get; init; }

    /// <summary>
    /// The Status's name, as a person reads it.
    /// </summary>
    /// <remarks>
    /// A Project's rename of a default never writes it: that is an operation of the Project's delta, so
    /// the Space's default keeps its own name for every other Project. A rename of an owned definition
    /// records the same operation and also writes this, because the unique index over the Project's
    /// owned names would otherwise still hold the old name and refuse adding it again.
    /// </remarks>
    public required string Name { get; set; }

    /// <summary>
    /// Where this Status sits in the Space's order, ascending. The Board draws its columns in
    /// this order.
    /// </summary>
    public required int Position { get; set; }

    /// <summary>
    /// When the Status was defined, in UTC.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
