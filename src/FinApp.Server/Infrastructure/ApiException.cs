namespace FinApp.Server.Infrastructure;

/// <summary>An error that maps to a specific HTTP status code. Thrown by services, translated to a response by the pipeline.</summary>
public class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class BadRequestException(string message) : ApiException(StatusCodes.Status400BadRequest, message);
public sealed class UnauthorizedException(string message) : ApiException(StatusCodes.Status401Unauthorized, message);
public sealed class ForbiddenException(string message) : ApiException(StatusCodes.Status403Forbidden, message);
public sealed class NotFoundException(string message) : ApiException(StatusCodes.Status404NotFound, message);
public sealed class ConflictException(string message) : ApiException(StatusCodes.Status409Conflict, message);
