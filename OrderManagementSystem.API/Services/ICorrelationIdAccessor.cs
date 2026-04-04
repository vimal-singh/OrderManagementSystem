namespace OrderManagementSystem.API.Services
{
    // Helper service to consistently read the CorrelationId for the current HTTP request.
    // This keeps CorrelationId logic in one place and can be reused by any logger or service.
    public interface ICorrelationIdAccessor
    {
        // Returns the current CorrelationId if available, or null if there is no active HttpContext.
        // Falls back to HttpContext.TraceIdentifier when a custom CorrelationId is not present.
        string? GetCorrelationId();
    }
}