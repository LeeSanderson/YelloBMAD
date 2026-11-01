using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.Accounts;
using Yello.Infrastructure.Outbox;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The OutboxMessage table: policy-free, no Space column, no foreign key - Lee's Q6 and its recorded AD-2 variance.
/// Story 4.1; AD-10 / AR-33.
/// </summary>
/// <remarks>
/// <b>No query filter</b>, because there is no Space to filter on: the recovery sweep reads every row with no session
/// key. What the row holds is exactly what the email carries, for the seconds or minutes before dispatch - see
/// <see cref="OutboxMessage"/>.
/// </remarks>
internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable(SchemaNames.OutboxMessageTable);

        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();

        builder.Property(message => message.Kind)
            .IsRequired()
            .IsUnicode(false)
            .HasMaxLength(OutboxMessage.KindMaxLength);

        builder.Property(message => message.Recipient)
            .IsRequired()
            .HasMaxLength(EmailAddressRule.MaxLength);

        builder.Property(message => message.Subject)
            .IsRequired()
            .HasMaxLength(OutboxMessage.SubjectMaxLength);

        builder.Property(message => message.Body).IsRequired();

        builder.Property(message => message.EnqueuedAt).IsRequired();

        builder.Property(message => message.ClaimedAt);

        builder.Property(message => message.Attempts).IsRequired();
    }
}
