namespace OrderManagementSystem.API.Services
{
    public class CorrelationIdAccessor(IHttpContextAccessor httpContextAccessor) : ICorrelationIdAccessor
    {
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

        // Key used by the CorrelationId middleware to store the value in HttpContext.Items.
        private const string CorrelationIdItemKey = "X-Correlation-ID";

        public string? GetCorrelationId()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                return null;

            // Try to read the CorrelationId set by middleware.
            if (httpContext.Items.TryGetValue(CorrelationIdItemKey, out var value) &&
                value is string cidFromItems)
            {
                return cidFromItems;
            }

            // Fallback: use the ASP.NET Core generated TraceIdentifier.
            return httpContext.TraceIdentifier;
        }
    }
}