using System.Net;
using System.Text.Json;
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
                _logger.LogError(ex, "Unhandled exception occurred");

                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";

                object response;
                if (_env.IsDevelopment())
                {
                    response = new
                    {
                        message = ex.Message,
                        innerException = ex.InnerException?.Message,
                        stackTrace = ex.StackTrace
                    };
                }
                else
                {
                    response = new
                    {
                        message = "An unexpected error occurred. Please try again later."
                    };
                }

                await context.Response.WriteAsJsonAsync(response);
            }
        }
    }
}

