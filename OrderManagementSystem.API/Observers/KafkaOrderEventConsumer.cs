using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;

namespace OrderManagementSystem.API.Observers
{
    /// <summary>
    /// Background service that consumes <see cref="OrderCreatedEvent"/> messages from a Kafka topic
    /// and dispatches them to all registered <see cref="IOrderObserver"/> implementations.
    /// 
    /// This is the "Subject → Observer dispatch" bridge in the Observer pattern,
    /// with Kafka providing the durable, decoupled transport layer.
    /// 
    /// Uses IServiceScopeFactory to resolve scoped observers per consumed message,
    /// ensuring proper DI lifetime management.
    /// </summary>
    public class KafkaOrderEventConsumer : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<KafkaOrderEventConsumer> _logger;
        private readonly string _topic;
        private readonly ConsumerConfig _consumerConfig;

        public KafkaOrderEventConsumer(
            IConfiguration configuration,
            IServiceScopeFactory scopeFactory,
            ILogger<KafkaOrderEventConsumer> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;

            var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
            _topic = configuration["Kafka:Topic"] ?? "order-created-events";
            var groupId = configuration["Kafka:GroupId"] ?? "order-notification-consumers";

            _consumerConfig = new ConsumerConfig
            {
                BootstrapServers = bootstrapServers,
                GroupId = groupId,
                // Start reading from the earliest unread offset when no committed offset exists.
                AutoOffsetReset = AutoOffsetReset.Earliest,
                // Manual commit after all observers have processed — avoids message loss.
                EnableAutoCommit = false,
                // Heartbeat and session timeouts for consumer group rebalancing.
                SessionTimeoutMs = 30000,
                HeartbeatIntervalMs = 10000
            };

            _logger.LogInformation(
                "KafkaOrderEventConsumer configured. BootstrapServers={BootstrapServers}, Topic={Topic}, GroupId={GroupId}.",
                bootstrapServers,
                _topic,
                groupId);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Yield to let the host continue startup before entering the consume loop.
            await Task.Yield();

            // Ensure the topic exists first before subscribing
            await EnsureTopicExistsAsync();

            _logger.LogInformation("KafkaOrderEventConsumer starting consume loop on topic {Topic}.", _topic);

            using var consumer = new ConsumerBuilder<string, string>(_consumerConfig).Build();
            consumer.Subscribe(_topic);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        // Block up to 100ms waiting for a message, then loop to check cancellation.
                        var consumeResult = consumer.Consume(TimeSpan.FromMilliseconds(100));

                        if (consumeResult == null)
                            continue;

                        _logger.LogInformation(
                            "Consumed message from Kafka. Topic={Topic}, Partition={Partition}, Offset={Offset}, Key={Key}.",
                            consumeResult.Topic,
                            consumeResult.Partition.Value,
                            consumeResult.Offset.Value,
                            consumeResult.Message.Key);

                        await DispatchToObserversAsync(consumeResult.Message.Value, stoppingToken);

                        // Commit offset only after all observers have been invoked.
                        consumer.Commit(consumeResult);

                        _logger.LogDebug(
                            "Committed offset {Offset} for partition {Partition}.",
                            consumeResult.Offset.Value,
                            consumeResult.Partition.Value);
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(
                            ex,
                            "Kafka consume error. Reason={Reason}. Continuing consume loop.",
                            ex.Error.Reason);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("KafkaOrderEventConsumer shutting down gracefully.");
            }
            finally
            {
                consumer.Close();
                _logger.LogInformation("KafkaOrderEventConsumer closed consumer connection.");
            }
        }

        /// <summary>
        /// Deserializes the Kafka message and dispatches the event to all registered observers.
        /// Each observer is invoked inside its own try/catch to ensure a failing observer
        /// does not prevent the remaining observers from executing.
        /// </summary>
        private async Task DispatchToObserversAsync(string messageValue, CancellationToken cancellationToken)
        {
            OrderCreatedEvent? orderEvent;

            try
            {
                orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(messageValue, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });

                if (orderEvent == null)
                {
                    _logger.LogWarning("Deserialized OrderCreatedEvent is null. Skipping message. RawValue={RawValue}.", messageValue);
                    return;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to deserialize Kafka message to OrderCreatedEvent. Skipping malformed message. RawValue={RawValue}.",
                    messageValue);
                return;
            }

            _logger.LogInformation(
                "Dispatching OrderCreatedEvent to observers. OrderId={OrderId}, CustomerId={CustomerId}.",
                orderEvent.OrderId,
                orderEvent.CustomerId);

            // Create a DI scope per message to resolve scoped observers.
            using var scope = _scopeFactory.CreateScope();
            var observers = scope.ServiceProvider.GetServices<IOrderObserver>();

            var observerCount = 0;
            var failureCount = 0;

            foreach (var observer in observers)
            {
                observerCount++;
                var observerName = observer.GetType().Name;

                try
                {
                    await observer.OnOrderCreatedAsync(orderEvent);

                    _logger.LogInformation(
                        "Observer {ObserverName} processed OrderCreatedEvent successfully. OrderId={OrderId}.",
                        observerName,
                        orderEvent.OrderId);
                }
                catch (Exception ex)
                {
                    failureCount++;
                    _logger.LogError(
                        ex,
                        "Observer {ObserverName} failed while processing OrderCreatedEvent. OrderId={OrderId}. Error: {ErrorMessage}.",
                        observerName,
                        orderEvent.OrderId,
                        ex.Message);
                }
            }

            _logger.LogInformation(
                "Observer dispatch complete for OrderId={OrderId}. Total={Total}, Succeeded={Succeeded}, Failed={Failed}.",
                orderEvent.OrderId,
                observerCount,
                observerCount - failureCount,
                failureCount);
        }

        private async Task EnsureTopicExistsAsync()
        {
            var adminConfig = new AdminClientConfig
            {
                BootstrapServers = _consumerConfig.BootstrapServers
            };

            using var adminClient = new AdminClientBuilder(adminConfig).Build();
            try
            {
                _logger.LogInformation("Checking/creating Kafka topic '{Topic}'...", _topic);
                await adminClient.CreateTopicsAsync(new TopicSpecification[]
                {
                    new TopicSpecification
                    {
                        Name = _topic,
                        NumPartitions = 1,
                        ReplicationFactor = 1
                    }
                });
                _logger.LogInformation("Kafka topic '{Topic}' created successfully.", _topic);
            }
            catch (CreateTopicsException ex)
            {
                var error = ex.Results[0].Error;
                if (error.Code == ErrorCode.TopicAlreadyExists)
                {
                    _logger.LogInformation("Kafka topic '{Topic}' already exists.", _topic);
                }
                else
                {
                    _logger.LogWarning("Failed to create Kafka topic '{Topic}'. Error={Reason}.", _topic, error.Reason);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected error ensuring Kafka topic '{Topic}' exists.", _topic);
            }
        }
    }
}
