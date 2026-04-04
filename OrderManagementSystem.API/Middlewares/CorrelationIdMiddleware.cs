using Serilog.Context;

namespace OrderManagementSystem.API.Middlewares
{
    // Middleware that ensures every incoming request has a unique Correlation ID.
    // This ID is used to trace a request end-to-end across multiple layer
    public class CorrelationIdMiddleware
    {
        // The next middleware in the request pipeline.
        private readonly RequestDelegate _next;

        // The HTTP header name used to carry the correlation ID.
        private const string CorrelationIdHeader = "X-Correlation-ID";

        // Constructor injects the next delegate in the pipeline.
        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        // Invoked once per HTTP request. 
        // Responsible for generating a Correlation ID and injecting it into:
        // 1. The request header (for downstream services)
        // 2. The response header (for clients)
        public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
        {
            // Generate a new correlation ID for each request
            var correlationId = Guid.NewGuid().ToString().ToUpper();

            // Store in HttpContext for downstream code
            context.Items[CorrelationIdHeader] = correlationId;

            // Ensure the same correlation ID is returned to the client
            // in the response header
            context.Response.Headers[CorrelationIdHeader] = correlationId;

            // Push CorrelationId into Serilog's log context for the entire request
            using (LogContext.PushProperty("CorrelationId", correlationId))
            {
                await _next(context);
            }
        }
    }

    // Extension method for registering the CorrelationIdMiddleware
    // in a fluent and readable way inside Program.cs (app.UseCorrelationId()).
    public static class CorrelationIdMiddlewareExtensions
    {
        // Adds the CorrelationIdMiddleware to the ASP.NET Core request pipeline.
        public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<CorrelationIdMiddleware>();
        }
    }
}