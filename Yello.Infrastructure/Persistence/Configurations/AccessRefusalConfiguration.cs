using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.AccessRefusals;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The AccessRefusal table: the write-only record of every refusal at the Space boundary. AD-20 /
/// AR-11.
/// </summary>
/// <remarks>
/// <para>
/// <b>No <c>SpaceId</c> column, no row-level security predicate and no query filter.</b> The target
/// is <c>TargetSpaceId</c>, deliberately not named <c>SpaceId</c>: every Space-scoped gate in this
/// repository detects a Space-scoped table by that exact column name, and this table is not one. A
/// refusal for a Space that does not exist is still recorded, and the retention purge has to see
/// every row - neither is possible under a predicate that reads one Space's session context.
/// </para>
/// <para>
/// <b>No foreign keys.</b> Not to <c>Space</c>, for the reason above; and not to <c>Account</c>,
/// because the record is an audit of what was attempted rather than a dependent of the Account, and
/// a restricting key would stand in the way of FR-3's Account deletion in story 5.4.
/// </para>
/// </remarks>
internal sealed class AccessRefusalConfiguration : IEntityTypeConfiguration<AccessRefusal>
{
    /// <summary>
    /// The longest capability name with room to spare. Stored as the name, like a Role.
    /// </summary>
    private const int CapabilityMaxLength = 64;

    /// <summary>
    /// The longest refusal kind name with room to spare.
    /// </summary>
    private const int KindMaxLength = 32;

    public void Configure(EntityTypeBuilder<AccessRefusal> builder)
    {
        builder.ToTable(SchemaNames.AccessRefusalTable);

        builder.HasKey(refusal => refusal.Id);
        builder.Property(refusal => refusal.Id).ValueGeneratedNever();

        builder.Property(refusal => refusal.AccountId).IsRequired();
        builder.Property(refusal => refusal.TargetSpaceId).IsRequired();

        builder.Property(refusal => refusal.Capability)
            .IsRequired()
            .HasMaxLength(CapabilityMaxLength)
            .HasConversion<string>();

        builder.Property(refusal => refusal.Outcome).IsRequired();

        builder.Property(refusal => refusal.Kind)
            .IsRequired()
            .HasMaxLength(KindMaxLength)
            .HasConversion<string>();

        builder.Property(refusal => refusal.OccurredAt).IsRequired();

        // The retention purge deletes by age, so it seeks on this rather than scanning the table.
        builder.HasIndex(refusal => refusal.OccurredAt);
    }
}
