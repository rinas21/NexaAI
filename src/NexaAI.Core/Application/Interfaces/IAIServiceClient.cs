using System.Text.Json;

namespace NexaAI.Core.Application.Interfaces;

/// <summary>
/// Client for communicating with the Python FastAPI AI service.
/// </summary>
public interface IAIServiceClient
{
    Task<JsonDocument> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
    Task<JsonDocument> SearchDocumentsAsync(string query, int topK, double threshold, string? metadataFilter, CancellationToken cancellationToken = default);
    Task<JsonDocument> RagChatAsync(string message, IReadOnlyList<JsonDocument> history, Guid userId, CancellationToken cancellationToken = default);
    Task<JsonDocument> ExecuteAgentAsync(string input, IReadOnlyList<string> tools, CancellationToken cancellationToken = default);
    Task<JsonDocument> RunEvaluationAsync(JsonDocument dataset, CancellationToken cancellationToken = default);
    Task<JsonDocument> DetectPiiAsync(string text, CancellationToken cancellationToken = default);
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}