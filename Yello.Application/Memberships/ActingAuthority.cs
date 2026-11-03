using Yello.Domain.Memberships;

namespace Yello.Application.Memberships;

/// <summary>
/// The acting Account's authority over other Memberships on this request, as Role sets: the pipeline-owned seam that gives
/// the matrix's Qualified cells their target rule without a slice reading a Role. Story 4.4, AC4; Lee's Q1 (a); AD-1,
/// AR-3, AD-20; <c>deferred-work.md:329</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Written by the pipeline and by nothing else.</b> <c>ActiveSpaceResolution</c> reads the acting Role off the Membership
/// row (AD-1), asks <see cref="RoleCapabilityMatrix.RoleChangeAuthorityOf"/> for its target rule and <see cref="Set"/>s it
/// here before the endpoint runs; <c>RefusalBoundaryGateTests.Only_the_pipeline_sets_the_acting_authority</c> holds that
/// from the IL. A slice reads <see cref="RoleChanges"/> - Role <b>sets</b>, never the Role - and hands them to its store,
/// whose one guarded statement composes them. Both Role-read gates stay unwidened.
/// </para>
/// <para>
/// <b>Request-scoped, never cached</b>: one instance per request, set from the Role read on that request, so a Role changed
/// between two requests governs the second (NFR-2). Unset, it fails closed.
/// </para>
/// <para>
/// <b>Consumed by every slice behind a Qualified cell</b> (<c>A_Qualified_cell_reaches_no_endpoint_without_its_target_rule</c>):
/// today the Role change; story 4.5's removal joins it.
/// </para>
/// </remarks>
public sealed class ActingAuthority
{
    private RoleChangeAuthority? _roleChanges;

    /// <summary>
    /// The Roles the acting Account may change a Membership from and to, and must still hold. Throws on a request the
    /// pipeline did not resolve a Space for.
    /// </summary>
    public RoleChangeAuthority RoleChanges =>
        _roleChanges ?? throw new InvalidOperationException(
            "No acting authority has been set on this request. The pipeline sets it once it has resolved the active Space.");

    /// <summary>
    /// Records the authority. The pipeline is the only caller.
    /// </summary>
    /// <param name="roleChanges">The target rule the matrix declares for the Role read on this request.</param>
    public void Set(RoleChangeAuthority roleChanges)
    {
        ArgumentNullException.ThrowIfNull(roleChanges);

        _roleChanges = roleChanges;
    }
}
