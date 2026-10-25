using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.Projects;
using Yello.Domain.Statuses;
using Yello.Domain.Tasks;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The Task table. Space-scoped: covered by the row-level security policy - FILTER, and BLOCK after insert
/// and after update - and by a query filter on the request's Space. Story 2.2; AD-2, AD-15, AR-8.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both references carry the Space</b> (AR-8): <c>(SpaceId, ProjectId)</c> to the Project and
/// <c>(SpaceId, StatusId)</c> to the Status definition, so a Task can never point at either in another
/// Space.
/// </para>
/// <para>
/// <b>No foreign key to <c>Space</c>, and the Status reference does not cascade.</b> SQL Server refuses
/// two cascade paths into one table (error 1785): Space to Project to Task, and Space to StatusDefinition
/// to Task, would be two. The Space is already constrained through the Project reference, which carries
/// it, and a Space's deletion still reaches its Tasks along that one path.
/// </para>
/// </remarks>
/// <param name="context">The context whose request-scoped Space the query filter closes over.</param>
internal sealed class TaskConfiguration(YelloDbContext context) : IEntityTypeConfiguration<TaskItem>
{
    /// <summary>
    /// The binary collation AD-15 requires of a position key: ordinal, so the database orders keys exactly
    /// as <see cref="StringComparer.Ordinal"/> does. Set on the column when it is created, because it
    /// cannot be retrofitted onto a populated, indexed column (AR-21).
    /// </summary>
    private const string PositionKeyCollation = "Latin1_General_100_BIN2";

    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable(SchemaNames.TaskTable);

        builder.HasKey(task => task.Id);

        // Never store-generated: the id comes from the sequential identifier port (AR-34).
        builder.Property(task => task.Id).ValueGeneratedNever();

        builder.Property(task => task.SpaceId)
            .HasColumnName(SchemaNames.SpaceIdColumn)
            .IsRequired();

        // AD-2's second layer, from application state. See MembershipConfiguration.
        builder.HasQueryFilter(task => task.SpaceId == context.ScopedSpaceId);

        builder.Property(task => task.Title)
            .IsRequired()
            .HasMaxLength(TaskTitle.MaxLength);

        builder.Property(task => task.PositionKey)
            .IsRequired()
            .IsUnicode(false)
            .HasMaxLength(PositionKey.MaxLength)
            .UseCollation(PositionKeyCollation);

        builder.Property(task => task.CreatedAt).IsRequired();

        // Optional (prd.md:387), and nullable is also what keeps story 2.3's migration expand-only: the
        // revision still serving inserts Tasks without naming it. No index - sorting by due date is 2.10's.
        builder.Property(task => task.DueDate).IsRequired(false);

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(task => new { task.SpaceId, task.ProjectId })
            .HasPrincipalKey(project => new { project.SpaceId, project.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<StatusDefinition>()
            .WithMany()
            .HasForeignKey(task => new { task.SpaceId, task.StatusId })
            .HasPrincipalKey(status => new { status.SpaceId, status.Id })
            .OnDelete(DeleteBehavior.NoAction);

        // The column read, the per-Status count and the bound's count all seek this index. SpaceId leads,
        // so a seek - and the key-range lock the bound's count takes - stays inside one Space's range.
        // Unique, because a position key is unique within its (Project, Status) (AD-15).
        builder.HasIndex(task => new { task.SpaceId, task.ProjectId, task.StatusId, task.PositionKey })
            .IsUnique();
    }
}
