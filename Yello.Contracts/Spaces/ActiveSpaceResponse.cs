namespace Yello.Contracts.Spaces;

/// <summary>
/// The active Space's own answer, for the context bar: its name, and the Role the caller holds
/// there as read on this request. Story 1.7, AC4 / AC5; AD-1.
/// </summary>
/// <remarks>
/// <para>
/// <b>The Role is the pipeline's resolution, reported - never carried.</b> It is the value the
/// Membership row held when this request read it (AD-1), so a client asks again on every arrival in
/// a Space and holds the answer no longer than it stays there. Nothing about it is put on the
/// Session, a cookie, a claim or a client cache.
/// </para>
/// <para>
/// <b><see cref="Role"/> is the Role's stable code</b> - <c>owner</c>, <c>admin</c>, <c>member</c>,
/// <c>viewer</c> - and the client renders it from its own copy, so no server prose is shown.
/// </para>
/// </remarks>
/// <param name="Id">The Space's id, as requested.</param>
/// <param name="Name">The Space's name - the Space the caller has just proved Membership in.</param>
/// <param name="Role">The caller's Role there, as its stable code.</param>
public sealed record ActiveSpaceResponse(Guid Id, string Name, string Role);
