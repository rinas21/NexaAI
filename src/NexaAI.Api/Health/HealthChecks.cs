using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NexaAI.Core.Infrastructure.Persistence;

namespace NexaAI.Api.Health;

/// <summary>Verifies that the PostgreSQL database is reachable.</summary>
public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("PostgreSQL is reachable.")
                : HealthCheckResult.Unhealthy("PostgreSQL connection failed.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL connection failed.", ex);
        }
    }
}

/// <summary>Verifies that an HTTP dependency responds successfully.</summary>
public sealed class UrlHealthCheck(string name, HttpClient client, string path) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        try
        {
            using var response = await client.GetAsync(path, timeout.Token);
            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy($"{name} responded successfully.")
                : HealthCheckResult.Unhealthy($"{name} returned {(int)response.StatusCode}.");
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy($"{name} is unreachable.", ex);
        }
    }
}
