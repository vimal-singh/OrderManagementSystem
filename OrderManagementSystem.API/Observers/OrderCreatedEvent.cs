namespace OrderManagementSystem.API.Observers
{
    /// <summary>
    /// Immutable event payload published when an order is successfully created.
    /// Serialized to JSON for Kafka transport.
    /// </summary>
    public record OrderCreatedEvent
    {
        public int OrderId { get; init; }
        public int CustomerId { get; init; }
        public string CustomerName { get; init; } = string.Empty;
        public decimal TotalAmount { get; init; }
        public DateTime OrderDate { get; init; }
        public int ItemCount { get; init; }
        public DateTime PublishedAt { get; init; } = DateTime.UtcNow;
    }
}
