using Yello.Domain.Memberships;

namespace Yello.Application.Spaces;

/// <summary>
/// The Space one request acts in, and the standing the acting Account holds there. AD-1 / AR-4,
/// AD-2 / AR-5.
/// </summary>
/// <remarks>
/// <para>
/// <b>Resolved once per request, from the Membership row, and never from anything else.</b> AD-1
/// (<c>ARCHITECTURE-SPINE.md:75</c>): "Role is never read from a cookie, a claim, an API Token
/// payload, or any cache outliving the request." <see cref="Role"/> here is the value the
/// Membership row held when this request read it, so a Role changed between two requests reaches
/// the second one with no re-authentication - there is nowhere else for a stale copy to live.
/// </para>
/// <para>
/// <b>Why the Role is on a per-request value rather than the Account.</b> Membership is
/// many-to-many with the Role carried on the relationship, so one Account legitimately holds
/// different standing in different Spaces at the same time (<c>decisions-settled.md:20</c>). The
/// Space has to be resolved before any authorisation decision can even be phrased, which is what
/// makes this the input to every one of them.
/// </para>
/// <para>
/// <b>Not a tenancy abstraction.</b> A generic tenant column or ambient tenant middleware is an
/// explicitly rejected shape (<c>decisions-settled.md:18</c>). The column is uniform; the
/// authorisation is not, because it is a function of <c>(Account, Space)</c>.
/// </para>
/// </remarks>
/// <param name="AccountId">The Account acting on this request.</param>
/// <param name="SpaceId">The Space the request acts in - the <c>{spaceId}</c> route segment, verified.</param>
/// <param name="Role">The Role the Account's Membership in that Space carries, as read on this request.</param>
public sealed record ActiveSpaceContext(Guid AccountId, Guid SpaceId, Role Role);
