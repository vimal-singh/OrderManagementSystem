namespace OrderManagementSystem.API.Observers
{
    /// <summary>
    /// Observer interface for the Observer design pattern.
    /// Each notification channel (email, SMS, push, etc.) implements this interface
    /// to react to order lifecycle events.
    /// </summary>
    public interface IOrderObserver
    {
        /// <summary>
        /// Called when a new order has been created.
        /// </summary>
        /// <param name="orderEvent">The event payload containing order details.</param>
        Task OnOrderCreatedAsync(OrderCreatedEvent orderEvent);
    }
}
