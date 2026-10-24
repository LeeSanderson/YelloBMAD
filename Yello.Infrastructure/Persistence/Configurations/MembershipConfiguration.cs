using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.Accounts;
using Yello.Domain.Memberships;
using Yello.Domain.Spaces;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The Membership table, where AD-5's "exactly one Owner per Space" becomes a database fact.
/// </summary>
/// <param name="context">
/// The context whose request-scoped Space the query filter closes over. EF Core substitutes the
/// executing context for this instance on every query, so the model built once serves every
/// request with that request's scope.
/// </param>
internal sealed class MembershipConfiguration(YelloDbContext context) : IEntityTypeConfiguration<Membership>
{
    /// <summary>
    /// The longest Role name plus room for one more. Roles are persisted as their names.
    /// </summary>
    private const int RoleMaxLength = 20;

    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable(SchemaNames.MembershipTable);

        builder.HasKey(membership => membership.Id);
        builder.Property(membership => membership.Id).ValueGeneratedNever();

        // AD-2: a Space-scoped table's scoping column is non-nullable. A nullable SpaceId is a
        // row that belongs to no Space, which no security predicate can place - it would be
        // filtered out of every query and visible to nobody, which is a leak in the other
        // direction.
        builder.Property(membership => membership.SpaceId)
            .HasColumnName(SchemaNames.SpaceIdColumn)
            .IsRequired();

        builder.Property(membership => membership.AccountId).IsRequired();

        // AD-2's second layer: the request's Space, from application state. Independent of the
        // row-level security policy by construction - it never reads SESSION_CONTEXT - so a
        // session context set to the wrong Space still returns nothing from another one.
        //
        // WIDENED BY STORY 1.7, ON THIS TABLE ALONE (AD-24): the request's Account as well, which is
        // how the Space switcher lists every Space an Account belongs to. It restates the database
        // predicate's disjunction from application state, so each layer still holds alone. A unit of
        // work narrows exactly one of the two scopes, so this admits one Space's Memberships or one
        // Account's - never both. PersistenceModelGateTests permits this one disjunction on this one
        // entity and refuses any other shape.
        builder.HasQueryFilter(membership =>
            membership.SpaceId == context.ScopedSpaceId || membership.AccountId == context.ScopedAccountId);

        // AD-2's composite-key rule, enabled rather than exercised. A reference to a Membership
        // from another Space-scoped row - the first is FR-21's Assignee in story 4.6 - has to be
        // the pair (SpaceId, MembershipId) so that "same Space" holds by construction, and a
        // composite foreign key can only target a matching unique key on the principal. The
        // primary key is Id alone, so this is that key.
        builder.HasAlternateKey(membership => new { membership.SpaceId, membership.Id });

        // Stored as the name rather than the ordinal. Two things follow: reordering the enum is
        // not a migration, and the filtered index below reads as `WHERE [Role] = N'Owner'` in
        // the database rather than as a magic integer nobody can check against the Glossary.
        builder.Property(membership => membership.Role)
            .IsRequired()
            .HasMaxLength(RoleMaxLength)
            .HasConversion<string>();

        // AD-5 / AR-12, and the reason it is an index rather than a check in the handler:
        // application code cannot hold this under concurrency, and AD-22 requires "an Account
        // holding zero Spaces or two" to be a FAILED TRANSACTION rather than a repairable state.
        // That is only true if the constraint is somewhere the transaction can fail.
        //
        // The filter is built from nameof(Role.Owner) rather than written as a literal, so
        // renaming the enum member breaks the build here instead of leaving an index that
        // silently matches nothing - which would let a Space acquire two Owners with every
        // assertion green.
        builder.HasIndex(membership => membership.SpaceId)
            .IsUnique()
            .HasDatabaseName(SchemaNames.MembershipOwnerUniqueIndex)
            .HasFilter($"[{nameof(Membership.Role)}] = N'{nameof(Role.Owner)}'");

        // An Account may hold at most one Membership in a Space. Without this a second
        // Membership at a different Role is a legal row, and every Role check downstream would
        // have to decide which of the two wins.
        builder.HasIndex(membership => new { membership.SpaceId, membership.AccountId })
            .IsUnique();

        // Deleting a Space takes its Memberships with it: a Membership in no Space cannot exist
        // (see the non-nullable column above), so cascade is the only behaviour that leaves the
        // table consistent. Story 3.3 owns Space deletion.
        builder.HasOne<Space>()
            .WithMany()
            .HasForeignKey(membership => membership.SpaceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Account deletion does NOT cascade, for two reasons. SQL Server refuses two cascade
        // paths into one table outright, so one of the pair has to be restricted whatever we
        // think - and this is the right one to restrict, because FR-3 makes account deletion an
        // explicit sequence rather than a side effect: "every Membership goes, the address is
        // freed for reuse, and the new Account inherits nothing". Story 5.4 performs that
        // deliberately, and a silent cascade would remove the Owner Membership of a Space that
        // still has members in it.
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(membership => membership.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
