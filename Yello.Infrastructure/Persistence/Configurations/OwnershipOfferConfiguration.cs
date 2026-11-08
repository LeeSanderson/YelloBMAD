using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.Spaces;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The Ownership Offer table. Space-scoped: covered by the row-level security policy - FILTER, and BLOCK after insert and
/// after update - and by a query filter on the request's Space. FR-8, AD-26, AD-27.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its only reference is its Space, cascading</b>: a Space's deletion takes its offers in the one deletion statement,
/// along with every Membership they could name. <b>There is no key to <c>Membership</c> or <c>Account</c></b>: a terminal
/// offer is retained after the Memberships it names are gone, which a no-action key would refuse (547), a cascade would
/// erase, and a composite <c>SET NULL</c> would null the Space too. That no <c>Pending</c> offer names a vanished
/// Membership rests on the guarded insert and on the lapse a removal or leave writes, under one lock.
/// </para>
/// <para>
/// <b>At most one <c>Pending</c> offer per Space</b>, by a filtered unique index. Expiry is derived, so a pending offer
/// past its expiry still holds that place until the next offer lapses it in its own transaction.
/// </para>
/// <para>
/// <b>The expiry is the database's</b>: a column default seven days after <c>SYSDATETIMEOFFSET()</c> - PRD section 12
/// assumption 3, unconfirmed - so no C# clock stamps it.
/// </para>
/// </remarks>
/// <param name="context">The context whose request-scoped Space the query filter closes over.</param>
internal sealed class OwnershipOfferConfiguration(YelloDbContext context) : IEntityTypeConfiguration<OwnershipOffer>
{
    /// <summary>
    /// The days between an offer and its expiry. The migration carries the same 7.
    /// </summary>
    internal const int ExpiryDays = 7;

    /// <summary>
    /// The longest stored State name.
    /// </summary>
    private const int NameLength = 16;

    public void Configure(EntityTypeBuilder<OwnershipOffer> builder)
    {
        builder.ToTable(SchemaNames.OwnershipOfferTable);

        builder.HasKey(offer => offer.Id);

        // Never store-generated: the id comes from the sequential identifier port (AR-34).
        builder.Property(offer => offer.Id).ValueGeneratedNever();

        builder.Property(offer => offer.SpaceId)
            .HasColumnName(SchemaNames.SpaceIdColumn)
            .IsRequired();

        // AD-2's second layer, from application state. See MembershipConfiguration.
        builder.HasQueryFilter(offer => offer.SpaceId == context.ScopedSpaceId);

        builder.Property(offer => offer.RecipientMembershipId).IsRequired();

        builder.Property(offer => offer.OfferedByMembershipId).IsRequired();

        builder.Property(offer => offer.OffererName)
            .IsRequired()
            .HasMaxLength(OwnershipOffer.OffererNameMaxLength);

        builder.Property(offer => offer.State)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(NameLength);

        builder.Property(offer => offer.ExpiresAt)
            .IsRequired()
            .HasDefaultValueSql($"DATEADD(day, {ExpiryDays}, {DatabaseClock.FunctionName}())")
            .ValueGeneratedOnAdd();

        builder.Property(offer => offer.CreatedAt).IsRequired();

        builder.HasIndex(offer => offer.SpaceId)
            .IsUnique()
            .HasFilter($"[State] = N'{nameof(OwnershipOfferState.Pending)}'")
            .HasDatabaseName(SchemaNames.OwnershipOfferPendingUniqueIndex);

        // The lapse's seek: this Space's offers naming one Membership.
        builder.HasIndex(offer => new { offer.SpaceId, offer.RecipientMembershipId });

        builder.HasOne<Space>()
            .WithMany()
            .HasForeignKey(offer => offer.SpaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
