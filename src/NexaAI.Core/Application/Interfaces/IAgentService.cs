using NexaAI.Core.Application.DTOs;

namespace NexaAI.Core.Application.Interfaces;

public interface IAgentService
{
    Task<AgentExecutionResponse> ExecuteAsync(AgentExecutionRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentExecutionResponse>> GetExecutionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AgentExecutionResponse?> GetExecutionAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ToolApprovalResponse>> GetPendingApprovalsAsync(CancellationToken cancellationToken = default);
    Task<bool> DecideApprovalAsync(Guid approvalId, bool approved, Guid adminId, string? reason = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ToolDefinitionDto>> GetAvailableToolsAsync(CancellationToken cancellationToken = default);
}