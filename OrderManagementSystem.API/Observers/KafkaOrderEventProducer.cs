using System.Text.Json;
using Confluent.Kafka;

namespace OrderManagementSystem.API.Observers
{
    /// <summary>
    /// Kafka-backed implementation of <see cref="IOrderEventProducer"/>.
    /// Publishes <see cref="OrderCreatedEvent"/> as JSON messages to a Kafka topic.
    /// 
    /// Registered as a Singleton because Confluent.Kafka producers are thread-safe
    /// and designed for long-lived reuse (internal connection pooling + batching).
    /// </summary>
    public class KafkaOrderEventProducer : IOrderEventProducer, IDisposable
    {
        private readonly IProducer<string, string> _producer;
        private readonly string _topic;
        private readonly ILogger<KafkaOrderEventProducer> _logger;

        public KafkaOrderEventProducer(IConfiguration configuration, ILogger<KafkaOrderEventProducer> logger)
        {
            _logger = logger;

            var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
            _topic = configuration["Kafka:Topic"] ?? "order-created-events";

            var config = new ProducerConfig
            {
                BootstrapServers = bootstrapServers,
                // Ensure at-least-once delivery: wait for all in-sync replicas to acknowledge.
                Acks = Acks.All,
                // Enable idempotent producer to avoid duplicate messages on retries.
                EnableIdempotence = true,
                // Retry transient errors up to 3 times.
                MessageSendMaxRetries = 3,
                // Wait up to 1 second between retries.
                RetryBackoffMs = 1000
            };

            _producer = new ProducerBuilder<string, string>(config).Build();

            _logger.LogInformation(
                "KafkaOrderEventProducer initialized. BootstrapServers={BootstrapServers}, Topic={Topic}.",
                bootstrapServers,
                _topic);
        }

        public async Task PublishOrderCreatedAsync(OrderCreatedEvent orderEvent)
        {
            var key = orderEvent.OrderId.ToString();
            var value = JsonSerializer.Serialize(orderEvent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            try
            {
                var deliveryResult = await _producer.ProduceAsync(_topic, new Message<string, string>
                {
                    Key = key,
                    Value = value
                });

                _logger.LogInformation(
                    "Published OrderCreatedEvent to Kafka. OrderId={OrderId}, Topic={Topic}, Partition={Partition}, Offset={Offset}.",
                    orderEvent.OrderId,
                    deliveryResult.Topic,
                    deliveryResult.Partition.Value,
                    deliveryResult.Offset.Value);
            }
            catch (ProduceException<string, string> ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to publish OrderCreatedEvent to Kafka. OrderId={OrderId}, Topic={Topic}, ErrorReason={ErrorReason}.",
                    orderEvent.OrderId,
                    _topic,
                    ex.Error.Reason);

                // Re-throw so the caller (OrderService) can decide how to handle the failure.
                // The order is already committed to the DB; this is a notification failure only.
                throw;
            }
        }

        public void Dispose()
        {
            _logger.LogInformation("Disposing KafkaOrderEventProducer. Flushing pending messages...");
            _producer.Flush(TimeSpan.FromSeconds(10));
            _producer.Dispose();
        }
    }
}
