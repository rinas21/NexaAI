using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace NexaAI.Core.Application.DTOs;

public record AgentExecutionRequest(
    [Required] string AgentName,
    [Required] string Input,
    bool RequireApproval = false,
    JsonDocument? Parameters = null
);

public record AgentExecutionResponse(
    Guid Id,
    string AgentName,
    string Status,
    JsonDocument Input,
    JsonDocument? Output,
    string? ErrorMessage,
    int? ExecutionTimeMs,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt
);

public record ToolApprovalResponse(
    Guid Id,
    Guid ExecutionId,
    string ToolName,
    JsonDocument Parameters,
    string Status,
    string? Reason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DecidedAt
);

public record ApprovalDecisionRequest(
    string? Reason = null
);

public record ToolDefinitionDto(
    string Name,
    string Description,
    JsonDocument Parameters,
    bool RequiresApproval
);

public record ToolCallDto(
    string ToolName,
    string Arguments,
    string? Result,
    bool Success,
    int ExecutionTimeMs
);