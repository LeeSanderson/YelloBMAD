namespace Yello.Domain.Projects;

/// <summary>
/// A named container for work, belonging to exactly one Space. FR-17, FR-18; AD-2.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its Space is fixed at creation.</b> <see cref="SpaceId"/> has an <c>init</c> accessor and no
/// setter, so no code path can move a Project once it exists; the database refuses the same change
/// with a <c>BLOCK ... AFTER UPDATE</c> predicate, and no route or request contract names a Space to
/// move it to. That is AC7 at every layer that could otherwise carry it.
/// </para>
/// <para>
/// <b>No Status column, and no effective Status set.</b> A Project's effective set is derived from
/// its Space's defaults and its own delta at the point of use (AD-16); the delta does not exist until
/// story 6.1, so a new Project's effective set is the Space default set by construction.
/// </para>
/// <para>
/// No navigation properties - see <c>YelloDbContext</c>'s remarks.
/// </para>
/// </remarks>
public sealed class Project
{
    /// <summary>
    /// The Project's identity, generated application-side by the sequential identifier port.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The Space the Project belongs to. Non-nullable (AD-2), and set once.
    /// </summary>
    public required Guid SpaceId { get; init; }

    /// <summary>
    /// The Project's name, as a person reads it. Renaming changes this and nothing else.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// When the Project was created, in UTC.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
