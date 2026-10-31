namespace Yello.Contracts.Spaces;

/// <summary>
/// The Space-scoped endpoints' paths, stated once for the client that calls them and the Host that
/// serves them.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>{spaceId}</c> is always the first segment after the version</b>
/// (<c>ARCHITECTURE-SPINE.md:267</c>), and it is the input to Space resolution. The Host resolves
/// the active Space for every route whose pattern carries that parameter - by the shape of the
/// route rather than by a list of routes - so a Space-scoped path added here is resolved because of
/// what it is.
/// </para>
/// <para>
/// <b>The <c>guid</c> constraint is part of the refusal, not a convenience.</b> A segment that is not
/// a Space id matches no route and answers 404, which is the same answer a real Space the caller
/// holds no Membership in receives (AD-3: "never distinguished").
/// </para>
/// </remarks>
public static class SpaceRoutes
{
    /// <summary>
    /// The route parameter every Space-scoped path carries.
    /// </summary>
    public static string SpaceIdParameter => "spaceId";

    /// <summary>
    /// <c>GET</c> here for the Space switcher: every Space the caller holds a Membership in, as
    /// <see cref="SpaceIdentityResponse"/> rows ordered by name then id. Answers <c>200</c> - an empty
    /// list included - or <c>401</c> without a Session.
    /// </summary>
    /// <remarks>
    /// <b>Account-scoped, not Space-scoped</b> (AD-24): it carries no <c>{spaceId}</c>, so Space
    /// resolution never runs on it and it is never a refusal surface. It is one of exactly two such
    /// surfaces; Account settings is the other.
    /// <para>
    /// <c>POST</c> here, with a <see cref="CreateSpaceRequest"/>, creates a Space (story 3.1): <c>201</c>
    /// with a <c>Location</c> of <see cref="ActiveOf"/> and a <see cref="SpaceIdentityResponse"/> body,
    /// <c>400 invalid-space-name</c>, <c>409 bound-reached</c> at 50 Spaces, or <c>401</c> without a
    /// Session. It runs on the Space creation unit of work, the third Account-scoped unit of work the
    /// AD-24 amendment of 2026-10-29 names.
    /// </para>
    /// </remarks>
    public static string Directory => "/api/v1/spaces";

    /// <summary>
    /// <c>GET</c> here for the active Space's own answer - its name and the caller's Role in it, read
    /// on this request - as an <see cref="ActiveSpaceResponse"/>. <c>404</c> for a Space the caller
    /// holds no Membership in or that does not exist, exactly as every Space-scoped route answers.
    /// <c>PATCH</c> here, with a <see cref="RenameSpaceRequest"/>, renames it (story 3.1) and answers
    /// the same <see cref="ActiveSpaceResponse"/>; it declares <c>RenameSpace</c>. <c>DELETE</c> here, with no body,
    /// deletes it (story 3.3) and answers <c>204</c>; it declares <c>DeleteSpace</c>, which the Owner alone holds. A
    /// replayed deletion finds no Membership and is the boundary's <c>404</c> (AD-18's recorded variance).
    /// </summary>
    public static string Active => "/api/v1/spaces/{spaceId:guid}";

    /// <summary>
    /// <c>GET</c> here for what deleting the active Space would take, as a <see cref="SpaceDeletionSummaryResponse"/>:
    /// its Project and Task counts and how many other Accounts would lose access, counted live (story 3.3, open question
    /// 2 (a)). Declares <c>DeleteSpace</c>, so it answers the Owner alone.
    /// </summary>
    public static string DeletionSummary => "/api/v1/spaces/{spaceId:guid}/deletion";

    /// <summary>
    /// The deletion summary's path for one Space, as a client calls it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string DeletionSummaryOf(Guid spaceId) => $"/api/v1/spaces/{spaceId:D}/deletion";

    /// <summary>
    /// The active Space's path for one Space, as a client calls it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string ActiveOf(Guid spaceId) => $"/api/v1/spaces/{spaceId:D}";

    /// <summary>
    /// <c>GET</c> here for the active Space's Statuses, in Board order. Answers <c>200</c> with the
    /// list, <c>401</c> without a Session, and <c>404</c> for a Space the caller holds no Membership
    /// in or that does not exist.
    /// </summary>
    public static string Statuses => "/api/v1/spaces/{spaceId:guid}/statuses";

    /// <summary>
    /// The Statuses path for one Space, as a client calls it.
    /// </summary>
    /// <param name="spaceId">The Space.</param>
    /// <returns>The path.</returns>
    public static string StatusesOf(Guid spaceId) => $"/api/v1/spaces/{spaceId:D}/statuses";
}
