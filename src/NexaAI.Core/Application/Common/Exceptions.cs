namespace NexaAI.Core.Application.Common;

/// <summary>Base class for errors that map to a specific HTTP status code.</summary>
public abstract class ApplicationExceptionBase(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public abstract int StatusCode { get; }
}

/// <summary>The requested resource does not exist.</summary>
public sealed class NotFoundException(string message) : ApplicationExceptionBase(message)
{
    public override int StatusCode => 404;
}

/// <summary>The current user may not access the requested resource.</summary>
public sealed class ForbiddenException(string message) : ApplicationExceptionBase(message)
{
    public override int StatusCode => 403;
}

/// <summary>The operation conflicts with existing state.</summary>
public sealed class ConflictException(string message) : ApplicationExceptionBase(message)
{
    public override int StatusCode => 409;
}

/// <summary>The supplied credentials or token were rejected.</summary>
public sealed class UnauthorizedException(string message) : ApplicationExceptionBase(message)
{
    public override int StatusCode => 401;
}

/// <summary>The downstream Python AI service failed or is unavailable.</summary>
public sealed class AIServiceException(string message, Exception? innerException = null)
    : ApplicationExceptionBase(message, innerException)
{
    public override int StatusCode => 502;
}
