using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace TrackBoard.Common;

/// <summary>
/// Single exit point for unhandled exceptions, producing RFC 7807 <c>ProblemDetails</c>.
/// This is the .NET counterpart to Spring's <c>@ControllerAdvice</c>.
/// </summary>
/// <remarks>
/// Only exception types the domain defines get their message echoed back. Anything else
/// returns a fixed string, so an unexpected failure can never leak a stack trace, a SQL
/// fragment, or a connection string to the caller — the detail goes to the log instead.
/// </remarks>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            NotFoundException notFound => (
                StatusCodes.Status404NotFound,
                "Resource not found",
                notFound.Message),

            ConflictException conflict => (
                StatusCodes.Status409Conflict,
                "Request conflicts with the current state",
                conflict.Message),

            // 401 and 403 are not interchangeable: 401 means we cannot identify the caller,
            // 403 means we can and they still may not do this.
            UnauthorizedException unauthorized => (
                StatusCodes.Status401Unauthorized,
                "Authentication failed",
                unauthorized.Message),

            ForbiddenException forbidden => (
                StatusCodes.Status403Forbidden,
                "Access denied",
                forbidden.Message),

            // 499 is nginx's "client closed request"; ASP.NET Core has no constant for it.
            OperationCanceledException => (
                499,
                "Request cancelled",
                "The client closed the request before it completed."),

            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred",
                "The request could not be completed. Contact support with the trace identifier."),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation(
                "Request failed with {Status} on {Method} {Path}: {Reason}",
                status,
                httpContext.Request.Method,
                httpContext.Request.Path,
                exception.Message);
        }

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Instance = $"{httpContext.Request.Method} {httpContext.Request.Path}",
            },
        });
    }
}
