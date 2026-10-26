using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.Labels;
using Yello.Domain.Spaces;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The Label table. Space-scoped: covered by the row-level security policy - FILTER, and BLOCK after insert and
/// after update - and by a query filter on the request's Space. Story 2.4; FR-22, AD-2, AR-8.
/// </summary>
/// <remarks>
/// <para>
/// <b>Unique by <c>(SpaceId, Name)</c></b> (open question 2): the database's default case-insensitive collation
/// makes "Bug" and "bug" collide, which is the intended rule, and the index rather than a pre-check is what refuses
/// two concurrent definitions of one name.
/// </para>
/// <para>
/// <b><c>(SpaceId, Id)</c> is an alternate key</b> so an application references a Label through a key that carries
/// its Space (AR-8).
/// </para>
/// </remarks>
/// <param name="context">The context whose request-scoped Space the query filter closes over.</param>
internal sealed class LabelConfiguration(YelloDbContext context) : IEntityTypeConfiguration<Label>
{
    /// <summary>
    /// The longest palette key the column holds. The keys are short words; this leaves room.
    /// </summary>
    private const int ColourLength = 16;

    public void Configure(EntityTypeBuilder<Label> builder)
    {
        builder.ToTable(SchemaNames.LabelTable);

        builder.HasKey(label => label.Id);

        // Never store-generated: the id comes from the sequential identifier port (AR-34).
        builder.Property(label => label.Id).ValueGeneratedNever();

        builder.Property(label => label.SpaceId)
            .HasColumnName(SchemaNames.SpaceIdColumn)
            .IsRequired();

        // AD-2's second layer, from application state. See MembershipConfiguration.
        builder.HasQueryFilter(label => label.SpaceId == context.ScopedSpaceId);

        builder.Property(label => label.Name)
            .IsRequired()
            .HasMaxLength(LabelName.MaxLength);

        builder.Property(label => label.Colour)
            .IsRequired()
            .IsUnicode(false)
            .HasMaxLength(ColourLength);

        builder.Property(label => label.CreatedAt).IsRequired();

        builder.HasAlternateKey(label => new { label.SpaceId, label.Id });

        builder.HasIndex(label => new { label.SpaceId, label.Name })
            .IsUnique();

        builder.HasOne<Space>()
            .WithMany()
            .HasForeignKey(label => label.SpaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
