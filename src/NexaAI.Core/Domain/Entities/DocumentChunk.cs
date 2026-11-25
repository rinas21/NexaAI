using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using pgvector;

namespace NexaAI.Core.Domain.Entities;

public class DocumentChunk
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    [Required]
    public Guid DocumentId { get; private set; }

    public int ChunkIndex { get; private set; }

    [Required]
    public string Content { get; private set; } = string.Empty;

    public int TokenCount { get; private set; }

    [Column(TypeName = "vector(768)")]
    public Vector? Embedding { get; private set; }

    [Column(TypeName = "jsonb")]
    public JsonDocument Metadata { get; private set; } = JsonDocument.Parse("{}");

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    // Navigation property
    public Document? Document { get; private set; }

    // Private constructor for EF Core
    private DocumentChunk() { }

    public static DocumentChunk Create(
        Guid documentId,
        int chunkIndex,
        string content,
        int tokenCount,
        Vector? embedding = null,
        JsonDocument? metadata = null)
    {
        if (documentId == Guid.Empty)
            throw new ArgumentException("Document ID is required", nameof(documentId));

        if (chunkIndex < 0)
            throw new ArgumentException("Chunk index must be non-negative", nameof(chunkIndex));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content is required", nameof(content));

        if (tokenCount < 0)
            throw new ArgumentException("Token count must be non-negative", nameof(tokenCount));

        return new DocumentChunk
        {
            DocumentId = documentId,
            ChunkIndex = chunkIndex,
            Content = content,
            TokenCount = tokenCount,
            Embedding = embedding,
            Metadata = metadata ?? JsonDocument.Parse("{}")
        };
    }

    public void SetEmbedding(Vector embedding)
    {
        Embedding = embedding;
    }

    public void UpdateMetadata(JsonDocument metadata)
    {
        Metadata = metadata;
    }
}