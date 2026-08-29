using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.Accounts;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The Account table. FR-1's uniqueness lives here, as an index.
/// </summary>
internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    /// <summary>
    /// Long enough for any address that can be delivered to. RFC 5321 caps a path at 256
    /// octets including the angle brackets, which leaves 254 for the address itself.
    /// </summary>
    private const int EmailAddressMaxLength = 254;

    /// <summary>
    /// A generous cap on a display name. Long enough for any real name in any script,
    /// short enough that the unique index above it and every rendering below it stay bounded.
    /// </summary>
    private const int DisplayNameMaxLength = 128;

    /// <summary>
    /// IdentityV3 encodes format marker, iteration count, salt and subkey into one base64
    /// string. At the work factor this story configures that is 84 characters; the cap is well
    /// above it so raising the work factor is a configuration change and not a migration.
    /// </summary>
    private const int PasswordHashMaxLength = 256;

    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable(SchemaNames.AccountTable);

        builder.HasKey(account => account.Id);

        // The application generates every id, before the insert - see IIdentifierGenerator for
        // why that is what resolves AD-2's registration seam. Stated rather than left to
        // convention: EF Core would otherwise treat a Guid key as store-generated and write a
        // default constraint, which would put generation after the insert and take the SpaceId
        // out of the slice's hands at exactly the moment it needs it.
        builder.Property(account => account.Id).ValueGeneratedNever();

        builder.Property(account => account.EmailAddress)
            .IsRequired()
            .HasMaxLength(EmailAddressMaxLength);

        builder.Property(account => account.NormalizedEmailAddress)
            .IsRequired()
            .HasMaxLength(EmailAddressMaxLength);

        builder.Property(account => account.DisplayName)
            .IsRequired()
            .HasMaxLength(DisplayNameMaxLength);

        // Nullable, and that is AC7 rather than laxity: an OAuth Account has no password, and
        // harness-constraints.md:64 names "an Account is created with an email address AND a
        // password" as one of the four assumptions the deferred OAuth work will break.
        builder.Property(account => account.PasswordHash)
            .HasMaxLength(PasswordHashMaxLength);

        // FR-1's uniqueness, on the normalised column rather than on the address as typed.
        // EmailAddressNormalisation explains why the rule is explicit rather than collational -
        // in short, AD-15's Latin1_General_100_BIN2 offers no case-insensitive comparison at
        // all, so a design resting on a collation would have collided with a settled decision.
        //
        // This index is never a soft-delete tombstone. FR-3 requires a deleted Account's address
        // to be reusable by a new Account that inherits nothing, so there is no filter here and
        // no DeletedAt column for a later story to add one against.
        builder.HasIndex(account => account.NormalizedEmailAddress)
            .IsUnique()
            .HasDatabaseName(SchemaNames.AccountEmailUniqueIndex);
    }
}
