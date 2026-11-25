using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NexaAI.Core.Application.DTOs;
using NexaAI.Core.Application.Interfaces;

namespace NexaAI.Api.Controllers;

/// <summary>MCP-style tool discovery and invocation surface backed by the agent service.</summary>
[Route("mcp")]
[Authorize]
public sealed class McpController(IAgentService agentService, IConfiguration configuration) : ApiControllerBase
{
    /// <summary>Lists tools exposed to MCP clients.</summary>
    [HttpGet("tools")]
    [ProducesResponseType(typeof(IReadOnlyList<ToolDefinitionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ToolDefinitionDto>>> Tools(CancellationToken cancellationToken)
    {
        if (!IsMcpEnabled())
        {
            return NotFound();
        }

        try
        {
            return Ok(await agentService.GetAvailableToolsAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            return Failure<IReadOnlyList<ToolDefinitionDto>>(ex);
        }
    }

    /// <summary>Calls a named MCP tool with the supplied arguments.</summary>
    [HttpPost("tools/{name}/call")]
    [ProducesResponseType(typeof(AgentExecutionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AgentExecutionResponse>> Call(
        string name,
        [FromBody] JsonElement arguments,
        CancellationToken cancellationToken)
    {
        if (!IsMcpEnabled())
        {
            return NotFound();
        }

        try
        {
            var tools = await agentService.GetAvailableToolsAsync(cancellationToken);
            if (tools.All(t => !string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return NotFound(new ProblemDetails { Status = 404, Title = "Not Found", Detail = $"MCP tool '{name}' is not registered." });
            }

            var input = ReadArgument(arguments, "input", "message", "query", "prompt")
                        ?? arguments.GetRawText();
            var parameters = JsonDocument.Parse(arguments.ValueKind == JsonValueKind.Object ? arguments.GetRawText() : "{}");

            var execution = await agentService.ExecuteAsync(
                new AgentExecutionRequest(name, input, false, parameters), CurrentUserId, cancellationToken);
            return Ok(execution);
        }
        catch (Exception ex)
        {
            return Failure<AgentExecutionResponse>(ex);
        }
    }

    private bool IsMcpEnabled() =>
        bool.TryParse(configuration["FeatureFlags:EnableMcp"], out var enabled) ? enabled : true;

    private static string? ReadArgument(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }
        }

        return null;
    }
}
