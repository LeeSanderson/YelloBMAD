namespace Yello.Domain.Memberships;

/// <summary>
/// One Account's place in one Space, at one <see cref="Memberships.Role"/>. The join that makes
/// an Account a User of that Space.
/// </summary>
/// <remarks>
/// <para>
/// <b>These rows are a product metric's denominator, not just a join table.</b>
/// <c>success-metrics.md:34</c> derives SM-3 - "the product's central bet, and the most important
/// number in this group" - from Membership rows, and SM-C1 from Space plus Membership. The single
/// row registration writes is the first data point in both.
/// </para>
/// <para>
/// <b><see cref="SpaceId"/> is non-nullable, by AD-2.</b> A Space-scoped table whose scoping
/// column can be null has a row that belongs to no Space, which no row-level security predicate
/// can place - so the column is required and the schema test asserts the policy that reads it.
/// </para>
/// <para>
/// <b>Exactly one Owner per Space, enforced by the database.</b> AD-5 / AR-12: a filtered unique
/// index on <c>(SpaceId) WHERE Role = 'Owner'</c>. Application code cannot hold this invariant
/// under concurrency, and AD-22 makes "an Account holding zero Spaces or two" a failed
/// transaction rather than a repairable state - which is only true if the constraint is where
/// the transaction can fail.
/// </para>
/// </remarks>
public sealed class Membership
{
    /// <summary>
    /// The Membership's identity.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// The Space this Membership is in. Never null: see the class remarks.
    /// </summary>
    public required Guid SpaceId { get; init; }

    /// <summary>
    /// The Account this Membership belongs to.
    /// </summary>
    public required Guid AccountId { get; init; }

    /// <summary>
    /// What this Account may do in this Space.
    /// </summary>
    /// <remarks>
    /// Settable because Epic 4 changes a Role in place (story 4.4), and Epic 5 moves ownership
    /// between two existing Memberships rather than deleting and recreating them.
    /// </remarks>
    public required Role Role { get; set; }

    /// <summary>
    /// When the Membership was created, in UTC.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
