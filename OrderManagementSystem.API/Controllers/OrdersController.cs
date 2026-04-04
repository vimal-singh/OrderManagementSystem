using Microsoft.AspNetCore.Mvc;
using OrderManagementSystem.API.DTOs;
using OrderManagementSystem.API.Services;
using System.Diagnostics;
using System.Text.Json;

namespace OrderManagementSystem.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly ILogger<OrdersController> _logger;
        private readonly IOrderService _orderService;

        public OrdersController(
            ILogger<OrdersController> logger,
            IOrderService orderService)
        {
            _logger = logger;
            _orderService = orderService;

            // Simple informational log – no extra properties needed here.
            _logger.LogInformation("OrdersController instantiated.");
        }

        // POST: api/orders
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDTO dto)
        {
            var stopwatch = Stopwatch.StartNew();

            // Structured logging of incoming request body
            _logger.LogInformation(
                "HTTP POST /api/orders called. Payload: {@Request}",
                dto);

            if (!ModelState.IsValid)
            {
                // Log ModelState as structured data (key-value pairs for validation errors)
                _logger.LogWarning(
                    "Model validation failed for CreateOrder request. ModelState: {@ModelState}",
                    ModelState);

                return BadRequest(ModelState);
            }

            try
            {
                var created = await _orderService.CreateOrderAsync(dto!);
                stopwatch.Stop();

                // Structured success log with separate fields for querying
                _logger.LogInformation(
                    "Order {OrderId} created successfully via API in {ElapsedMs} ms. Response: {@Response}",
                    created.Id,
                    stopwatch.ElapsedMilliseconds,
                    created);

                return CreatedAtAction(nameof(GetOrderById), new { id = created.Id }, created);
            }
            catch (ArgumentException ex)
            {
                stopwatch.Stop();

                // Structured validation error with exception attached
                _logger.LogWarning(
                    ex,
                    "Validation error while creating order for CustomerId {CustomerId}. ElapsedMs: {ElapsedMs} ms. ErrorMessage: {ErrorMessage}",
                    dto?.CustomerId,
                    stopwatch.ElapsedMilliseconds,
                    ex.Message);

                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                // Structured unexpected error with timing info
                _logger.LogError(
                    ex,
                    "Unexpected error while creating order for CustomerId {CustomerId}. ElapsedMs: {ElapsedMs} ms.",
                    dto?.CustomerId,
                    stopwatch.ElapsedMilliseconds);

                return StatusCode(500, new { message = "An unexpected error occurred." });
            }
        }

        // GET: api/orders/{id}
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetOrderById(int id)
        {
            var stopwatch = Stopwatch.StartNew();

            // Structured – OrderId is a separate property
            _logger.LogInformation(
                "HTTP GET /api/orders/{OrderId} called.",
                id);

            var order = await _orderService.GetOrderByIdAsync(id);
            stopwatch.Stop();

            if (order == null)
            {
                _logger.LogWarning(
                    "Order with ID {OrderId} not found. Execution time: {ElapsedMs} ms.",
                    id,
                    stopwatch.ElapsedMilliseconds);

                return NotFound(new { message = $"Order with id {id} not found." });
            }

            // Log returned order object as structured data
            _logger.LogInformation(
                "Order {OrderId} fetched successfully. Execution time: {ElapsedMs} ms. Response: {@Response}",
                id,
                stopwatch.ElapsedMilliseconds,
                order);

            return Ok(order);
        }

        // GET: api/orders/customer/{customerId}
        [HttpGet("customer/{customerId:int}")]
        public async Task<IActionResult> GetOrdersForCustomer(int customerId)
        {
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation(
                "HTTP GET /api/orders/customer/{CustomerId} called.",
                customerId);

            var orders = await _orderService.GetOrdersForCustomerAsync(customerId);
            stopwatch.Stop();

            // OrderCount / CustomerId / ElapsedMs come out as separate properties
            _logger.LogInformation(
                "{OrderCount} orders returned for CustomerId {CustomerId} in {ElapsedMs} ms.",
                orders.Count(),
                customerId,
                stopwatch.ElapsedMilliseconds);

            return Ok(orders);
        }
    }
}