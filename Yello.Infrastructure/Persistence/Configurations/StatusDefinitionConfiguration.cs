using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.Projects;
using Yello.Domain.Spaces;
using Yello.Domain.Statuses;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The StatusDefinition table. Space-scoped, and therefore covered by the row-level security
/// policy the migration creates and by a query filter on the request's Space.
/// </summary>
/// <param name="context">The context whose request-scoped Space the query filter closes over.</param>
internal sealed class StatusDefinitionConfiguration(YelloDbContext context) : IEntityTypeConfiguration<StatusDefinition>
{
    /// <summary>
    /// The case-insensitive collation of every Status and Label name column, declared rather than inherited from the
    /// database's default, so a restore onto a differently collated server cannot turn the name indexes case-sensitive
    /// and disagree with the application's own comparison.
    /// </summary>
    internal const string NameCollation = "SQL_Latin1_General_CP1_CI_AS";

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

        // AD-2's second layer, from application state. See MembershipConfiguration.
        builder.HasQueryFilter(status => status.SpaceId == context.ScopedSpaceId);

        // A Status name is a column heading. The Board renders it uppercased by text-transform, never by
        // the stored string, so the cap is on the sentence-case form a person actually types.
        builder.Property(status => status.Name)
            .IsRequired()
            .HasMaxLength(StatusName.MaxLength)
            .UseCollation(NameCollation);

        builder.Property(status => status.Position).IsRequired();

        builder.Property(status => status.OwnerProjectId).IsRequired(false);

        // Backstops only: two defaults of one Space, or two Statuses one Project added, may not share a name.
        // The effective set's own uniqueness is the application's, under the Project's lock - a renamed
        // default's name lives in the delta, where no index of this table can see it - and two Projects
        // each adding a Status of one name is the normal case.
        builder.HasIndex(status => new { status.SpaceId, status.Name })
            .IsUnique()
            .HasFilter($"[{nameof(StatusDefinition.OwnerProjectId)}] IS NULL");

        builder.HasIndex(status => new { status.SpaceId, status.OwnerProjectId, status.Name })
            .IsUnique()
            .HasFilter($"[{nameof(StatusDefinition.OwnerProjectId)}] IS NOT NULL");

        // AR-8: a Space-scoped reference carries its Space, so story 2.2's Task references its Status
        // through (SpaceId, Id) and can never hold a Status of another Space.
        builder.HasAlternateKey(status => new { status.SpaceId, status.Id });

        builder.HasOne<Space>()
            .WithMany()
            .HasForeignKey(status => status.SpaceId)
            .OnDelete(DeleteBehavior.Cascade);

        // NO ACTION: Space already cascades to this table, and Space to Project to here would be a second
        // path (1785). A Project's deletion removes its owned definitions itself, after its Tasks and its
        // delta, which reference them; a Space's deletion is checked at the end of its one statement.
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(status => new { status.SpaceId, status.OwnerProjectId })
            .HasPrincipalKey(project => new { project.SpaceId, project.Id })
            .OnDelete(DeleteBehavior.NoAction);
    }
}
