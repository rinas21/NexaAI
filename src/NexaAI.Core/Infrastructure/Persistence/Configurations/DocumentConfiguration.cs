using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaAI.Core.Domain.Entities;

namespace NexaAI.Core.Infrastructure.Persistence.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(d => d.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.OriginalFilename)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(d => d.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.FileSize)
            .IsRequired();

        builder.Property(d => d.StoragePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(d => d.OwnerId);

        builder.Property(d => d.AccessLevel)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue(DocumentAccessLevel.Private);

        builder.Property(d => d.Status)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue(DocumentStatus.Processing);

        builder.Property(d => d.Metadata)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property(d => d.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP AT TIME ZONE 'UTC'");

        builder.Property(d => d.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP AT TIME ZONE 'UTC'");

        // Indexes
        builder.HasIndex(d => d.OwnerId)
            .HasDatabaseName("ix_documents_owner_id");

        builder.HasIndex(d => d.AccessLevel)
            .HasDatabaseName("ix_documents_access_level");

        builder.HasIndex(d => d.Status)
            .HasDatabaseName("ix_documents_status");

        builder.HasIndex(d => d.CreatedAt)
            .HasDatabaseName("ix_documents_created_at");

        // Navigation properties
        builder.HasMany(d => d.Chunks)
            .WithOne(c => c.Document!)
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}