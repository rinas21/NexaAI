using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Context;

namespace NexaAI.Api.Middleware;

public class RequestResponseLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;

    public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip logging for health checks and static files
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/swagger") ||
            context.Request.Path.Value?.EndsWith(".js") == true ||
            context.Request.Path.Value?.EndsWith(".css") == true ||
            context.Request.Path.Value?.EndsWith(".map") == true)
        {
            await _next(context);
            return;
        }

        var requestId = context.TraceIdentifier;
        var userId = context.User?.FindFirst("sub")?.Value ?? context.User?.FindFirst("id")?.Value ?? "anonymous";

        using (LogContext.PushProperty("RequestId", requestId))
        using (LogContext.PushProperty("UserId", userId))
        using (LogContext.PushProperty("RequestMethod", context.Request.Method))
        using (LogContext.PushProperty("RequestPath", context.Request.Path))
        {
            // Log request
            _logger.LogInformation("HTTP {Method} {Path} started", context.Request.Method, context.Request.Path);

            // Capture request body if needed (be careful with sensitive data)
            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            try
            {
                await _next(context);

                // Log response
                responseBody.Seek(0, SeekOrigin.Begin);
                var responseText = await new StreamReader(responseBody).ReadToEndAsync();
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBodyStream);

                _logger.LogInformation(
                    "HTTP {Method} {Path} completed with {StatusCode}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "HTTP {Method} {Path} failed", context.Request.Method, context.Request.Path);
                throw;
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }
        }
    }
}