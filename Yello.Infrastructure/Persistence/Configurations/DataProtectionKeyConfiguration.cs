using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Yello.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="DataProtectionKey"/>. Story 1.10 AC12.
/// </summary>
internal sealed class DataProtectionKeyConfiguration : IEntityTypeConfiguration<DataProtectionKey>
{
    private const int FriendlyNameMaxLength = 100;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<DataProtectionKey> builder)
    {
        builder.ToTable(SchemaNames.DataProtectionKeyTable);

        builder.HasKey(key => key.Id);
        builder.Property(key => key.Id).ValueGeneratedNever();

        builder.Property(key => key.FriendlyName).HasMaxLength(FriendlyNameMaxLength);
        builder.Property(key => key.Xml).IsRequired();
    }
}
