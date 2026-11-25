using NexaAI.Core.Application.DTOs;

namespace NexaAI.Core.Application.Interfaces;

public interface IEvaluationService
{
    Task<EvaluationRunResponse> StartAsync(StartEvaluationRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EvaluationRunResponse>> GetRunsAsync(CancellationToken cancellationToken = default);
    Task<EvaluationRunResponse?> GetRunAsync(Guid id, CancellationToken cancellationToken = default);
    Task<EvaluationSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default);
}