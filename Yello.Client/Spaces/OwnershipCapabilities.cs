namespace Yello.Client.Spaces;

/// <summary>
/// Whether the Role held in the active Space may offer its ownership - so the Ownership section is absent rather than
/// disabled for every other Role.
/// </summary>
/// <remarks>
/// <b>A presentation decision, never an authorisation one.</b> The server checks <c>OfferOwnershipTransfer</c> against the
/// capability matrix on every request. Held against the matrix by
/// <c>RefusalBoundaryGateTests.The_clients_Ownership_section_predicate_agrees_with_the_capability_matrix</c>, and part of
/// <see cref="SpaceCapabilities.PermitsSpaceSettings"/>'s union.
/// </remarks>
public static class OwnershipCapabilities
{
    /// <summary>
    /// Whether a Role may offer, see and revoke an Ownership Offer.
    /// </summary>
    /// <param name="roleCode">The Role's stable code, from the active Space's answer.</param>
    /// <returns><c>true</c> for the Owner only.</returns>
    public static bool PermitsOfferOwnership(string roleCode) =>
        roleCode is "owner";
}
