using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vanq.Domain.Entities;

namespace Vanq.Infrastructure.Persistence.Configurations;

public class SystemParameterConfiguration : IEntityTypeConfiguration<SystemParameter>
{
    public void Configure(EntityTypeBuilder<SystemParameter> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Key)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(x => x.Value)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Category)
            .HasMaxLength(64);

        builder.Property(x => x.IsSensitive)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.LastUpdatedBy)
            .HasMaxLength(64);

        builder.Property(x => x.LastUpdatedAt)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(256);

        builder.Property(x => x.Metadata)
            .HasColumnType("text");

        // Unique index on Key
        builder.HasIndex(x => x.Key)
            .IsUnique()
            .HasDatabaseName("IX_SystemParameters_Key");

        // Index for querying by category
        builder.HasIndex(x => x.Category)
            .HasDatabaseName("IX_SystemParameters_Category");

        // Index for querying by IsSensitive (useful for admin dashboards)
        builder.HasIndex(x => x.IsSensitive)
            .HasDatabaseName("IX_SystemParameters_IsSensitive");
    }
}
