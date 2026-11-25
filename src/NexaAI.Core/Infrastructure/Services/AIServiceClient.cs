using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NexaAI.Core.Application.Common;
using NexaAI.Core.Application.Configuration;
using NexaAI.Core.Application.Interfaces;

namespace NexaAI.Core.Infrastructure.Services;

/// <summary>
/// HTTP client for the local Python FastAPI AI service. Uses the named "AIService"
/// HttpClient so the Polly retry/circuit-breaker policies from Program.cs apply.
/// </summary>
public sealed class AIServiceClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AIServiceOptions> options,
    ILogger<AIServiceClient> logger) : IAIServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("AIService");
    private readonly AIServiceOptions _options = options.Value;

    /// <inheritdoc />
    public Task<JsonDocument> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default) =>
        PostAsync("api/v1/embeddings", new { text }, cancellationToken);

    /// <inheritdoc />
    public Task<JsonDocument> SearchDocumentsAsync(
        string query,
        int topK,
        double threshold,
        string? metadataFilter,
        CancellationToken cancellationToken = default) =>
        PostAsync("api/v1/search", new
        {
            query,
            top_k = Math.Clamp(topK, 1, 100),
            threshold,
            metadata_filter = ParseFilter(metadataFilter)
        }, cancellationToken);

    /// <inheritdoc />
    public Task<JsonDocument> RagChatAsync(
        string message,
        IReadOnlyList<JsonDocument> history,
        Guid userId,
        CancellationToken cancellationToken = default) =>
        PostAsync("api/v1/rag", new
        {
            message,
            history = history.Select(h => h.RootElement.Clone()).ToList(),
            user_id = userId.ToString()
        }, cancellationToken);

    /// <inheritdoc />
    public Task<JsonDocument> ExecuteAgentAsync(
        string input,
        IReadOnlyList<string> tools,
        CancellationToken cancellationToken = default) =>
        PostAsync("api/v1/agents/execute", new { input, tools }, cancellationToken);

    /// <inheritdoc />
    public Task<JsonDocument> RunEvaluationAsync(JsonDocument dataset, CancellationToken cancellationToken = default) =>
        PostAsync("api/v1/evaluation/run", dataset.RootElement, cancellationToken);

    /// <inheritdoc />
    public Task<JsonDocument> DetectPiiAsync(string text, CancellationToken cancellationToken = default) =>
        PostAsync("api/v1/security/pii/detect", new { text }, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "AI service health probe failed");
            return false;
        }
    }

    private async Task<JsonDocument> PostAsync(string path, object payload, CancellationToken cancellationToken)
    {
        EnsureApiKeyHeader();

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            using var response = await _httpClient.PostAsync(path, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("AI service {Path} returned {StatusCode}: {Body}", path, (int)response.StatusCode, Truncate(body, 500));
                throw new AIServiceException($"AI service request to {path} failed with status {(int)response.StatusCode}: {Truncate(body, 300)}");
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                return JsonHelpers.EmptyObject();
            }

            return JsonDocument.Parse(body);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AIServiceException($"AI service request to {path} timed out.");
        }
        catch (HttpRequestException ex)
        {
            throw new AIServiceException($"Could not reach the AI service at {_options.BaseUrl}: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            throw new AIServiceException($"AI service returned invalid JSON for {path}.", ex);
        }
    }

    private void EnsureApiKeyHeader()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey) || _httpClient.DefaultRequestHeaders.Contains("X-API-Key"))
        {
            return;
        }

        _httpClient.DefaultRequestHeaders.Add("X-API-Key", _options.ApiKey);
    }

    private static JsonElement? ParseFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(filter).RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
