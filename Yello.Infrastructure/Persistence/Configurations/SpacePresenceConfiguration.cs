using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.Spaces;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The SpacePresence table: who is live in a Space, for NFR-8's concurrent-active-Sessions bound.
/// Space-scoped, and therefore covered by the row-level security policy and by a query filter on the
/// request's Space.
/// </summary>
/// <param name="context">The context whose request-scoped Space the query filter closes over.</param>
internal sealed class SpacePresenceConfiguration(YelloDbContext context) : IEntityTypeConfiguration<SpacePresence>
{
    public void Configure(EntityTypeBuilder<SpacePresence> builder)
    {
        builder.ToTable(SchemaNames.SpacePresenceTable);

        // One presence per Account per Space, keyed by the pair: there is no surrogate id because
        // nothing refers to a presence, and the pair is what the upsert finds.
        builder.HasKey(presence => new { presence.SpaceId, presence.AccountId });

        builder.Property(presence => presence.SpaceId)
            .HasColumnName(SchemaNames.SpaceIdColumn)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(presence => presence.AccountId)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(presence => presence.ExpiresAt).IsRequired();

        // AD-2's second layer, from application state. See MembershipConfiguration.
        builder.HasQueryFilter(presence => presence.SpaceId == context.ScopedSpaceId);

        // A presence in no Space cannot exist; deleting a Space takes its presences with it.
        builder.HasOne<Space>()
            .WithMany()
            .HasForeignKey(presence => presence.SpaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
