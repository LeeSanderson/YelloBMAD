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
/// <b><c>DELETE</c> on <see cref="Authenticate"/> signs out</b> (story 1.7, open question 4). No FR
/// names sign-out, but UX-DR31 names it as a purge trigger and story 1.7's AC11 requires a control
/// that "ends the Session on the server". It does: the Account's <c>SessionsValidAfter</c> is
/// stamped, so every copy of the cookie issued before it is refused (FR-2; Lee's decision (a) of
/// 2026-10-24) - on every device. Answers <c>204</c> with a Session or without one, and requires the
/// anti-forgery token like every state-changing request.
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
    /// <c>204 No Content</c> when it does - with no body - and <c>401</c> with the same
    /// <c>application/problem+json</c> shape every other refusal in this API uses when it does not.
    /// </summary>
    /// <remarks>
    /// <b>NOTHING ABOUT THE SESSION IS EVER ON THE WIRE, WHICH IS THE PART AC1 CARES ABOUT.</b> This
    /// said "carries no body in either case" until code review on 2026-09-19, which is not what the
    /// endpoint does: the 401 carries a problem document, like every other refusal. The summary now
    /// describes the endpoint rather than an intention.
    /// <para>
    /// The underlying rule is unchanged and is the one worth keeping: the <i>success</i> answer is
    /// empty. AC1 fixes a Session as identifying the Account "and nothing more", and the surest way
    /// to keep that true on the wire is for the endpoint that reads one to return nothing at all. A
    /// success body would be a profile contract this story has no requirement for, and the first
    /// place a Role or a Space would later be tempted onto a Session. A problem document on the
    /// refusal path says nothing about any Session - there isn't one - so it costs that rule
    /// nothing, and making this endpoint the single exception to the API's error shape would cost a
    /// caller something real.
    /// </para>
    /// <para>
    /// It exists because it answers the <i>authentication</i> question alone, with no Space
    /// involved: "an expired Session grants access to nothing" is demonstrable here without a
    /// Space-scoped resource, and <c>SpaceRoutes</c> shows the same refusal on a route that has
    /// one.
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
