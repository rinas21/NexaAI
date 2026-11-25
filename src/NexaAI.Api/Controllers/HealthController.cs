using System.Diagnostics;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexaAI.Core.Application.Interfaces;
using NexaAI.Core.Infrastructure.Persistence;

namespace NexaAI.Api.Controllers;

/// <summary>Liveness, detailed dependency health and basic process metrics.</summary>
[Route("health")]
public sealed class HealthController(
    IAIServiceClient aiClient,
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    IWebHostEnvironment environment) : ApiControllerBase
{
    /// <summary>Returns a lightweight liveness response.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> Basic(CancellationToken cancellationToken)
    {
        var aiHealthy = await aiClient.IsHealthyAsync(cancellationToken);
        return Ok(new
        {
            status = aiHealthy ? "Healthy" : "Degraded",
            environment = environment.EnvironmentName,
            timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>Returns detailed database, AI service and Ollama health (administrators only).</summary>
    [HttpGet("detailed")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> Detailed(CancellationToken cancellationToken)
    {
        var databaseHealthy = await db.Database.CanConnectAsync(cancellationToken);
        var aiHealthy = await aiClient.IsHealthyAsync(cancellationToken);
        var ollamaHealthy = await CheckOllamaAsync(cancellationToken);
        var allHealthy = databaseHealthy && aiHealthy && ollamaHealthy;

        return Ok(new
        {
            status = allHealthy ? "Healthy" : "Degraded",
            checks = new
            {
                database = databaseHealthy,
                aiService = aiHealthy,
                ollama = ollamaHealthy
            },
            environment = environment.EnvironmentName,
            timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>Exposes basic .NET process metrics in Prometheus text format.</summary>
    [HttpGet("/metrics")]
    [AllowAnonymous]
    [Produces("text/plain")]
    public ActionResult<string> Metrics()
    {
        var process = Process.GetCurrentProcess();
        var builder = new StringBuilder();
        builder.AppendLine("# HELP process_working_set_bytes Current process working set in bytes.");
        builder.AppendLine("# TYPE process_working_set_bytes gauge");
        builder.AppendLine($"process_working_set_bytes {process.WorkingSet64}");
        builder.AppendLine("# HELP dotnet_managed_heap_bytes Managed heap size in bytes.");
        builder.AppendLine("# TYPE dotnet_managed_heap_bytes gauge");
        builder.AppendLine($"dotnet_managed_heap_bytes {GC.GetTotalMemory(false)}");
        builder.AppendLine("# HELP dotnet_gc_collection_count_total Number of GC collections by generation.");
        builder.AppendLine("# TYPE dotnet_gc_collection_count_total counter");
        for (var generation = 0; generation <= 2; generation++)
        {
            builder.AppendLine($"dotnet_gc_collection_count_total{{generation=\"{generation}\"}} {GC.CollectionCount(generation)}");
        }

        return Content(builder.ToString(), "text/plain; version=0.0.4");
    }

    private async Task<bool> CheckOllamaAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            var client = httpClientFactory.CreateClient("Ollama");
            using var response = await client.GetAsync("api/tags", timeout.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return false;
        }
    }
}
