using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaAI.Core.Application.DTOs;
using NexaAI.Core.Application.Interfaces;

namespace NexaAI.Api.Controllers;

/// <summary>AI quality evaluation runs and aggregated metrics (administrators only).</summary>
[Route("api/evaluation")]
[Authorize(Policy = "AdminOnly")]
public sealed class EvaluationController(IEvaluationService evaluationService) : ApiControllerBase
{
    /// <summary>Starts an evaluation run against the configured dataset.</summary>
    [HttpPost("run")]
    [ProducesResponseType(typeof(EvaluationRunResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<EvaluationRunResponse>> Run(
        [FromBody] StartEvaluationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await evaluationService.StartAsync(request, cancellationToken));
        }
        catch (Exception ex)
        {
            return Failure<EvaluationRunResponse>(ex);
        }
    }

    /// <summary>Lists evaluation runs with their results.</summary>
    [HttpGet("runs")]
    [ProducesResponseType(typeof(IReadOnlyList<EvaluationRunResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EvaluationRunResponse>>> Runs(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await evaluationService.GetRunsAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return Failure<IReadOnlyList<EvaluationRunResponse>>(ex);
        }
    }

    /// <summary>Returns one evaluation run and its per-question metrics.</summary>
    [HttpGet("runs/{id:guid}")]
    [ProducesResponseType(typeof(EvaluationRunResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<EvaluationRunResponse>> RunById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var run = await evaluationService.GetRunAsync(id, cancellationToken);
            return run is null ? NotFound() : Ok(run);
        }
        catch (Exception ex)
        {
            return Failure<EvaluationRunResponse>(ex);
        }
    }

    /// <summary>Returns aggregated retrieval, generation, latency and token metrics.</summary>
    [HttpGet("metrics/summary")]
    [ProducesResponseType(typeof(EvaluationSummaryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<EvaluationSummaryResponse>> Summary(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await evaluationService.GetSummaryAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return Failure<EvaluationSummaryResponse>(ex);
        }
    }
}
