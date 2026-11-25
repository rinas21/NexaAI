using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexaAI.Core.Application.Common;
using NexaAI.Core.Application.DTOs;
using NexaAI.Core.Application.Interfaces;
using NexaAI.Core.Domain.Entities;
using NexaAI.Core.Infrastructure.Persistence;

namespace NexaAI.Core.Application.Services;

/// <summary>Persists AI evaluation runs, their per-question results and aggregated metrics.</summary>
public sealed class EvaluationService(
    AppDbContext db,
    IAIServiceClient aiClient,
    ILogger<EvaluationService> logger) : IEvaluationService
{
    /// <inheritdoc />
    public async Task<EvaluationRunResponse> StartAsync(StartEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        var config = request.Config?.RootElement.Clone() ?? JsonHelpers.EmptyObject().RootElement;
        var run = EvaluationRun.Create(request.Name, JsonHelpers.ToDocument(config), request.DatasetVersion);
        db.EvaluationRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var payload = JsonHelpers.ToDocument(new
            {
                run_id = run.Id.ToString(),
                name = run.Name,
                dataset_version = run.DatasetVersion,
                config
            });

            var response = await aiClient.RunEvaluationAsync(payload, cancellationToken);
            var root = response.RootElement;
            var items = root.ValueKind == JsonValueKind.Array
                ? root
                : JsonHelpers.GetArray(root, "results", "items", "evaluations");

            var index = 0;
            foreach (var item in items.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var questionId = JsonHelpers.GetString(item, "question_id", "id")
                                 ?? $"q-{++index}";
                var question = JsonHelpers.GetString(item, "question", "prompt", "input")
                               ?? string.Empty;
                if (string.IsNullOrWhiteSpace(question))
                {
                    continue;
                }

                var retrieved = JsonHelpers.TryGetProperty(item, "retrieved_chunks", out var chunksElement)
                                && chunksElement.ValueKind == JsonValueKind.Array
                    ? JsonDocument.Parse(chunksElement.GetRawText())
                    : JsonHelpers.EmptyArray();

                var metricsElement = JsonHelpers.TryGetProperty(item, "metrics", out var metrics) &&
                                     metrics.ValueKind == JsonValueKind.Object
                    ? metrics
                    : item;

                db.EvaluationResults.Add(EvaluationResult.Create(
                    run.Id,
                    questionId,
                    question,
                    JsonHelpers.GetString(item, "expected_answer", "expected", "expectedAnswer"),
                    JsonHelpers.GetString(item, "actual_answer", "answer", "actual", "response", "output"),
                    retrieved,
                    JsonDocument.Parse(metricsElement.GetRawText())));
            }

            run.Complete();
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Evaluation run {RunId} completed with {ResultCount} result(s)", run.Id, items.GetArrayLength());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Evaluation run {RunId} failed", run.Id);
            run.Fail();
            await db.SaveChangesAsync(CancellationToken.None);
            throw new AIServiceException($"Evaluation run failed: {ex.Message}", ex);
        }

        return await GetRunAsync(run.Id, cancellationToken)
               ?? throw new NotFoundException("Evaluation run could not be loaded after completion.");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EvaluationRunResponse>> GetRunsAsync(CancellationToken cancellationToken = default)
    {
        var runs = await db.EvaluationRuns
            .OrderByDescending(r => r.StartedAt)
            .Take(100)
            .ToListAsync(cancellationToken);
        return await ToResponsesAsync(runs, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EvaluationRunResponse?> GetRunAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var run = await db.EvaluationRuns.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (run is null)
        {
            return null;
        }

        var responses = await ToResponsesAsync([run], cancellationToken);
        return responses.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<EvaluationSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var runs = await db.EvaluationRuns
            .Include(r => r.Results)
            .ToListAsync(cancellationToken);

        var results = runs.SelectMany(r => r.Results).ToList();
        var latencies = results
            .Select(r => JsonHelpers.GetDouble(r.Metrics.RootElement, "latency_ms", "latencyMs") ?? 0)
            .OrderBy(v => v)
            .ToList();

        return new EvaluationSummaryResponse(
            TotalRuns: runs.Count,
            AverageRecall: Average(results, "recall"),
            AverageMrr: Average(results, "mrr"),
            AverageFaithfulness: Average(results, "faithfulness"),
            AverageAnswerRelevance: Average(results, "answer_relevance", "relevance", "answerRelevance"),
            AverageLatencyMs: latencies.Count == 0 ? 0 : latencies.Average(),
            P95LatencyMs: Percentile(latencies, 0.95),
            TotalTokensUsed: results.Sum(r => (long)(JsonHelpers.GetInt(r.Metrics.RootElement, "tokens_used", "total_tokens", "tokens") ?? 0)),
            TotalFailures: runs.Count(r => r.Status == EvaluationStatus.Failed)
                           + results.Count(r =>
                               JsonHelpers.GetBool(r.Metrics.RootElement, "success") == false ||
                               JsonHelpers.GetString(r.Metrics.RootElement, "error") is not null));
    }

    private async Task<IReadOnlyList<EvaluationRunResponse>> ToResponsesAsync(
        IReadOnlyList<EvaluationRun> runs,
        CancellationToken cancellationToken)
    {
        if (runs.Count == 0)
        {
            return [];
        }

        var ids = runs.Select(r => r.Id).ToList();
        var results = await db.EvaluationResults
            .Where(r => ids.Contains(r.RunId))
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return runs.Select(run => new EvaluationRunResponse(
            run.Id,
            run.Name,
            run.DatasetVersion,
            run.Status,
            run.StartedAt,
            run.CompletedAt,
            results
                .Where(r => r.RunId == run.Id)
                .Select(r => new EvaluationResultResponse(
                    r.Id, r.QuestionId, r.Question, r.ExpectedAnswer, r.ActualAnswer, r.Metrics, r.CreatedAt))
                .ToList())).ToList();
    }

    private static double Average(IReadOnlyList<EvaluationResult> results, params string[] names)
    {
        var values = results
            .Select(r => JsonHelpers.GetDouble(r.Metrics.RootElement, names))
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();
        return values.Count == 0 ? 0 : values.Average();
    }

    private static double Percentile(IReadOnlyList<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
        {
            return 0;
        }

        var index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
        return sortedValues[Math.Clamp(index, 0, sortedValues.Count - 1)];
    }
}
