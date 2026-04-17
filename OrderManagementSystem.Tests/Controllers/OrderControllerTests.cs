using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using OrderManagementSystem.API.Controllers;
using OrderManagementSystem.API.DTOs;
using OrderManagementSystem.API.Services;

namespace OrderManagementSystem.Tests.Controllers
{
    public class OrderControllerTests
    {
        private readonly Mock<ILogger<OrdersController>> _loggerMock;
        private readonly Mock<IOrderService> _orderServiceMock;
        private readonly OrdersController _controller;

        public OrderControllerTests()
        {
            _loggerMock = new Mock<ILogger<OrdersController>>();
            _orderServiceMock = new Mock<IOrderService>();
            _controller = new OrdersController(_loggerMock.Object, _orderServiceMock.Object);
        }

        [Fact]
        public async Task CreateOrder_ValidRequest_ReturnsCreatedResult()
        {
            // Arrange
            var createOrderDto = new CreateOrderDTO
            {
                CustomerId = 1,
                Items = new List<CreateOrderItemDTO>
                {
                    new CreateOrderItemDTO { ProductId = 1, Quantity = 2 },
                    new CreateOrderItemDTO { ProductId = 2, Quantity = 1 }
                }
            };

            var createdOrder = new OrderDTO
            {
                Id = 1,
                CustomerId = createOrderDto.CustomerId,
                Items = createOrderDto.Items.Select(i => new OrderItemDTO
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity
                }).ToList(),
                TotalAmount = 100.00m,
                OrderDate = DateTime.UtcNow
            };

            _orderServiceMock.Setup(s => s.CreateOrderAsync(createOrderDto))
                .ReturnsAsync(createdOrder);

            // Act
            var result = await _controller.CreateOrder(createOrderDto);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(nameof(OrdersController.GetOrderById), createdAtActionResult.ActionName);
            Assert.Equal(createdOrder.Id, ((OrderDTO)createdAtActionResult.Value!).Id);
        }

        [Fact]
        public async Task CreateOrder_InvalidModel_ReturnsBadRequest()
        {
            // Arrange
            var createOrderDto = new CreateOrderDTO
            {
                CustomerId = 1,
                Items = new List<CreateOrderItemDTO>() // Invalid: no items
            };

            _controller.ModelState.AddModelError("CustomerId", "CustomerId must be greater than 0.");
            _controller.ModelState.AddModelError("Items", "At least one item is required.");

            // Act
            var result = await _controller.CreateOrder(createOrderDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.IsType<SerializableError>(badRequestResult.Value);
        }

        [Fact]
        public async Task GetOrderById_ExistingOrder_ReturnsOkResult()
        {
            // Arrange
            var orderId = 1;
            var order = new OrderDTO
            {
                Id = orderId,
                CustomerId = 1,
                Items = new List<OrderItemDTO>
                {
                    new OrderItemDTO { ProductId = 1, Quantity = 2 },
                    new OrderItemDTO { ProductId = 2, Quantity = 1 }
                },
                TotalAmount = 100.00m,
                OrderDate = DateTime.UtcNow
            };

            _orderServiceMock.Setup(s => s.GetOrderByIdAsync(orderId))
                .ReturnsAsync(order);

            // Act
            var result = await _controller.GetOrderById(orderId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(order.Id, ((OrderDTO)okResult.Value!).Id);
        }
    }
}