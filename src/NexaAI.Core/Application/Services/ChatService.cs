using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexaAI.Core.Application.Common;
using NexaAI.Core.Application.DTOs;
using NexaAI.Core.Application.Interfaces;
using NexaAI.Core.Domain.Entities;
using NexaAI.Core.Infrastructure.Persistence;

namespace NexaAI.Core.Application.Services;

/// <summary>Conversation persistence and retrieval-augmented chat via the AI service.</summary>
public sealed class ChatService(
    AppDbContext db,
    IAIServiceClient aiClient,
    ILogger<ChatService> logger) : IChatService
{
    private const int HistoryLimit = 20;

    /// <inheritdoc />
    public async Task<ChatResponse> SendMessageAsync(ChatRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsureUserAsync(userId, cancellationToken);

        Conversation conversation;
        if (request.ConversationId is { } id && id != Guid.Empty)
        {
            conversation = await db.Conversations.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken)
                           ?? throw new NotFoundException("Conversation not found.");
        }
        else
        {
            conversation = Conversation.Create(userId, Truncate(request.Message, 80));
            db.Conversations.Add(conversation);
        }

        var history = await db.Messages
            .Where(m => m.ConversationId == conversation.Id)
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Take(HistoryLimit)
            .ToListAsync(cancellationToken);

        var historyDocuments = history
            .Select(m => JsonHelpers.ToDocument(new { role = m.Role, content = m.Content }))
            .ToList();

        var response = await aiClient.RagChatAsync(request.Message, historyDocuments, userId, cancellationToken);
        var chat = ParseChatResponse(response);

        var userMessage = Message.Create(conversation.Id, MessageRole.User, request.Message, metadata: request.Metadata);
        var assistantMetadata = JsonHelpers.ToDocument(new
        {
            use_agent = request.UseAgent,
            total_latency_ms = chat.TotalLatencyMs,
            token_usage = chat.TokenUsage
        });
        var assistantMessage = Message.Create(
            conversation.Id,
            MessageRole.Assistant,
            chat.Content,
            JsonHelpers.ToDocument(chat.Citations),
            JsonHelpers.EmptyArray(),
            assistantMetadata);

        db.Messages.AddRange(userMessage, assistantMessage);
        conversation.Touch();
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Chat message persisted for conversation {ConversationId} with {CitationCount} citation(s)",
            conversation.Id, chat.Citations.Count);

        return chat with { ConversationId = conversation.Id, MessageId = assistantMessage.Id };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConversationResponse>> GetConversationsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsureUserAsync(userId, cancellationToken);
        var conversations = await db.Conversations
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(cancellationToken);

        return await ToResponsesAsync(conversations, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConversationResponse?> GetConversationAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var conversation = await db.Conversations
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);
        if (conversation is null)
        {
            return null;
        }

        var responses = await ToResponsesAsync([conversation], cancellationToken);
        return responses.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<Guid> CreateConversationAsync(CreateConversationRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsureUserAsync(userId, cancellationToken);
        var conversation = Conversation.Create(userId, request.Title);
        db.Conversations.Add(conversation);
        await db.SaveChangesAsync(cancellationToken);
        return conversation.Id;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteConversationAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var conversation = await db.Conversations.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);
        if (conversation is null)
        {
            return false;
        }

        db.Conversations.Remove(conversation);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<IReadOnlyList<ConversationResponse>> ToResponsesAsync(
        IReadOnlyList<Conversation> conversations,
        CancellationToken cancellationToken)
    {
        if (conversations.Count == 0)
        {
            return [];
        }

        var ids = conversations.Select(c => c.Id).ToList();
        var messages = await db.Messages
            .Where(m => ids.Contains(m.ConversationId))
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

        return conversations.Select(conversation => new ConversationResponse(
            conversation.Id,
            conversation.Title,
            conversation.CreatedAt,
            conversation.UpdatedAt,
            messages
                .Where(m => m.ConversationId == conversation.Id)
                .Select(m => new MessageResponse(m.Id, m.Role, m.Content, m.Citations, m.ToolCalls, m.CreatedAt))
                .ToList())).ToList();
    }

    private ChatResponse ParseChatResponse(JsonDocument response)
    {
        var root = response.RootElement;
        var answer = JsonHelpers.GetString(root, "content", "answer", "response", "text") ?? string.Empty;
        var citations = new List<CitationDto>();

        foreach (var item in JsonHelpers.GetArray(root, "citations", "sources").EnumerateArray())
        {
            var documentId = ParseGuid(JsonHelpers.GetString(item, "document_id", "doc_id", "documentId"));
            var chunkId = ParseGuid(JsonHelpers.GetString(item, "chunk_id", "id", "chunkId"));
            var title = JsonHelpers.GetString(item, "document_title", "title", "source", "documentTitle")
                        ?? (documentId is { } docId
                            ? db.Documents.Where(d => d.Id == docId).Select(d => d.Title).FirstOrDefault()
                            : null)
                        ?? "Unknown document";

            citations.Add(new CitationDto(
                documentId ?? Guid.Empty,
                chunkId ?? Guid.Empty,
                title,
                JsonHelpers.GetString(item, "snippet", "content", "text") ?? string.Empty,
                JsonHelpers.GetDouble(item, "similarity_score", "similarity", "score") ?? 0));
        }

        TokenUsageDto? tokenUsage = null;
        if (JsonHelpers.TryGetProperty(root, "token_usage", out var usage) ||
            JsonHelpers.TryGetProperty(root, "tokenUsage", out usage))
        {
            tokenUsage = new TokenUsageDto(
                JsonHelpers.GetInt(usage, "prompt_tokens", "promptTokens") ?? 0,
                JsonHelpers.GetInt(usage, "completion_tokens", "completionTokens") ?? 0,
                JsonHelpers.GetInt(usage, "total_tokens", "totalTokens") ?? 0);
        }

        return new ChatResponse(
            Guid.Empty,
            Guid.Empty,
            answer,
            citations,
            JsonHelpers.GetBool(root, "is_grounded", "grounded") ?? citations.Count > 0,
            JsonHelpers.GetString(root, "refusal_reason", "refusal"),
            JsonHelpers.GetInt(root, "total_latency_ms", "latency_ms", "latencyMs"),
            tokenUsage);
    }

    private async Task EnsureUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId, cancellationToken))
        {
            throw new ForbiddenException("The current user is not active.");
        }
    }

    private static Guid? ParseGuid(string? value) => Guid.TryParse(value, out var parsed) ? parsed : null;

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength].TrimEnd() + "…";
}
