using NexaAI.Core.Application.DTOs;

namespace NexaAI.Core.Application.Interfaces;

public interface IChatService
{
    Task<ChatResponse> SendMessageAsync(ChatRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConversationResponse>> GetConversationsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ConversationResponse?> GetConversationAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<Guid> CreateConversationAsync(CreateConversationRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteConversationAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
}