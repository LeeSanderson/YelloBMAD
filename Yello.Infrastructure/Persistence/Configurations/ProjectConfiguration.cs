using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.Projects;
using Yello.Domain.Spaces;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The Project table. Space-scoped: covered by the row-level security policy - FILTER, and BLOCK
/// after insert and after update - and by a query filter on the request's Space. Story 2.1; AD-2.
/// </summary>
/// <param name="context">The context whose request-scoped Space the query filter closes over.</param>
internal sealed class ProjectConfiguration(YelloDbContext context) : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable(SchemaNames.ProjectTable);

        builder.HasKey(project => project.Id);

        // Never store-generated: the id comes from the sequential identifier port (AC6).
        builder.Property(project => project.Id).ValueGeneratedNever();

        builder.Property(project => project.SpaceId)
            .HasColumnName(SchemaNames.SpaceIdColumn)
            .IsRequired();

        // AD-2's second layer, from application state. See MembershipConfiguration.
        builder.HasQueryFilter(project => project.SpaceId == context.ScopedSpaceId);

        builder.Property(project => project.Name)
            .IsRequired()
            .HasMaxLength(ProjectName.MaxLength);

        builder.Property(project => project.CreatedAt).IsRequired();

        // AR-8: a Space-scoped reference carries its Space, so story 2.2's Task references a Project
        // through (SpaceId, Id) and can never point at a Project in another Space.
        builder.HasAlternateKey(project => new { project.SpaceId, project.Id });

        builder.HasOne<Space>()
            .WithMany()
            .HasForeignKey(project => project.SpaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
