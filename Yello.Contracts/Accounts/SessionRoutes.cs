namespace Yello.Contracts.Accounts;

/// <summary>
/// The Session endpoints' paths, stated once for the client that calls them and the Host that
/// serves them.
/// </summary>
/// <remarks>
/// <para>
/// <b>A Session is a resource, and these are its verbs.</b> No document names a sign-in route or
/// its response shapes - exactly as none named registration's before story 1.3 chose
/// <c>POST /api/v1/accounts</c> - so this is a recorded decision rather than a transcription.
/// <c>POST</c> establishes one; <c>GET .../current</c> asks whether the caller holds one.
/// </para>
/// <para>
/// <b>There is no <c>DELETE</c>, and its absence is deliberate.</b> No FR, CAP, journey or
/// acceptance criterion anywhere in the artifact set asks for sign-out - yet UX-DR31 names it as
/// one of the five purge triggers, which presupposes it exists. Story 1.4's own scope line assigns
/// the purge seam's extension "to Space switch and sign-out" to story <b>1.7</b>, which is also the
/// story that builds the context bar a sign-out control would live in. Recorded as question 8 for
/// Lee rather than built here, because building an endpoint no requirement asks for is how a
/// product grows a surface nobody chose.
/// </para>
/// <para>
/// <b>The version segment is in the path, not in a header</b>, for the reason
/// <see cref="AccountRoutes"/> records: story 8.2 owns versioning properly, and an unversioned path
/// shipped today is one 8.2 would have to break.
/// </para>
/// </remarks>
public static class SessionRoutes
{
    /// <summary>
    /// <c>POST</c> here to establish a Session. Answers <c>204 No Content</c> with the Session
    /// cookie in <c>Set-Cookie</c>, or <c>401</c> with one problem <c>type</c> for <i>every</i>
    /// failure cause (AD-23).
    /// </summary>
    public static string Authenticate => "/api/v1/sessions";

    /// <summary>
    /// <c>GET</c> here to ask whether the caller holds a Session that is still good. Answers
    /// <c>204 No Content</c> when it does and <c>401</c> when it does not, and carries no body in
    /// either case.
    /// </summary>
    /// <remarks>
    /// <b>No body, deliberately.</b> AC1 fixes a Session as identifying the Account "and nothing
    /// more", and the surest way to keep that true on the wire is for the endpoint that reads one
    /// to return nothing at all. A body would be a profile contract this story has no requirement
    /// for, and the first place a Role or a Space would later be tempted onto a Session.
    /// <para>
    /// It exists because AC3 - "an expired Session grants access to nothing" - cannot otherwise be
    /// demonstrated: until story 1.5 builds the first Space-scoped endpoint there is nothing in the
    /// product for a Session to fail to grant access to, and an assertion with nothing to refuse
    /// would pass against a system that refuses nothing.
    /// </para>
    /// </remarks>
    public static string Current => "/api/v1/sessions/current";

    /// <summary>
    /// <c>GET</c> here for the anti-forgery request token every state-changing request must carry.
    /// Answers <c>200</c> with the token, and sets the paired cookie half.
    /// </summary>
    /// <remarks>
    /// See <see cref="Antiforgery"/> for why a cross-origin JSON API needs this at all, and why the
    /// automatic middleware .NET 10 registers does not cover it.
    /// </remarks>
    public static string AntiforgeryToken => "/api/v1/antiforgery/token";
}
