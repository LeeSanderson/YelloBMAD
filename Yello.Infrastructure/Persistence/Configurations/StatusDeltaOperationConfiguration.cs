using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.Projects;
using Yello.Domain.Statuses;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The StatusDeltaOperation table: every Project's Status delta, one row per operation. Space-scoped: covered by the
/// row-level security policy - FILTER, and BLOCK after insert and after update - and by a query filter on the request's
/// Space.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cascade from <c>Project</c>, NO ACTION to <c>StatusDefinition</c>, and no key to <c>Space</c></b>, for the reason
/// <see cref="TaskConfiguration"/> gives: SQL Server refuses a second cascade path into one table (1785), and Space to
/// StatusDefinition already cascades. A Space's deletion still reaches these rows through the Project, and the NO ACTION
/// keys are checked at the end of that one statement, after the cascades have emptied both sides.
/// </para>
/// <para>
/// <b>Unique per <c>(SpaceId, ProjectId, StatusId, Kind)</c></b>: a write replaces the operation of the same kind, so the delta
/// stays bounded and holds no position for any Status that was never placed.
/// </para>
/// </remarks>
/// <param name="context">The context whose request-scoped Space the query filter closes over.</param>
internal sealed class StatusDeltaOperationConfiguration(YelloDbContext context) : IEntityTypeConfiguration<StatusDeltaOperation>
{
    private const int KindLength = 16;

    public void Configure(EntityTypeBuilder<StatusDeltaOperation> builder)
    {
        builder.ToTable(SchemaNames.StatusDeltaOperationTable);

        builder.HasKey(operation => operation.Id);

        builder.Property(operation => operation.Id).ValueGeneratedNever();

        builder.Property(operation => operation.SpaceId)
            .HasColumnName(SchemaNames.SpaceIdColumn)
            .IsRequired();

        // AD-2's second layer, from application state. See MembershipConfiguration.
        builder.HasQueryFilter(operation => operation.SpaceId == context.ScopedSpaceId);

        builder.Property(operation => operation.Kind)
            .IsRequired()
            .IsUnicode(false)
            .HasMaxLength(KindLength)
            .HasConversion<string>();

        builder.Property(operation => operation.Name)
            .IsRequired(false)
            .HasMaxLength(StatusName.MaxLength)
            .UseCollation(StatusDefinitionConfiguration.NameCollation);

        builder.Property(operation => operation.AfterStatusId).IsRequired(false);

        builder.Property(operation => operation.Sequence).IsRequired();

        builder.Property(operation => operation.CreatedAt).IsRequired();

        builder.HasIndex(operation => new { operation.SpaceId, operation.ProjectId, operation.StatusId, operation.Kind })
            .IsUnique();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(operation => new { operation.SpaceId, operation.ProjectId })
            .HasPrincipalKey(project => new { project.SpaceId, project.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<StatusDefinition>()
            .WithMany()
            .HasForeignKey(operation => new { operation.SpaceId, operation.StatusId })
            .HasPrincipalKey(status => new { status.SpaceId, status.Id })
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<StatusDefinition>()
            .WithMany()
            .HasForeignKey(operation => new { operation.SpaceId, operation.AfterStatusId })
            .HasPrincipalKey(status => new { status.SpaceId, status.Id })
            .OnDelete(DeleteBehavior.NoAction);
    }
}
