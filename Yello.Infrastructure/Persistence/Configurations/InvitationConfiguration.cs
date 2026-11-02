using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.Accounts;
using Yello.Domain.Invitations;
using Yello.Domain.Spaces;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The Invitation table. Space-scoped: covered by the row-level security policy - FILTER, and BLOCK after insert and
/// after update - and by a query filter on the request's Space. Story 4.1; FR-10, AD-2, AD-27.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cascades from its Space</b> (Lee's Q1, FR-7): deleting a Space destroys its Invitations in the one deletion
/// statement. <b>References its issuer's Account with no action</b> (FR-12, AD-5): an Invitation survives its issuer's
/// demotion and removal, and Account deletion tombstones (story 5.4). <b>There is no reference to <c>Membership</c></b>:
/// a composite <c>(SpaceId, MembershipId)</c> with <c>SET NULL</c> would null the Space too, and with no action it would
/// block 4.5's removals. <c>Account</c> is not Space-scoped, so <c>IssuedByAccountId</c> is a deliberate
/// non-Membership reference outside the rule that Space-scoped references carry the Space.
/// </para>
/// <para>
/// <b>The address is stored twice</b>: as submitted (trimmed), for 4.2's list; and normalised, in the Account column's
/// exact type and <c>Latin1_General_100_BIN2</c> collation (Lee's Q3), so story 4.3's accept compares the caller's
/// address with one function and one collation. <c>(SpaceId, NormalizedEmailAddress)</c> is the index both duplicate checks seek.
/// </para>
/// <para>
/// <b>No unique index filtered on <c>State</c></b>: expiry is derived, so an expired Invitation is still
/// <c>Pending</c>, and such an index would block the reissue FR-39 requires.
/// </para>
/// <para>
/// <b>The expiry is the database's</b> (open question 10 (a)): a column default seven days after
/// <c>SYSDATETIMEOFFSET()</c> - PRD section 12 item 9, unconfirmed - read back on insert, so no C# clock stamps it.
/// </para>
/// </remarks>
/// <param name="context">The context whose request-scoped Space the query filter closes over.</param>
internal sealed class InvitationConfiguration(YelloDbContext context) : IEntityTypeConfiguration<Invitation>
{
    /// <summary>
    /// The days between issue and expiry. PRD section 12 item 9 (an assumption); the migration carries the same 7.
    /// </summary>
    internal const int ExpiryDays = 7;

    /// <summary>
    /// A SHA-256 hash.
    /// </summary>
    private const int TokenHashLength = 32;

    /// <summary>
    /// The longest stored Role or State name.
    /// </summary>
    private const int NameLength = 16;

    /// <summary>
    /// The normalised address's collation: the Account column's.
    /// </summary>
    private const string NormalizedEmailCollation = "Latin1_General_100_BIN2";

    public void Configure(EntityTypeBuilder<Invitation> builder)
    {
        builder.ToTable(SchemaNames.InvitationTable);

        builder.HasKey(invitation => invitation.Id);

        // Never store-generated: the id comes from the sequential identifier port (AR-34).
        builder.Property(invitation => invitation.Id).ValueGeneratedNever();

        builder.Property(invitation => invitation.SpaceId)
            .HasColumnName(SchemaNames.SpaceIdColumn)
            .IsRequired();

        // AD-2's second layer, from application state. See MembershipConfiguration.
        builder.HasQueryFilter(invitation => invitation.SpaceId == context.ScopedSpaceId);

        builder.Property(invitation => invitation.EmailAddress)
            .IsRequired()
            .HasMaxLength(EmailAddressRule.MaxLength);

        builder.Property(invitation => invitation.NormalizedEmailAddress)
            .IsRequired()
            .HasMaxLength(EmailAddressRule.MaxLength)
            .UseCollation(NormalizedEmailCollation);

        builder.Property(invitation => invitation.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(NameLength);

        builder.Property(invitation => invitation.State)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(NameLength);

        builder.Property(invitation => invitation.ExpiresAt)
            .IsRequired()
            .HasDefaultValueSql($"DATEADD(day, {ExpiryDays}, {DatabaseClock.FunctionName}())")
            .ValueGeneratedOnAdd();

        builder.Property(invitation => invitation.TokenHash)
            .IsRequired()
            .HasMaxLength(TokenHashLength)
            .IsFixedLength(false);

        builder.Property(invitation => invitation.IssuedByAccountId).IsRequired();

        builder.Property(invitation => invitation.IssuerName)
            .IsRequired()
            .HasMaxLength(Invitation.IssuerNameMaxLength);

        builder.Property(invitation => invitation.CreatedAt).IsRequired();

        builder.HasAlternateKey(invitation => new { invitation.SpaceId, invitation.Id });

        // AC4's and AC13's seek: this Space's Invitations for one address.
        builder.HasIndex(invitation => new { invitation.SpaceId, invitation.NormalizedEmailAddress });

        // One Invitation per token. Story 4.3 resolves a token through the policy-free InvitationToken index instead,
        // because this table is under the Space policy.
        builder.HasIndex(invitation => invitation.TokenHash)
            .IsUnique()
            .HasDatabaseName(SchemaNames.InvitationTokenHashUniqueIndex);

        builder.HasOne<Space>()
            .WithMany()
            .HasForeignKey(invitation => invitation.SpaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(invitation => invitation.IssuedByAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
