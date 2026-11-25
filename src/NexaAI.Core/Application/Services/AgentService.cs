using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NexaAI.Core.Application.Common;
using NexaAI.Core.Application.DTOs;
using NexaAI.Core.Application.Interfaces;
using NexaAI.Core.Domain.Entities;
using NexaAI.Core.Infrastructure.Persistence;

namespace NexaAI.Core.Application.Services;

/// <summary>Agent execution logging, tool approval workflow and tool catalog.</summary>
public sealed class AgentService(
    AppDbContext db,
    IAIServiceClient aiClient,
    ILogger<AgentService> logger) : IAgentService
{
    private static readonly IReadOnlyList<ToolDefinitionDto> ToolCatalog =
    [
        new(
            "document_search",
            "Semantic and keyword search across documents the caller is allowed to read.",
            JsonHelpers.ToDocument(new { query = "string", top_k = 10, metadata_filter = "object?" }),
            false),
        new(
            "get_document",
            "Retrieve a document and its metadata by id.",
            JsonHelpers.ToDocument(new { document_id = "uuid" }),
            false),
        new(
            "list_documents",
            "List documents visible to the caller.",
            JsonHelpers.ToDocument(new { status = "string?", access_level = "string?" }),
            false),
        new(
            "calculator",
            "Evaluate a deterministic arithmetic expression.",
            JsonHelpers.ToDocument(new { expression = "string" }),
            false),
        new(
            "datetime",
            "Return the current date and time in a requested time zone.",
            JsonHelpers.ToDocument(new { time_zone = "string?" }),
            false),
        new(
            "database_query",
            "Execute a read-only, parameterized SQL query against the application database.",
            JsonHelpers.ToDocument(new { sql = "string", parameters = "object?" }),
            true)
    ];

    /// <inheritdoc />
    public async Task<AgentExecutionResponse> ExecuteAsync(AgentExecutionRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        var tools = ReadTools(request.Parameters);
        var input = JsonHelpers.ToDocument(new
        {
            input = request.Input,
            parameters = request.Parameters?.RootElement ?? JsonHelpers.EmptyObject().RootElement,
            tools
        });

        var execution = AgentExecution.Create(request.AgentName, input, userId == Guid.Empty ? null : userId);
        db.AgentExecutions.Add(execution);

        if (request.RequireApproval || tools.Any(t => ToolCatalog.Any(def => def.Name == t && def.RequiresApproval)))
        {
            var approvalTools = tools.Count > 0 ? tools : new[] { "agent_execution" };
            foreach (var tool in approvalTools)
            {
                db.ToolApprovals.Add(CreateApproval(execution.Id, tool, request.Parameters, userId));
            }

            execution.SetPendingApproval();
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Agent execution {ExecutionId} is awaiting approval", execution.Id);
            return ToResponse(execution);
        }

        await RunAsync(execution, request.Input, tools, cancellationToken);
        return ToResponse(execution);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AgentExecutionResponse>> GetExecutionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var query = db.AgentExecutions.AsQueryable();
        // Guid.Empty is the convention used by controllers for administrators (all users).
        if (userId != Guid.Empty)
        {
            query = query.Where(e => e.UserId == userId);
        }

        var executions = await query
            .OrderByDescending(e => e.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        return executions.Select(ToResponse).ToList();
    }

    /// <inheritdoc />
    public async Task<AgentExecutionResponse?> GetExecutionAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var execution = await db.AgentExecutions.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (execution is null || (userId != Guid.Empty && execution.UserId != userId))
        {
            return null;
        }

        return ToResponse(execution);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ToolApprovalResponse>> GetPendingApprovalsAsync(CancellationToken cancellationToken = default)
    {
        var approvals = await db.ToolApprovals
            .Where(a => a.Status == ApprovalStatus.Pending)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
        return approvals.Select(ToResponse).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> DecideApprovalAsync(
        Guid approvalId,
        bool approved,
        Guid adminId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var approval = await db.ToolApprovals
            .Include(a => a.Execution)
            .FirstOrDefaultAsync(a => a.Id == approvalId, cancellationToken)
            ?? throw new NotFoundException("Tool approval not found.");

        if (approval.Status != ApprovalStatus.Pending)
        {
            throw new ConflictException("This tool approval has already been decided.");
        }

        if (approved)
        {
            approval.Approve(adminId, reason);
        }
        else
        {
            approval.Reject(adminId, reason);
        }

        var execution = approval.Execution
                        ?? await db.AgentExecutions.FindAsync([approval.ExecutionId], cancellationToken)
                        ?? throw new NotFoundException("Agent execution not found.");

        var remaining = await db.ToolApprovals
            .Where(a => a.ExecutionId == execution.Id && a.Status == ApprovalStatus.Pending)
            .CountAsync(cancellationToken);
        var anyRejected = await db.ToolApprovals
            .AnyAsync(a => a.ExecutionId == execution.Id && a.Status == ApprovalStatus.Rejected, cancellationToken);

        if (remaining == 0)
        {
            if (anyRejected)
            {
                execution.Fail($"Tool approval rejected: {reason ?? "no reason provided"}", 0);
            }
            else
            {
                var tools = ReadTools(execution.Input.RootElement);
                var input = JsonHelpers.GetString(execution.Input.RootElement, "input") ?? string.Empty;
                execution.SetRunning();
                await db.SaveChangesAsync(cancellationToken);
                await RunAsync(execution, input, tools, cancellationToken);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Approval {ApprovalId} {Decision} by {AdminId}",
            approvalId, approved ? "approved" : "rejected", adminId);
        return true;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ToolDefinitionDto>> GetAvailableToolsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ToolCatalog);

    private async Task RunAsync(
        AgentExecution execution,
        string input,
        IReadOnlyList<string> tools,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var output = await aiClient.ExecuteAgentAsync(input, tools, cancellationToken);
            stopwatch.Stop();
            execution.Complete(output, (int)stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            logger.LogError(ex, "Agent execution {ExecutionId} failed", execution.Id);
            execution.Fail(ex.Message, (int)stopwatch.ElapsedMilliseconds);
            await db.SaveChangesAsync(CancellationToken.None);
            throw new AIServiceException($"Agent execution failed: {ex.Message}", ex);
        }
    }

    private static ToolApproval CreateApproval(
        Guid executionId,
        string tool,
        JsonDocument? parameters,
        Guid userId) =>
        ToolApproval.Create(
            executionId,
            tool,
            parameters ?? JsonHelpers.EmptyObject(),
            userId == Guid.Empty ? null : userId);

    private static IReadOnlyList<string> ReadTools(JsonDocument? parameters)
    {
        var tools = new List<string>();
        if (parameters is null)
        {
            return tools;
        }

        var array = JsonHelpers.GetArray(parameters.RootElement, "tools");
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var name = item.GetString();
                if (!string.IsNullOrWhiteSpace(name) && !tools.Contains(name))
                {
                    tools.Add(name);
                }
            }
        }

        return tools;
    }

    private static IReadOnlyList<string> ReadTools(JsonElement input)
    {
        if (!JsonHelpers.TryGetProperty(input, "tools", out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .ToList();
    }

    private static AgentExecutionResponse ToResponse(AgentExecution execution) => new(
        execution.Id,
        execution.AgentName,
        execution.Status,
        execution.Input,
        execution.Output,
        execution.ErrorMessage,
        execution.ExecutionTimeMs,
        execution.CreatedAt,
        execution.CompletedAt);

    private static ToolApprovalResponse ToResponse(ToolApproval approval) => new(
        approval.Id,
        approval.ExecutionId,
        approval.ToolName,
        approval.Parameters,
        approval.Status,
        approval.Reason,
        approval.CreatedAt,
        approval.DecidedAt);
}
