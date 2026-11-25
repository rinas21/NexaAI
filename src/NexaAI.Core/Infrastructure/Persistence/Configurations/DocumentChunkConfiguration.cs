using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NexaAI.Core.Domain.Entities;
using pgvector;

namespace NexaAI.Core.Infrastructure.Persistence.Configurations;

public class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("document_chunks");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(c => c.DocumentId)
            .IsRequired();

        builder.Property(c => c.ChunkIndex)
            .IsRequired();

        builder.Property(c => c.Content)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(c => c.TokenCount)
            .IsRequired();

        builder.Property(c => c.Embedding)
            .HasColumnType("vector(768)");

        builder.Property(c => c.Metadata)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property(c => c.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("CURRENT_TIMESTAMP AT TIME ZONE 'UTC'");

        // Indexes
        builder.HasIndex(c => c.DocumentId)
            .HasDatabaseName("ix_document_chunks_document_id");

        builder.HasIndex(c => new { c.DocumentId, c.ChunkIndex })
            .IsUnique()
            .HasDatabaseName("ix_document_chunks_document_chunk_index");

        // Vector index (HNSW) - created via migration
        // builder.HasIndex(c => c.Embedding)
        //     .HasMethod("hnsw")
        //     .HasDatabaseName("ix_document_chunks_embedding");

        // Navigation properties
        builder.HasOne(c => c.Document)
            .WithMany(d => d.Chunks)
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}