using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace OrderManagementSystem.API.Middlewares
{
    public class ExceptionHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IHostEnvironment _env;

        public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
        {
            _next = next;
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            var (statusCode, title) = ex switch
            {
                // 400 - Bad Request: caller sent invalid arguments/data
                ArgumentException
                or ArgumentNullException
                or ArgumentOutOfRangeException       => (StatusCodes.Status400BadRequest,          "Bad Request"),

                // 401 - Unauthorized: identity not established
                UnauthorizedAccessException          => (StatusCodes.Status401Unauthorized,         "Unauthorized"),

                // 403 - Forbidden: identity established but access denied
                // Use a custom ForbiddenException or map AccessViolationException
                AccessViolationException             => (StatusCodes.Status403Forbidden,            "Forbidden"),

                // 404 - Not Found: requested resource does not exist
                KeyNotFoundException                 => (StatusCodes.Status404NotFound,             "Not Found"),

                // 405 - Method Not Allowed
                NotSupportedException                => (StatusCodes.Status405MethodNotAllowed,     "Method Not Allowed"),

                // 408 - Request Timeout / 499 - Client closed request
                OperationCanceledException           => (StatusCodes.Status408RequestTimeout,       "Request Timeout"),

                // 409 - Conflict: state conflict (e.g., duplicate order, stale optimistic lock)
                InvalidOperationException            => (StatusCodes.Status409Conflict,             "Conflict"),

                // 422 - Unprocessable Entity: syntactically valid but business-rule violation
                // Map via a custom DomainValidationException if desired
                FormatException                      => (StatusCodes.Status422UnprocessableEntity,  "Unprocessable Entity"),

                // 429 - Too Many Requests (custom exception recommended)
                // 503 - Service Unavailable: downstream dependency (DB/Redis) is down
                TimeoutException                     => (StatusCodes.Status503ServiceUnavailable,   "Service Unavailable"),

                // 500 - Internal Server Error: anything else unexpected
                _                                   => (StatusCodes.Status500InternalServerError,   "Internal Server Error")
            };

            // Log level varies by severity
            if (statusCode >= StatusCodes.Status500InternalServerError)
                _logger.LogError(ex, "Unhandled exception [{ExceptionType}]: {Message}", ex.GetType().Name, ex.Message);
            else
                _logger.LogWarning(ex, "Handled exception [{ExceptionType}] mapped to HTTP {StatusCode}: {Message}", ex.GetType().Name, statusCode, ex.Message);

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = _env.IsDevelopment()
                    ? $"[{ex.GetType().Name}] {ex.Message}"
                    : "An error occurred. Please try again or contact support.",
                Instance = context.Request.Path,
            };

            // Only expose stack trace in Development
            if (_env.IsDevelopment() && ex.StackTrace is not null)
            {
                problem.Extensions["stackTrace"] = ex.StackTrace;
                problem.Extensions["exceptionType"] = ex.GetType().FullName;
            }

            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
