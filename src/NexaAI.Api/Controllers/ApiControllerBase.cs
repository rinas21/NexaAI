using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using NexaAI.Core.Application.Common;

namespace NexaAI.Api.Controllers;

/// <summary>Shared error mapping and authenticated-user helpers for API controllers.</summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Gets the authenticated user's id from the JWT subject claim.</summary>
    protected Guid CurrentUserId
    {
        get
        {
            var raw = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("sub")?.Value
                      ?? User.FindFirst("id")?.Value;

            if (!Guid.TryParse(raw, out var id))
            {
                throw new UnauthorizedAccessException("A valid authenticated user is required.");
            }

            return id;
        }
    }

    /// <summary>Indicates whether the caller has the Admin role.</summary>
    protected bool IsAdmin => User.IsInRole("Admin");

    /// <summary>Maps domain exceptions to consistent ProblemDetails responses.</summary>
    protected ActionResult<T> Failure<T>(Exception exception)
    {
        var problem = BuildProblem(exception);
        return StatusCode(problem.Status ?? StatusCodes.Status500InternalServerError, problem);
    }

    /// <summary>Maps domain exceptions to consistent ProblemDetails responses.</summary>
    protected IActionResult Failure(Exception exception)
    {
        var problem = BuildProblem(exception);
        return StatusCode(problem.Status ?? StatusCodes.Status500InternalServerError, problem);
    }

    private static ProblemDetails BuildProblem(Exception exception) => exception switch
    {
        ApplicationExceptionBase app => new ProblemDetails
        {
            Status = app.StatusCode,
            Title = app.StatusCode switch
            {
                401 => "Unauthorized",
                403 => "Forbidden",
                404 => "Not Found",
                409 => "Conflict",
                502 => "Bad Gateway",
                _ => "Request failed"
            },
            Detail = app.Message
        },
        ArgumentException arg => new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Bad Request",
            Detail = arg.Message
        },
        UnauthorizedAccessException auth => new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = auth.Message
        },
        KeyNotFoundException missing => new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Not Found",
            Detail = missing.Message
        },
        InvalidOperationException invalid => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflict",
            Detail = invalid.Message
        },
        _ => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Detail = "An unexpected error occurred."
        }
    };
}
