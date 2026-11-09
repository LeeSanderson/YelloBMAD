namespace Yello.Domain.Memberships;

/// <summary>
/// The target rule of the "Change a Membership's Role" cell for one acting Role: the Roles the actor may change a
/// Membership <i>from</i> and <i>to</i>, and the Roles the actor must still hold for the authority to stand. FR-13, AR-12,
/// AD-5; story 4.4, AC4, AC5; Lee's Q1 (a) and Q2 (a).
/// </summary>
/// <remarks>
/// <para>
/// <b>Declared by <see cref="RoleCapabilityMatrix.RoleChangeAuthorityOf"/>, carried as sets, never as a Role.</b> The
/// pipeline, which alone reads the acting Role (AD-1, AR-3), asks the matrix for this rule and hands it to the slice; the
/// slice and the store see only the sets, so neither re-implements the Role check and neither Role-read gate is widened.
/// </para>
/// <para>
/// <b>Owner is in no set, for any actor.</b> FR-13: "No Role change can produce a second Owner or remove the sole Owner.
/// Ownership moves only by an Ownership Offer" - its acceptance, which the offer store writes as its own demote and promote.
/// So the one guarded statement that composes these sets
/// can neither read an Owner row as its target nor write <c>Owner</c>: the filtered unique index carries "at most one
/// Owner", and this rule, with the S-2 invariant test, carries "at least one" (Lee's Q2 (a)).
/// </para>
/// <para>
/// <b><see cref="HeldBy"/> is the in-flight re-check</b> (Lee's Q1 (a), the actor clause kept): the Roles whose own
/// authority covers this one. The statement admits the change only while the actor's Membership still holds one of them,
/// so an actor demoted after the pipeline read its Role cannot act on the authority it lost (NFR-2, "no tolerance").
/// </para>
/// </remarks>
/// <param name="From">The Roles a target Membership may hold for the actor to change it.</param>
/// <param name="To">The Roles the actor may change a Membership to.</param>
/// <param name="HeldBy">The Roles the actor's own Membership must still hold when the change is written.</param>
public sealed record RoleChangeAuthority(
    IReadOnlySet<Role> From,
    IReadOnlySet<Role> To,
    IReadOnlySet<Role> HeldBy)
{
    /// <summary>
    /// The authority of a Role the matrix denies the capability: nothing to change from, nothing to change to.
    /// </summary>
    public static RoleChangeAuthority None { get; } = new(
        new HashSet<Role>(),
        new HashSet<Role>(),
        new HashSet<Role>());

    /// <summary>
    /// Whether this authority admits changing a Membership holding <paramref name="current"/> to <paramref name="proposed"/>.
    /// </summary>
    /// <param name="current">The target's Role now.</param>
    /// <param name="proposed">The Role asked for.</param>
    /// <returns><c>true</c> when both are inside the sets.</returns>
    public bool Admits(Role current, Role proposed) => From.Contains(current) && To.Contains(proposed);

    /// <summary>
    /// What a change the guarded statement did not write comes to - decided after the fact from the target's Role as it now
    /// stands and whether the actor still holds its authority. Story 4.4, AC4, Q10 (a), Q11 (a).
    /// </summary>
    /// <remarks>
    /// In order: an actor that no longer holds the authority is refused whatever it asked; an Owner target is
    /// <see cref="RoleChangeRefusal.OwnerRoleFixed"/> for an authority that reaches every other Role (the Owner's own - it
    /// holds full authority, so "insufficient Role" would be untrue) and beyond any lesser authority; a target or a Role
    /// outside the sets is beyond the authority; and a target already at the Role asked for is a no-op.
    /// </remarks>
    /// <param name="current">The target's Role, read after the statement wrote nothing.</param>
    /// <param name="proposed">The Role asked for.</param>
    /// <param name="actorStillHolds">Whether the actor's Membership still holds a Role in <see cref="HeldBy"/>.</param>
    /// <returns>Why nothing was written.</returns>
    public RoleChangeRefusal Judge(Role current, Role proposed, bool actorStillHolds)
    {
        if (!actorStillHolds)
        {
            return RoleChangeRefusal.BeyondAuthority;
        }

        if (current == Role.Owner)
        {
            return ReachesEveryRoleButOwner ? RoleChangeRefusal.OwnerRoleFixed : RoleChangeRefusal.BeyondAuthority;
        }

        if (!Admits(current, proposed))
        {
            return RoleChangeRefusal.BeyondAuthority;
        }

        // Admitted and different, yet not written: only a writer outside the Memberships lock could cause it. Fail closed -
        // nothing was written, so it is never answered as a success.
        return current == proposed ? RoleChangeRefusal.Unchanged : RoleChangeRefusal.BeyondAuthority;
    }

    private bool ReachesEveryRoleButOwner =>
        Enum.GetValues<Role>().Where(role => role != Role.Owner).All(role => From.Contains(role) && To.Contains(role));
}

/// <summary>
/// Why a Role change wrote nothing. Story 4.4, AC4.
/// </summary>
public enum RoleChangeRefusal
{
    /// <summary>
    /// The target already holds the Role asked for: a no-op, answered with the unchanged row (Q11 (a)).
    /// </summary>
    Unchanged,

    /// <summary>
    /// The target is the Owner's Membership and the actor's authority is the Owner's: ownership moves only by an Ownership
    /// Offer (Q10 (a)).
    /// </summary>
    OwnerRoleFixed,

    /// <summary>
    /// The target, the Role asked for, or the actor's own standing is outside the authority: the pipeline's recorded
    /// <c>403</c> (AD-20).
    /// </summary>
    BeyondAuthority,
}
