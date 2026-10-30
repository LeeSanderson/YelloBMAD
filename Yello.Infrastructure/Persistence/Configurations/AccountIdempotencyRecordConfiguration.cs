using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.Accounts;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The AccountIdempotencyRecord table. Account-scoped: covered by <c>AccountIsolationPolicy</c> and by a query
/// filter on the request's Account. Story 3.1, open question 2 (a); AD-18, AD-24 as amended.
/// </summary>
/// <param name="context">The context whose request-scoped Account the query filter closes over.</param>
internal sealed class AccountIdempotencyRecordConfiguration(YelloDbContext context) : IEntityTypeConfiguration<AccountIdempotencyRecord>
{
    /// <summary>
    /// The Host refuses a longer key before anything is read (<c>Idempotency.MaxKeyLength</c>).
    /// </summary>
    private const int KeyMaxLength = 255;

    private const string KeyCollation = "Latin1_General_100_BIN2";

    private const int FingerprintLength = 64;
    private const int ContentTypeMaxLength = 128;
    private const int LocationMaxLength = 512;

    public void Configure(EntityTypeBuilder<AccountIdempotencyRecord> builder)
    {
        builder.ToTable(SchemaNames.AccountIdempotencyRecordTable);

        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).ValueGeneratedNever();

        builder.Property(record => record.AccountId)
            .HasColumnName(SchemaNames.AccountIdColumn)
            .IsRequired();

        // The policy's second layer, from application state: the Account scope the creation unit of work narrows.
        builder.HasQueryFilter(record => record.AccountId == context.ScopedAccountId);

        builder.Property(record => record.Key).IsRequired().HasMaxLength(KeyMaxLength).UseCollation(KeyCollation);
        builder.Property(record => record.Fingerprint).IsRequired().HasMaxLength(FingerprintLength).IsFixedLength();
        builder.Property(record => record.StatusCode).IsRequired();
        builder.Property(record => record.ContentType).HasMaxLength(ContentTypeMaxLength);
        builder.Property(record => record.Location).HasMaxLength(LocationMaxLength);
        builder.Property(record => record.Body).IsRequired();
        builder.Property(record => record.CreatedAt).IsRequired();

        // One record per key, per Account: the second insert of a key is how a concurrent first use is detected.
        builder.HasIndex(record => new { record.AccountId, record.Key }).IsUnique();

        // An Account's expired records are deleted by age inside its own creation unit of work.
        builder.HasIndex(record => new { record.AccountId, record.CreatedAt });

        // Erased with the Account (FR-3, story 5.4).
        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(record => record.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
