using System.Data.Common;
using Microsoft.AspNetCore.Diagnostics;

namespace BankTransferService.Diagnostics;

/// <summary>
/// Centralized exception handler implementing the modern IExceptionHandler pattern.
/// Maps exceptions to appropriate HTTP status codes and problem details responses.
/// </summary>
public class CustomExceptionHandler : IExceptionHandler
{
    private readonly ILogger<CustomExceptionHandler> _logger;

    public CustomExceptionHandler(ILogger<CustomExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        // Determine status code based on exception type
        var statusCode = exception switch
        {
            DbException => StatusCodes.Status500InternalServerError,
            InvalidOperationException => StatusCodes.Status500InternalServerError,
            ArgumentException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError,
        };

        _logger.LogError(
            exception,
            "Exception handled: {ExceptionType}, Status: {StatusCode}",
            exception.GetType().Name,
            statusCode
        );

        httpContext.Response.StatusCode = statusCode;

        // Get the problem details service
        var problemDetailsService =
            httpContext.RequestServices.GetService<IProblemDetailsService>();

        if (problemDetailsService is null)
            return false;

        // Write problem details response
        await problemDetailsService.WriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new()
                {
                    Title = GetTitle(exception),
                    Detail = GetDetail(exception, httpContext.RequestServices),
                    Status = statusCode,
                },
            }
        );

        return true;
    }

    private static string GetTitle(Exception exception) =>
        exception switch
        {
            DbException => "Database Error",
            InvalidOperationException => "Operation Error",
            ArgumentException => "Invalid Argument",
            _ => "An Unexpected Error Occurred",
        };

    private static string GetDetail(Exception exception, IServiceProvider services)
    {
        var env = services.GetService<IWebHostEnvironment>();

        // In development, include exception message; in production, generic message
        return env?.IsDevelopment() == true
            ? exception.Message
            : "An unexpected error occurred. Please try again later.";
    }
}
