namespace Yello.Tests.Shared;

/// <summary>
/// Story 2.1's Project routes and stable problem types, as the story's Tasks 4 and 7 name them, held in
/// one place so the red-phase scaffolds compile before <c>Yello.Contracts.Projects.ProjectRoutes</c>
/// exists. Written by the story 2.1 ATDD run.
/// </summary>
/// <remarks>
/// <para>
/// <b>This file is temporary by design.</b> On activation, dev-story replaces every use with the
/// <c>Yello.Contracts</c> constant it stands for (<c>ProjectRoutes.List</c>/<c>Create</c>,
/// <c>ProjectRoutes.Rename</c>, <c>ProjectRoutes.ListIn</c>, <c>ProjectRoutes.ProjectOf</c>,
/// <c>ProblemTypes.InvalidProjectName</c>, <c>ProblemTypes.IdempotencyKeyReused</c>,
/// <c>ProblemTypes.IdempotencyKeyInFlight</c>, the <c>Idempotency-Key</c> header constant) and deletes
/// this file, so a contract rename is a compile error again. The isolation list's convention is "a
/// <c>Yello.Contracts</c> pattern constant, never a literal"; these literals are the one exception, and
/// only for as long as the contract does not exist.
/// </para>
/// <para>
/// <b>If the story's Q6 picks a different shape</b>, correct the strings here first; the isolation
/// gap gate (1.9-GATE-002) reads the Host's real route table and fails on any mapped route no row names,
/// so a drifted pattern cannot pass silently.
/// </para>
/// </remarks>
public static class ProjectRouteScaffold
{
    /// <summary>
    /// <c>GET</c> (list) and <c>POST</c> (create): <c>ProjectRoutes.List</c> / <c>ProjectRoutes.Create</c>.
    /// </summary>
    public static string Collection => "/api/v1/spaces/{spaceId:guid}/projects";

    /// <summary>
    /// <c>PATCH</c> (rename): <c>ProjectRoutes.Rename</c>.
    /// </summary>
    public static string Item => "/api/v1/spaces/{spaceId:guid}/projects/{projectId:guid}";

    /// <summary>
    /// The <c>Idempotency-Key</c> request header (AD-18, Task 4).
    /// </summary>
    public static string IdempotencyKeyHeader => "Idempotency-Key";

    /// <summary>
    /// <c>400</c>: a Project name that is blank after trimming or longer than 100 characters (Q3).
    /// </summary>
    public static string InvalidProjectName => "urn:yello:problem:invalid-project-name";

    /// <summary>
    /// <c>422</c>: an <c>Idempotency-Key</c> replayed with a different request (Q7).
    /// </summary>
    public static string IdempotencyKeyReused => "urn:yello:problem:idempotency-key-reused";

    /// <summary>
    /// <c>409</c>: an <c>Idempotency-Key</c> whose first use is still in flight (Q7).
    /// </summary>
    public static string IdempotencyKeyInFlight => "urn:yello:problem:idempotency-key-in-flight";

    /// <summary>
    /// The list and create path for <paramref name="spaceId"/>: <c>ProjectRoutes.ListIn</c>.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string ListIn(Guid spaceId) => $"/api/v1/spaces/{spaceId:D}/projects";

    /// <summary>
    /// One Project's path: <c>ProjectRoutes.ProjectOf</c>.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <param name="projectId">The Project.</param>
    /// <returns>The path.</returns>
    public static string ProjectOf(Guid spaceId, Guid projectId) => $"/api/v1/spaces/{spaceId:D}/projects/{projectId:D}";
}
