using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.Spaces;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The IdempotencyRecord table. Space-scoped: covered by the row-level security policy and by a query
/// filter on the request's Space. Story 2.1; AD-2, AD-18.
/// </summary>
/// <param name="context">The context whose request-scoped Space the query filter closes over.</param>
internal sealed class IdempotencyRecordConfiguration(YelloDbContext context) : IEntityTypeConfiguration<IdempotencyRecord>
{
    /// <summary>
    /// The Host refuses a longer key before anything is read (<c>Idempotency.MaxKeyLength</c>).
    /// </summary>
    private const int KeyMaxLength = 255;

    private const string KeyCollation = "Latin1_General_100_BIN2";

    private const int FingerprintLength = 64;
    private const int ContentTypeMaxLength = 128;
    private const int LocationMaxLength = 512;

    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable(SchemaNames.IdempotencyRecordTable);

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).ValueGeneratedNever();

        builder.Property(record => record.SpaceId)
            .HasColumnName(SchemaNames.SpaceIdColumn)
            .IsRequired();

        // AD-2's second layer, from application state. See MembershipConfiguration.
        builder.HasQueryFilter(record => record.SpaceId == context.ScopedSpaceId);

        builder.Property(record => record.AccountId).IsRequired();
        // Compared byte for byte: a key is an opaque token, and the database's default collation would make
        // 'abc' and 'ABC' the same key.
        builder.Property(record => record.Key).IsRequired().HasMaxLength(KeyMaxLength).UseCollation(KeyCollation);
        builder.Property(record => record.Fingerprint).IsRequired().HasMaxLength(FingerprintLength).IsFixedLength();
        builder.Property(record => record.StatusCode).IsRequired();
        builder.Property(record => record.ContentType).HasMaxLength(ContentTypeMaxLength);
        builder.Property(record => record.Location).HasMaxLength(LocationMaxLength);
        builder.Property(record => record.Body).IsRequired();
        builder.Property(record => record.CreatedAt).IsRequired();

        // One record per key, per Account, per Space: the second insert of a key is how a concurrent
        // first use is detected.
        builder.HasIndex(record => new { record.SpaceId, record.AccountId, record.Key }).IsUnique();

        // A Space's expired records are deleted by age inside its own unit of work.
        builder.HasIndex(record => new { record.SpaceId, record.CreatedAt });

        builder.HasOne<Space>()
            .WithMany()
            .HasForeignKey(record => record.SpaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
