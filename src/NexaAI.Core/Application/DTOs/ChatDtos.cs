using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace NexaAI.Core.Application.DTOs;

public record ChatRequest(
    [Required] string Message,
    Guid? ConversationId = null,
    bool UseAgent = false,
    JsonDocument? Metadata = null
);

public record ChatResponse(
    Guid ConversationId,
    Guid MessageId,
    string Content,
    IReadOnlyList<CitationDto> Citations,
    bool IsGrounded,
    string? RefusalReason,
    int? TotalLatencyMs,
    TokenUsageDto? TokenUsage
);

public record CitationDto(
    Guid DocumentId,
    Guid ChunkId,
    string DocumentTitle,
    string Snippet,
    double SimilarityScore
);

public record TokenUsageDto(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens
);

public record ConversationResponse(
    Guid Id,
    string? Title,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<MessageResponse> Messages
);

public record MessageResponse(
    Guid Id,
    string Role,
    string Content,
    JsonDocument Citations,
    JsonDocument ToolCalls,
    DateTimeOffset CreatedAt
);

public record CreateConversationRequest(string? Title);

public record StreamChatRequest(
    [Required] string Message,
    Guid? ConversationId = null
);