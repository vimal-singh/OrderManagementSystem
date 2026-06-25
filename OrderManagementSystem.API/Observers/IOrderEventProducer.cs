namespace OrderManagementSystem.API.Observers
{
    /// <summary>
    /// Abstraction for publishing order events to a message broker.
    /// Decouples the OrderService from the concrete Kafka implementation,
    /// enabling testability and broker-agnostic code.
    /// </summary>
    public interface IOrderEventProducer
    {
        /// <summary>
        /// Publishes an order-created event to the message broker.
        /// </summary>
        /// <param name="orderEvent">The event payload to publish.</param>
        Task PublishOrderCreatedAsync(OrderCreatedEvent orderEvent);
    }
}
