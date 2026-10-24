namespace Yello.Host.Spaces;

/// <summary>
/// How a request establishes which Account is acting and which Space it names - the one step a
/// browser request and an API Token request are allowed to do differently. AD-4.
/// </summary>
/// <remarks>
/// <para>
/// <b>Built by story 1.5 for story 1.8.</b> Its acceptance criterion (<c>epics.md:868-871</c>)
/// requires a Token request and a browser request to "differ only in how <c>AccountId</c> and
/// <c>SpaceId</c> are established" and thereafter to traverse the identical pipeline. Everything
/// after <see cref="Establish"/> - the Membership read, the session context, the refusal - is
/// <see cref="ActiveSpaceResolution"/>'s, and is shared by construction.
/// </para>
/// <para>
/// <b>What comes back is a claim to be verified, never a grant.</b> Neither value is trusted by
/// anything downstream until the Membership row for the pair has been read, and a Role is never
/// part of what an establishment may return.
/// </para>
/// </remarks>
internal interface ISpaceRequestEstablishment
{
    /// <summary>
    /// Reads the acting Account and the requested Space off a request.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <returns>
    /// Either value <c>null</c> when the request does not carry it: no Account means the request is
    /// refused at the authentication boundary, and no Space means it is refused at the Space one.
    /// </returns>
    EstablishedSpaceRequest Establish(HttpContext httpContext);
}

/// <summary>
/// What a request claims about who is acting and where.
/// </summary>
/// <param name="AccountId">The acting Account, or <c>null</c> when none is authenticated.</param>
/// <param name="SpaceId">The requested Space, or <c>null</c> when the request names none it can parse.</param>
/// <param name="BoundSpaceId">
/// The one Space an API Token is bound to, or <c>null</c> for a Session. A Token whose bound Space is
/// not <paramref name="SpaceId"/> is refused exactly as a non-member is.
/// </param>
internal readonly record struct EstablishedSpaceRequest(Guid? AccountId, Guid? SpaceId, Guid? BoundSpaceId = null);
