using Polly;
using Polly.Extensions.Http;

namespace NexaAI.Api.Extensions;

public static class PolicyBuilder
{
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var logger = context.GetLogger();
                    logger?.LogWarning("Retry {RetryCount} after {Timespan}s due to: {Reason}",
                        retryCount, timespan.TotalSeconds, outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                });
    }

    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (outcome, timespan, context) =>
                {
                    var logger = context.GetLogger();
                    logger?.LogWarning("Circuit breaker opened for {Timespan}s due to: {Reason}",
                        timespan.TotalSeconds, outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString());
                },
                onReset: context =>
                {
                    var logger = context.GetLogger();
                    logger?.LogInformation("Circuit breaker reset");
                });
    }

    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30));
    }
}

internal static class ContextExtensions
{
    public static ILogger? GetLogger(this Context context)
    {
        if (context.TryGetValue("logger", out var loggerObj) && loggerObj is ILogger logger)
        {
            return logger;
        }
        return null;
    }
}