namespace Yello.Contracts.Projects;

/// <summary>
/// One Status of a Project's effective set, as the Project's Status read and every delta write answer it.
/// </summary>
/// <remarks>
/// No Space and no position: the caller named both Space and Project in the path, and the order is the array's.
/// </remarks>
/// <param name="Id">The Status's id, the same at the Space and the Project level, and unchanged by a rename.</param>
/// <param name="Name">Its name in this Project.</param>
/// <param name="Source">Where it comes from: one of <see cref="ProjectStatusSources"/>.</param>
/// <param name="DefaultName">The Space default's own name when this Project renamed it; otherwise <c>null</c>.</param>
/// <param name="TaskCount">How many of the Project's Tasks hold it.</param>
public sealed record ProjectStatusResponse(Guid Id, string Name, string Source, string? DefaultName, int TaskCount);

/// <summary>
/// The values of <see cref="ProjectStatusResponse.Source"/>.
/// </summary>
public static class ProjectStatusSources
{
    /// <summary>
    /// One of the Space's defaults, renamed in this Project or not.
    /// </summary>
    public static string SpaceDefault => "space-default";

    /// <summary>
    /// A Status this Project added, which only it holds.
    /// </summary>
    public static string AddedHere => "added-here";
}
