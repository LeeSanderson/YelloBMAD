namespace Yello.Domain.Memberships;

/// <summary>
/// The target rule of the "Remove a Membership" cell for one acting Role: the Roles the actor may remove, and the Roles the
/// actor must still hold for the authority to stand.
/// </summary>
/// <remarks>
/// <para>
/// <b>Declared by <see cref="RoleCapabilityMatrix.RemovalAuthorityOf"/>, carried as sets, never as a Role</b>, exactly as
/// <see cref="RoleChangeAuthority"/> is: the pipeline alone reads the acting Role, so the slice and the store see only the
/// sets and neither Role-read gate is widened.
/// </para>
/// <para>
/// <b>Owner is in no <see cref="Removable"/> set, for any actor.</b> FR-14: "The Owner's Membership cannot be removed by
/// anyone, the Owner included, while it holds ownership." The schema does not forbid deleting an Owner row - a Space's
/// deletion cascades it - so this rule and the removal statements' own <c>Role &lt;&gt; Owner</c> clause, with the S-2
/// invariant test, are what keep every Space at one Owner.
/// </para>
/// <para>
/// <b>The actor's own non-Owner row is always removable</b>, whatever these sets say: "any Account can remove its own" is
/// decided by identity, not Role, so it is the store's clause beside the sets rather than a member of them.
/// </para>
/// </remarks>
/// <param name="Removable">The Roles a target Membership may hold for the actor to remove it.</param>
/// <param name="HeldBy">The Roles the actor's own Membership must still hold when the removal is written.</param>
public sealed record RemovalAuthority(
    IReadOnlySet<Role> Removable,
    IReadOnlySet<Role> HeldBy)
{
    /// <summary>
    /// The authority of a Role the matrix denies the capability: nothing to remove.
    /// </summary>
    public static RemovalAuthority None { get; } = new(new HashSet<Role>(), new HashSet<Role>());

    /// <summary>
    /// What a removal the guarded statement did not write comes to, decided after the fact from the target as it now stands
    /// and whether the actor still holds its authority.
    /// </summary>
    /// <remarks>
    /// In order: an actor that no longer holds the authority is refused whatever it asked; the Owner's row is
    /// <see cref="RemovalRefusal.OwnershipHeld"/> for the Owner itself (it holds full authority, so "insufficient Role" would
    /// be untrue) and beyond any other actor's authority; anything else not written is beyond the authority, including a
    /// target the sets admit, which only a writer outside the Memberships lock could cause - nothing was written, so it is
    /// never answered as a success.
    /// </remarks>
    /// <param name="target">The target's Role, read after the statement wrote nothing.</param>
    /// <param name="targetIsActor">Whether the target is the actor's own Membership.</param>
    /// <param name="actorNow">The Role the actor's own Membership holds now, read after the statement wrote nothing.</param>
    /// <returns>Why nothing was written.</returns>
    public RemovalRefusal Judge(Role target, bool targetIsActor, Role actorNow)
    {
        if (!HeldBy.Contains(actorNow))
        {
            return RemovalRefusal.BeyondAuthority;
        }

        return target == Role.Owner && targetIsActor
            ? RemovalRefusal.OwnershipHeld
            : RemovalRefusal.BeyondAuthority;
    }
}

/// <summary>
/// Why a removal wrote nothing.
/// </summary>
public enum RemovalRefusal
{
    /// <summary>
    /// The Owner's own Membership, by the Owner: it leaves only by an accepted Ownership Offer or by deleting the Space.
    /// </summary>
    OwnershipHeld,

    /// <summary>
    /// The target or the actor's own standing is outside the authority: the pipeline's recorded <c>403</c>.
    /// </summary>
    BeyondAuthority,
}
