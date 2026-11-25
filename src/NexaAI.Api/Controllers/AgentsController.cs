using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaAI.Core.Application.DTOs;
using NexaAI.Core.Application.Interfaces;

namespace NexaAI.Api.Controllers;

/// <summary>Agent execution history and human approval of sensitive tool calls.</summary>
[Route("api/agents")]
public sealed class AgentsController(IAgentService agentService) : ApiControllerBase
{
    /// <summary>Executes an agent, optionally pausing for tool approval.</summary>
    [HttpPost("execute")]
    [Authorize]
    [ProducesResponseType(typeof(AgentExecutionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AgentExecutionResponse>> Execute(
        [FromBody] AgentExecutionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await agentService.ExecuteAsync(request, CurrentUserId, cancellationToken));
        }
        catch (Exception ex)
        {
            return Failure<AgentExecutionResponse>(ex);
        }
    }

    /// <summary>Lists recent executions for the current user (all users for administrators).</summary>
    [HttpGet("executions")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<AgentExecutionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AgentExecutionResponse>>> Executions(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await agentService.GetExecutionsAsync(IsAdmin ? Guid.Empty : CurrentUserId, cancellationToken));
        }
        catch (Exception ex)
        {
            return Failure<IReadOnlyList<AgentExecutionResponse>>(ex);
        }
    }

    /// <summary>Returns a single agent execution.</summary>
    [HttpGet("executions/{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(AgentExecutionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AgentExecutionResponse>> Execution(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var execution = await agentService.GetExecutionAsync(
                id, IsAdmin ? Guid.Empty : CurrentUserId, cancellationToken);
            return execution is null ? NotFound() : Ok(execution);
        }
        catch (Exception ex)
        {
            return Failure<AgentExecutionResponse>(ex);
        }
    }

    /// <summary>Lists tool calls awaiting administrator approval.</summary>
    [HttpGet("approvals")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(IReadOnlyList<ToolApprovalResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ToolApprovalResponse>>> PendingApprovals(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await agentService.GetPendingApprovalsAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return Failure<IReadOnlyList<ToolApprovalResponse>>(ex);
        }
    }

    /// <summary>Approves a pending tool call and resumes its execution.</summary>
    [HttpPost("approvals/{id:guid}/approve")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromBody] ApprovalDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            await agentService.DecideApprovalAsync(id, true, CurrentUserId, request?.Reason, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return Failure(ex);
        }
    }

    /// <summary>Rejects a pending tool call.</summary>
    [HttpPost("approvals/{id:guid}/reject")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reject(
        Guid id,
        [FromBody] ApprovalDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        try
        {
            await agentService.DecideApprovalAsync(id, false, CurrentUserId, request?.Reason, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            return Failure(ex);
        }
    }

    /// <summary>Lists tools available to the agent runtime.</summary>
    [HttpGet("tools")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<ToolDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ToolDefinitionDto>>> Tools(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await agentService.GetAvailableToolsAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return Failure<IReadOnlyList<ToolDefinitionDto>>(ex);
        }
    }
}
