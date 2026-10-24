using Yello.Domain.AccessRefusals;
using Yello.Domain.Memberships;

namespace Yello.Application.Spaces;

/// <summary>
/// Writes the <c>AccessRefusal</c> record for a refusal at the Space boundary. AD-20 / AR-11.
/// </summary>
/// <remarks>
/// <para>
/// <b>The request pipeline is the only caller</b> (AR-3): every <c>403</c> and every Space-boundary
/// <c>404</c> is recorded by the pipeline that produced it, never by a slice. A gate fails the build
/// on any other dependent.
/// </para>
/// <para>
/// <b>Its own unit of work.</b> By the time a refusal is recorded the request's own unit of work has
/// been rolled back - a <c>404</c> never opened one that survived, and a <c>403</c> is rolled back
/// like every other failure - so the implementation writes on a fresh context and connection, and
/// never sets the Space session context a second time on the request's (error 15664).
/// </para>
/// <para>
/// <b>Synchronous, before the response.</b> Both boundary-<c>404</c> causes then do identical work,
/// which is what keeps them indistinguishable in duration (AD-3).
/// </para>
/// </remarks>
public interface IAccessRefusalRecorder
{
    /// <summary>
    /// Records one refusal.
    /// </summary>
    /// <param name="accountId">The refused Account.</param>
    /// <param name="targetSpaceId">The Space the request named, which may not exist.</param>
    /// <param name="capability">The capability the refused endpoint declares.</param>
    /// <param name="kind">Which refusal it was; the outcome status follows from it.</param>
    /// <param name="cancellationToken">
    /// The request's token. An implementation may ignore it, and the production one does: a caller
    /// who disconnects as the refusal is written has still been refused, and the record is the point.
    /// </param>
    /// <returns>A task that completes once the record is durable.</returns>
    Task RecordAsync(
        Guid accountId,
        Guid targetSpaceId,
        Capability capability,
        AccessRefusalKind kind,
        CancellationToken cancellationToken);
}
