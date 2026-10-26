namespace Yello.Domain.Labels;

/// <summary>
/// A named, coloured tag defined once per Space and applicable to any Task in any Project of it. FR-22; AD-2;
/// story 2.4.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its Space is fixed at creation.</b> <see cref="SpaceId"/> is <c>init</c>-only; the database refuses the same
/// change with a <c>BLOCK ... AFTER UPDATE</c> predicate; and no request contract names a Space.
/// </para>
/// <para>
/// <b>Applications are not a member of this type.</b> Which Tasks carry a Label is a many-to-many the store keeps
/// (<c>TaskLabel</c>); the model has no navigation properties by design - see <c>YelloDbContext</c>'s remarks.
/// </para>
/// </remarks>
public sealed class Label
{
    /// <summary>
    /// The Label's identity, generated application-side by the sequential identifier port (AR-34).
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The Space the Label belongs to. Non-nullable (AD-2), and set once.
    /// </summary>
    public required Guid SpaceId { get; init; }

    /// <summary>
    /// The Label's name, normalised by <see cref="LabelName.TryNormalise"/>; unique within its Space.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The Label's colour: one of <see cref="LabelPalette.Keys"/>, never a colour value. The token layer owns the
    /// fill each key renders as (story 2.4, open question 3).
    /// </summary>
    public required string Colour { get; set; }

    /// <summary>
    /// When the Label was defined, in UTC.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
