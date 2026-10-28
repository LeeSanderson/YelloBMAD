using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.Labels;
using Yello.Domain.Tasks;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The TaskLabel table: which Labels each Task carries. Space-scoped: covered by the row-level security policy -
/// FILTER, and BLOCK after insert and after update - and by a query filter on the request's Space. Story 2.4;
/// FR-22, AD-2, AR-8.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both references carry the Space</b> (AR-8): <c>(SpaceId, TaskId)</c> to the Task and <c>(SpaceId, LabelId)</c>
/// to the Label.
/// </para>
/// <para>
/// <b>The Task reference cascades; the Label reference does not.</b> Deleting a Task removes its applications (story
/// 2.5). A Label in use must not be deletable at all (AC3): the store refuses first, under a lock, and the
/// <c>NoAction</c> reference is the database's half of the rule. It is required anyway: SQL Server refuses two
/// cascade paths into one table (error 1785) - Space to Project to Task to TaskLabel, and Space to Label to TaskLabel.
/// </para>
/// <para>
/// <b>Indexed <c>(SpaceId, LabelId, TaskId)</c></b> for the deletion's in-use probe, which takes its range lock
/// there, inside one Space. Story 2.10's filter by Label does not use it: the List View tests each candidate Task for
/// any of the chosen Labels with an <c>EXISTS</c> on the primary key <c>(SpaceId, TaskId, LabelId)</c>, because the
/// sort's own index drives the read (AR-22).
/// </para>
/// </remarks>
/// <param name="context">The context whose request-scoped Space the query filter closes over.</param>
internal sealed class TaskLabelConfiguration(YelloDbContext context) : IEntityTypeConfiguration<TaskLabel>
{
    public void Configure(EntityTypeBuilder<TaskLabel> builder)
    {
        builder.ToTable(SchemaNames.TaskLabelTable);

        builder.HasKey(application => new { application.SpaceId, application.TaskId, application.LabelId });

        builder.Property(application => application.SpaceId)
            .HasColumnName(SchemaNames.SpaceIdColumn)
            .IsRequired();

        // AD-2's second layer, from application state. See MembershipConfiguration.
        builder.HasQueryFilter(application => application.SpaceId == context.ScopedSpaceId);

        builder.Property(application => application.AppliedAt).IsRequired();

        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(application => new { application.SpaceId, application.TaskId })
            .HasPrincipalKey(task => new { task.SpaceId, task.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Label>()
            .WithMany()
            .HasForeignKey(application => new { application.SpaceId, application.LabelId })
            .HasPrincipalKey(label => new { label.SpaceId, label.Id })
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(application => new { application.SpaceId, application.LabelId, application.TaskId });
    }
}
