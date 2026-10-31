namespace Yello.Application.Spaces;

/// <summary>
/// What a deletion of the active Space came to. Story 3.3; the shape of <c>ProjectDeletion</c>.
/// </summary>
public enum SpaceDeletion
{
    /// <summary>
    /// The Space row was deleted, and the database's cascades took every row hanging from it.
    /// </summary>
    Deleted,

    /// <summary>
    /// No row matched: the Space was deleted after the pipeline resolved it. Answered as the boundary's <c>404</c>.
    /// </summary>
    NotFound,
}
