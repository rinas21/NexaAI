using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace NexaAI.Core.Application.DTOs;

public record DocumentUploadRequest(
    [Required] string Title,
    [Required] string AccessLevel = "Private"
);

public record DocumentResponse(
    Guid Id,
    string Title,
    string OriginalFilename,
    string ContentType,
    long FileSize,
    Guid? OwnerId,
    string OwnerName,
    string AccessLevel,
    string Status,
    JsonDocument Metadata,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record DocumentListResponse(
    Guid Id,
    string Title,
    string OriginalFilename,
    string ContentType,
    long FileSize,
    string AccessLevel,
    string Status,
    int ChunkCount,
    DateTimeOffset CreatedAt
);

public record DocumentChunkResponse(
    Guid Id,
    int ChunkIndex,
    string Content,
    int TokenCount,
    JsonDocument Metadata,
    double? SimilarityScore
);

public record DocumentSearchRequest(
    [Required] string Query,
    int TopK = 10,
    double SimilarityThreshold = 0.7,
    string? AccessLevel = null,
    Guid? OwnerId = null,
    JsonDocument? MetadataFilter = null
);

public record DocumentSearchResponse(
    IReadOnlyList<DocumentChunkResponse> Results,
    int TotalCount,
    TimeSpan SearchTime
);

public record ReprocessDocumentRequest(
    bool ForceReprocess = false
);