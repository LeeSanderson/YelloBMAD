using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.ApiTokens;
using Yello.Domain.Memberships;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The ApiToken table. AD-6; story 1.8, open questions 4 and 6.
/// </summary>
/// <remarks>
/// <para>
/// <b>Outside the Space isolation policy, deliberately - the second such table after
/// <c>AccessRefusal</c>.</b> A Token is looked up by its hash in the authentication step, before any
/// Space is known, so a predicate on <c>SESSION_CONTEXT('SpaceId')</c> would hide every row from the
/// one read that needs it. Its Space column is <c>BoundSpaceId</c>, not <c>SpaceId</c>, for the same
/// reason <c>AccessRefusal</c>'s is <c>TargetSpaceId</c>. Only <c>ApiTokenStore</c> reads it.
/// </para>
/// <para>
/// <b>Invalidation is a foreign key.</b> The one foreign key is the composite
/// <c>(BoundSpaceId, AccountId) -&gt; Membership(SpaceId, AccountId)</c> with <c>ON DELETE
/// CASCADE</c>: removing the Membership deletes its Tokens in the same statement; deleting a Space
/// cascades through its Memberships; deleting an Account must delete its Memberships first, which
/// takes the Tokens with them. There is no second cascade from <c>Space</c> or <c>Account</c>, which
/// SQL Server refuses (error 1785).
/// </para>
/// </remarks>
internal sealed class ApiTokenConfiguration : IEntityTypeConfiguration<ApiToken>
{
    /// <summary>
    /// A SHA-256 hash.
    /// </summary>
    private const int SecretHashLength = 32;

    public void Configure(EntityTypeBuilder<ApiToken> builder)
    {
        builder.ToTable(SchemaNames.ApiTokenTable);

        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();

        builder.Property(token => token.AccountId).IsRequired();
        builder.Property(token => token.BoundSpaceId).IsRequired();
        builder.Property(token => token.Name).IsRequired().HasMaxLength(ApiTokenName.MaxLength);
        builder.Property(token => token.CreatedAt).IsRequired();

        builder.Property(token => token.SecretHash)
            .IsRequired()
            .HasMaxLength(SecretHashLength)
            .IsFixedLength(false);

        // The authentication lookup, once per Token request.
        builder.HasIndex(token => token.SecretHash)
            .IsUnique()
            .HasDatabaseName(SchemaNames.ApiTokenSecretHashUniqueIndex);

        // The Account-settings list.
        builder.HasIndex(token => token.AccountId);

        builder.HasOne<Membership>()
            .WithMany()
            .HasForeignKey(token => new { token.BoundSpaceId, token.AccountId })
            .HasPrincipalKey(membership => new { membership.SpaceId, membership.AccountId })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
