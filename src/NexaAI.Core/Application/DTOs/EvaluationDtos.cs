using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace NexaAI.Core.Application.DTOs;

public record StartEvaluationRequest(
    string Name = "default",
    string? DatasetVersion = null,
    JsonDocument? Config = null
);

public record EvaluationRunResponse(
    Guid Id,
    string Name,
    string? DatasetVersion,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<EvaluationResultResponse> Results
);

public record EvaluationResultResponse(
    Guid Id,
    string QuestionId,
    string Question,
    string? ExpectedAnswer,
    string? ActualAnswer,
    JsonDocument Metrics,
    DateTimeOffset CreatedAt
);

public record EvaluationSummaryResponse(
    int TotalRuns,
    double AverageRecall,
    double AverageMrr,
    double AverageFaithfulness,
    double AverageAnswerRelevance,
    double AverageLatencyMs,
    double P95LatencyMs,
    long TotalTokensUsed,
    int TotalFailures
);