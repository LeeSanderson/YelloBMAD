using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.Spaces;
using Yello.Infrastructure.Outbox;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The NotificationSend table: Space-scoped, under the row-level security policy - FILTER, and BLOCK after insert and
/// after update - and a query filter on the request's Space. See <see cref="NotificationSend"/> for what it holds and why.
/// </summary>
/// <remarks>
/// <b>Cascades with its Space</b>, like every Space-scoped table: a Space's erasure takes its records, so no row outlives
/// the Space it names.
/// </remarks>
/// <param name="context">The context whose request-scoped Space the query filter closes over.</param>
internal sealed class NotificationSendConfiguration(YelloDbContext context) : IEntityTypeConfiguration<NotificationSend>
{
    public void Configure(EntityTypeBuilder<NotificationSend> builder)
    {
        builder.ToTable(SchemaNames.NotificationSendTable);

        builder.HasKey(send => send.Id);
        builder.Property(send => send.Id).ValueGeneratedNever();

        builder.Property(send => send.SpaceId)
            .HasColumnName(SchemaNames.SpaceIdColumn)
            .IsRequired();

        // AD-2's second layer, from application state. See MembershipConfiguration.
        builder.HasQueryFilter(send => send.SpaceId == context.ScopedSpaceId);

        builder.Property(send => send.Kind)
            .IsRequired()
            .HasMaxLength(OutboxMessage.KindMaxLength);

        // The database's clock in UTC, so the record's time does not depend on the Host's.
        builder.Property(send => send.RecordedAt)
            .IsRequired()
            .HasDefaultValueSql($"SWITCHOFFSET({DatabaseClock.FunctionName}(), '+00:00')")
            .ValueGeneratedOnAdd();

        builder.HasOne<Space>()
            .WithMany()
            .HasForeignKey(send => send.SpaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
