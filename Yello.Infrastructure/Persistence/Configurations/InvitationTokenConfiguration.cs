using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yello.Domain.Invitations;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// The Invitation token index. <b>Outside the Space isolation policy, deliberately</b> - Lee's Q1 (a), story 4.3, a named
/// AD-2 variance beside <c>ApiToken.BoundSpaceId</c>: a token is looked up by its hash before any Space is known, so a
/// predicate on <c>SESSION_CONTEXT('SpaceId')</c> would hide every row from the one read that needs it.
/// </summary>
/// <remarks>
/// <para>
/// <b>No query filter</b>, for the same reason, and <c>SchemaVarianceTests.NamedVariances</c> names
/// <c>InvitationToken.OfferedSpaceId</c>. Its hash is read by this configuration and by <see cref="InvitationScope"/> alone
/// (<c>RefusalBoundaryGateTests.The_token_index_is_read_by_its_configuration_and_one_named_reader_only</c>).
/// </para>
/// <para>
/// <b>Keyed on the Invitation</b> (one token per Invitation), its hash unique, and its one foreign key the composite
/// <c>(OfferedSpaceId, InvitationId) -&gt; Invitation (SpaceId, Id)</c> with <c>ON DELETE CASCADE</c>: a row cannot name an
/// Invitation of another Space than the one it records, and a Space's deletion takes it through its Invitations.
/// </para>
/// </remarks>
internal sealed class InvitationTokenConfiguration : IEntityTypeConfiguration<InvitationToken>
{
    /// <summary>
    /// A SHA-256 hash.
    /// </summary>
    private const int TokenHashLength = 32;

    public void Configure(EntityTypeBuilder<InvitationToken> builder)
    {
        builder.ToTable(SchemaNames.InvitationTokenTable);

        builder.HasKey(token => token.InvitationId);
        builder.Property(token => token.InvitationId).ValueGeneratedNever();

        builder.Property(token => token.OfferedSpaceId).IsRequired();

        builder.Property(token => token.TokenHash)
            .IsRequired()
            .HasMaxLength(TokenHashLength)
            .IsFixedLength(false);

        // The view's and the accept's one lookup.
        builder.HasIndex(token => token.TokenHash)
            .IsUnique()
            .HasDatabaseName(SchemaNames.InvitationTokenIndexTokenHashUniqueIndex);

        builder.HasOne<Invitation>()
            .WithMany()
            .HasForeignKey(token => new { token.OfferedSpaceId, token.InvitationId })
            .HasPrincipalKey(invitation => new { invitation.SpaceId, invitation.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
