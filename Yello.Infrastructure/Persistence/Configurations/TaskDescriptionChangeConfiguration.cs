using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.Accounts;
using Yello.Domain.Tasks;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The TaskDescriptionChange table: each Task's description log. Space-scoped: covered by the row-level security policy -
/// FILTER, and BLOCK after insert and after update - and by a query filter on the request's Space.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cascade from the Task through <c>(SpaceId, TaskId)</c>, and no key to <c>Space</c></b>, for the reason
/// <see cref="TaskConfiguration"/> gives (1785). Deleting a Task, its Project or its Space removes the log with no store
/// change.
/// </para>
/// <para>
/// <b>The author is an Account that sets null</b>, as an Invitation's issuer does: the row is the Space's record and
/// outlives the Account, and the captured name stays. Not a Membership: a removal hard-deletes the Membership row, which
/// would either be blocked by the key or take the record with it.
/// </para>
/// <para>
/// <b>Unique per <c>(SpaceId, TaskId, Sequence)</c></b>: every appender takes the next sequence under the Task's row lock,
/// and this backstops one that does not. The index also serves the Task key and the rebuild's range read.
/// </para>
/// </remarks>
/// <param name="context">The context whose request-scoped Space the query filter closes over.</param>
internal sealed class TaskDescriptionChangeConfiguration(YelloDbContext context) : IEntityTypeConfiguration<TaskDescriptionChange>
{
    private const int KindLength = 16;

    public void Configure(EntityTypeBuilder<TaskDescriptionChange> builder)
    {
        builder.ToTable(SchemaNames.TaskDescriptionChangeTable);

        builder.HasKey(change => change.Id);

        builder.Property(change => change.Id).ValueGeneratedNever();

        builder.Property(change => change.SpaceId)
            .HasColumnName(SchemaNames.SpaceIdColumn)
            .IsRequired();

        // The second isolation layer, from application state. See MembershipConfiguration.
        builder.HasQueryFilter(change => change.SpaceId == context.ScopedSpaceId);

        builder.Property(change => change.Sequence).IsRequired();

        builder.Property(change => change.Kind)
            .IsRequired()
            .HasMaxLength(KindLength)
            .HasConversion<string>();

        builder.Property(change => change.Payload).IsRequired();

        builder.Property(change => change.AuthorAccountId).IsRequired(false);

        builder.Property(change => change.AuthorName)
            .IsRequired(false)
            .HasMaxLength(TaskDescriptionChange.AuthorNameMaxLength);

        builder.Property(change => change.RecordedAt).IsRequired();

        builder.HasIndex(change => new { change.SpaceId, change.TaskId, change.Sequence })
            .IsUnique();

        builder.HasOne<TaskItem>()
            .WithMany()
            .HasForeignKey(change => new { change.SpaceId, change.TaskId })
            .HasPrincipalKey(task => new { task.SpaceId, task.Id })
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(change => change.AuthorAccountId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
