using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using OrderManagementSystem.API.Data;
using OrderManagementSystem.API.DTOs;
using OrderManagementSystem.API.Entities;
using OrderManagementSystem.API.Observers;
using OrderManagementSystem.API.Services;
using Xunit;

namespace OrderManagementSystem.Tests.Observers
{
    public class OrderEventNotifierTests
    {
        private readonly OrderManagementDbContext _dbContext;
        private readonly Mock<IOrderEventProducer> _producerMock;
        private readonly Mock<ILogger<OrderService>> _loggerMock;
        private readonly OrderService _orderService;

        public OrderEventNotifierTests()
        {
            // Setup In-Memory database for testing OrderService logic
            var options = new DbContextOptionsBuilder<OrderManagementDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _dbContext = new OrderManagementDbContext(options);
            _producerMock = new Mock<IOrderEventProducer>();
            _loggerMock = new Mock<ILogger<OrderService>>();

            _orderService = new OrderService(_dbContext, _producerMock.Object, _loggerMock.Object);

            // Seed reference data (Customer & Product)
            SeedData();
        }

        private void SeedData()
        {
            _dbContext.Customers.Add(new Customer { Id = 1, FullName = "John Doe", Email = "john@example.com" });
            _dbContext.Products.Add(new Product { Id = 1, Name = "Test Product 1", Price = 10.0m, StockQuantity = 50, IsActive = true });
            _dbContext.Products.Add(new Product { Id = 2, Name = "Test Product 2", Price = 20.0m, StockQuantity = 0, IsActive = true }); // Out of stock
            _dbContext.Products.Add(new Product { Id = 3, Name = "Inactive Product", Price = 15.0m, StockQuantity = 10, IsActive = false });
            _dbContext.SaveChanges();
        }

        [Fact]
        public async Task CreateOrderAsync_Success_PublishesOrderCreatedEvent()
        {
            // Arrange
            var createOrderDto = new CreateOrderDTO
            {
                CustomerId = 1,
                Items = new List<CreateOrderItemDTO>
                {
                    new() { ProductId = 1, Quantity = 2 }
                }
            };

            // Act
            var result = await _orderService.CreateOrderAsync(createOrderDto);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Id > 0);

            // Verify that the producer was invoked exactly once with appropriate properties
            _producerMock.Verify(p => p.PublishOrderCreatedAsync(It.Is<OrderCreatedEvent>(e =>
                e.OrderId == result.Id &&
                e.CustomerId == 1 &&
                e.CustomerName == "John Doe" &&
                e.TotalAmount == 20.0m &&
                e.ItemCount == 1
            )), Times.Once);
        }

        [Fact]
        public async Task CreateOrderAsync_ValidationError_DoesNotPublishEvent()
        {
            // Arrange (trying to buy an out-of-stock product)
            var createOrderDto = new CreateOrderDTO
            {
                CustomerId = 1,
                Items = new List<CreateOrderItemDTO>
                {
                    new() { ProductId = 2, Quantity = 5 } // out of stock
                }
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _orderService.CreateOrderAsync(createOrderDto));

            // Verify event was NOT published
            _producerMock.Verify(p => p.PublishOrderCreatedAsync(It.IsAny<OrderCreatedEvent>()), Times.Never);
        }

        [Fact]
        public async Task CreateOrderAsync_PublishFails_DoesNotRollbackOrCrash()
        {
            // Arrange
            var createOrderDto = new CreateOrderDTO
            {
                CustomerId = 1,
                Items = new List<CreateOrderItemDTO>
                {
                    new() { ProductId = 1, Quantity = 1 }
                }
            };

            // Force the event producer to throw an error
            _producerMock
                .Setup(p => p.PublishOrderCreatedAsync(It.IsAny<OrderCreatedEvent>()))
                .ThrowsAsync(new Exception("Kafka broke"));

            // Act
            var result = await _orderService.CreateOrderAsync(createOrderDto);

            // Assert - The order is successfully created in DB and returned
            Assert.NotNull(result);
            Assert.True(result.Id > 0);

            // Verify the log was written for the error
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to publish OrderCreatedEvent")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consumer_DispatchesToAllObservers_AndHandlesFailuresGracefully()
        {
            // Arrange
            var observer1Mock = new Mock<IOrderObserver>();
            var observer2Mock = new Mock<IOrderObserver>();

            // Setup observer 1 to fail, and observer 2 to succeed
            observer1Mock
                .Setup(o => o.OnOrderCreatedAsync(It.IsAny<OrderCreatedEvent>()))
                .ThrowsAsync(new Exception("Observer 1 failed"));

            observer2Mock
                .Setup(o => o.OnOrderCreatedAsync(It.IsAny<OrderCreatedEvent>()))
                .Returns(Task.CompletedTask);

            // Setup IServiceScope and IServiceScopeFactory for DI resolution
            var serviceProviderMock = new Mock<IServiceProvider>();
            serviceProviderMock
                .Setup(sp => sp.GetService(typeof(IEnumerable<IOrderObserver>)))
                .Returns(new List<IOrderObserver> { observer1Mock.Object, observer2Mock.Object });

            var serviceScopeMock = new Mock<IServiceScope>();
            serviceScopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);

            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            scopeFactoryMock
                .Setup(f => f.CreateScope())
                .Returns(serviceScopeMock.Object);

            var configurationMock = new Mock<IConfiguration>();
            configurationMock.Setup(c => c["Kafka:BootstrapServers"]).Returns("localhost:9092");
            configurationMock.Setup(c => c["Kafka:Topic"]).Returns("order-created-events");
            configurationMock.Setup(c => c["Kafka:GroupId"]).Returns("order-notification-consumers");

            var consumerLoggerMock = new Mock<ILogger<KafkaOrderEventConsumer>>();

            var consumer = new KafkaOrderEventConsumer(
                configurationMock.Object,
                scopeFactoryMock.Object,
                consumerLoggerMock.Object);

            // Since KafkaOrderEventConsumer.DispatchToObserversAsync is private, we can use reflection 
            // or verify behavior through public invocation. However, to directly test our isolation 
            // and dispatch logic cleanly, let's call the private DispatchToObserversAsync method.
            var methodInfo = typeof(KafkaOrderEventConsumer)
                .GetMethod("DispatchToObserversAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.NotNull(methodInfo);

            var orderEvent = new OrderCreatedEvent
            {
                OrderId = 99,
                CustomerId = 1,
                CustomerName = "John Doe",
                TotalAmount = 50.0m,
                OrderDate = DateTime.UtcNow,
                ItemCount = 2
            };

            var messageJson = JsonSerializer.Serialize(orderEvent, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            // Act
            var task = (Task)methodInfo.Invoke(consumer, new object[] { messageJson, CancellationToken.None })!;
            await task;

            // Assert
            // Both observers must be invoked
            observer1Mock.Verify(o => o.OnOrderCreatedAsync(It.Is<OrderCreatedEvent>(e => e.OrderId == 99)), Times.Once);
            observer2Mock.Verify(o => o.OnOrderCreatedAsync(It.Is<OrderCreatedEvent>(e => e.OrderId == 99)), Times.Once);

            // Log messages should indicate that observer 1 failed and observer 2 succeeded, but flow finished
            consumerLoggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("failed while processing OrderCreatedEvent")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task Consumer_HandlesMalformedJson_Gracefully()
        {
            // Arrange
            var scopeFactoryMock = new Mock<IServiceScopeFactory>();
            var configurationMock = new Mock<IConfiguration>();
            var consumerLoggerMock = new Mock<ILogger<KafkaOrderEventConsumer>>();

            var consumer = new KafkaOrderEventConsumer(
                configurationMock.Object,
                scopeFactoryMock.Object,
                consumerLoggerMock.Object);

            var methodInfo = typeof(KafkaOrderEventConsumer)
                .GetMethod("DispatchToObserversAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Assert.NotNull(methodInfo);

            var malformedJson = "{ invalid json }";

            // Act & Assert (Should not throw exception)
            var task = (Task)methodInfo.Invoke(consumer, new object[] { malformedJson, CancellationToken.None })!;
            await task;

            // Verify a deserialization warning/error is logged
            consumerLoggerMock.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to deserialize Kafka message")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);

            // Scope factory must never be accessed
            scopeFactoryMock.Verify(f => f.CreateScope(), Times.Never);
        }
    }
}
