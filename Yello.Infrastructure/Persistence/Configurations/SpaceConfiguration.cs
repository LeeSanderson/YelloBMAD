using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.Spaces;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The Space table.
/// </summary>
/// <remarks>
/// <b>Note what is absent.</b> No <c>IsPersonal</c> column, no discriminator, no owner column,
/// no subtype and no filtered index that would distinguish one Space from another. AC4 requires
/// that no attribute distinguishes the Space provisioned at registration from a Space created by
/// any other route, and the cheapest place to break that is here - a nullable
/// <c>ProvisionedAtRegistration</c> flag would look like harmless provenance and would be exactly
/// the distinct type <c>decisions-settled.md:26</c> records as rejected.
/// </remarks>
internal sealed class SpaceConfiguration : IEntityTypeConfiguration<Space>
{
    /// <summary>
    /// A Space name is a heading, not prose. Long enough for a composed
    /// "&lt;display name&gt;'s Space" in any language - German and Finnish run 30-40% longer
    /// than English - and for a name a person types themselves from Epic 3.
    /// </summary>
    private const int NameMaxLength = 200;

    public void Configure(EntityTypeBuilder<Space> builder)
    {
        builder.ToTable(SchemaNames.SpaceTable);

        builder.HasKey(space => space.Id);

        // Generated application-side, before the insert, so the slice can set the row-level
        // security session context to it inside the same transaction. See IIdentifierGenerator.
        builder.Property(space => space.Id).ValueGeneratedNever();

        builder.Property(space => space.Name)
            .IsRequired()
            .HasMaxLength(NameMaxLength);
    }
}
