namespace Yello.Domain.Statuses;

/// <summary>
/// One Status of a Project's effective set: its id, its name in that Project, and where it comes from.
/// </summary>
/// <param name="Id">The Status's id, the same at the Space and the Project level.</param>
/// <param name="Name">Its effective name in the Project.</param>
/// <param name="Source">Whether it is a Space default or a Status the Project added.</param>
/// <param name="DefaultName">The Space default's own name when the Project renamed it; otherwise <c>null</c>.</param>
public sealed record EffectiveStatus(Guid Id, string Name, StatusSource Source, string? DefaultName);

/// <summary>
/// Where a Status of a Project's effective set comes from.
/// </summary>
public enum StatusSource
{
    /// <summary>
    /// One of the Space's defaults, renamed in the Project or not.
    /// </summary>
    SpaceDefault,

    /// <summary>
    /// A Status the Project added, which only it holds.
    /// </summary>
    AddedHere,
}
