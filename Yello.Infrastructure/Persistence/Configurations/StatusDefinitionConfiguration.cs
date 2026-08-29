using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.Spaces;
using Yello.Domain.Statuses;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The StatusDefinition table. Space-scoped, and therefore covered by the row-level security
/// policy the migration creates.
/// </summary>
internal sealed class StatusDefinitionConfiguration : IEntityTypeConfiguration<StatusDefinition>
{
    /// <summary>
    /// A Status name is a column heading. The Board renders it uppercased by
    /// <c>text-transform</c>, never by the stored string, so the cap is on the sentence-case
    /// form a person actually types.
    /// </summary>
    private const int NameMaxLength = 100;

    public void Configure(EntityTypeBuilder<StatusDefinition> builder)
    {
        builder.ToTable(SchemaNames.StatusDefinitionTable);

        builder.HasKey(status => status.Id);

        // Never store-generated: the id is what survives a rename (AR-23), so it is produced
        // once, application-side, and never again.
        builder.Property(status => status.Id).ValueGeneratedNever();

        builder.Property(status => status.SpaceId)
            .HasColumnName(SchemaNames.SpaceIdColumn)
            .IsRequired();

        builder.Property(status => status.Name)
            .IsRequired()
            .HasMaxLength(NameMaxLength);

        builder.Property(status => status.Position).IsRequired();

        // Two Statuses in one Space may not share a name: a Board with two columns called the
        // same thing is unusable, and Epic 6's rename would have no way to say which it meant.
        // Scoped to the Space, so two Spaces naming a Status identically is fine - which is the
        // normal case, since every Space starts from the same three defaults.
        builder.HasIndex(status => new { status.SpaceId, status.Name }).IsUnique();

        builder.HasOne<Space>()
            .WithMany()
            .HasForeignKey(status => status.SpaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
