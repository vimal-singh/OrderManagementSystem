using Microsoft.EntityFrameworkCore;
using OrderManagementSystem.API.Data;
using OrderManagementSystem.API.DTOs;
using OrderManagementSystem.API.Entities;

namespace OrderManagementSystem.API.Services
{
    public class OrderService : IOrderService
    {
        private readonly OrderManagementDbContext _dbContext;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            OrderManagementDbContext dbContext,
            ILogger<OrderService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<OrderDTO> CreateOrderAsync(CreateOrderDTO dto)
        {
            // Structured: CustomerId, ItemCount and Request are separate properties.
            _logger.LogInformation(
                "Creating order for CustomerId {CustomerId} with {ItemCount} items. Payload: {@Request}",
                dto.CustomerId,
                dto.Items?.Count ?? 0,
                dto);

            // 1. Validate Customer
            var customer = await _dbContext.Customers.FindAsync(dto.CustomerId);
            if (customer == null)
            {
                _logger.LogWarning(
                    "Cannot create order: CustomerId {CustomerId} not found.",
                    dto.CustomerId);

                throw new ArgumentException($"Customer with id {dto.CustomerId} not found.");
            }

            // Extra safety: ensure we actually have items
            if (dto.Items == null || dto.Items.Count == 0)
            {
                _logger.LogWarning(
                    "Cannot create order: no items provided for CustomerId {CustomerId}.",
                    dto.CustomerId);

                throw new ArgumentException("Order must contain at least one item.");
            }

            // 2. Get all product IDs from DTO and fetch from DB in one shot
            var productIds = dto.Items.Select(i => i.ProductId).Distinct().ToList();

            var products = await _dbContext.Products
                .Where(p => productIds.Contains(p.Id) && p.IsActive)
                .ToListAsync();

            if (products.Count != productIds.Count)
            {
                var missingIds = productIds.Except(products.Select(p => p.Id)).ToList();

                _logger.LogWarning(
                    "Cannot create order: some products not found or inactive. MissingIds: {MissingProductIds}",
                    missingIds);

                throw new ArgumentException("One or more products are invalid or not active.");
            }

            // 3. Create Order and OrderItems
            var order = new Order
            {
                CustomerId = dto.CustomerId,
                OrderDate = DateTime.UtcNow
            };

            decimal total = 0;

            foreach (var itemDto in dto.Items)
            {
                var product = products.Single(p => p.Id == itemDto.ProductId);

                var unitPrice = product.Price;
                var lineTotal = unitPrice * itemDto.Quantity;

                var orderItem = new OrderItem
                {
                    ProductId = product.Id,
                    Quantity = itemDto.Quantity,
                    UnitPrice = unitPrice,
                    LineTotal = lineTotal
                };

                order.Items.Add(orderItem);
                total += lineTotal;

                _logger.LogDebug(
                    "Added item to order. ProductId={ProductId}, Quantity={Quantity}, UnitPrice={UnitPrice}, LineTotal={LineTotal}.",
                    product.Id,
                    itemDto.Quantity,
                    unitPrice,
                    lineTotal);
            }

            order.TotalAmount = total;

            _logger.LogDebug(
                "Total amount for CustomerId {CustomerId} calculated as {TotalAmount}.",
                dto.CustomerId,
                order.TotalAmount);

            // 4. Save to DB with error logging
            try
            {
                _dbContext.Orders.Add(order);
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation(
                    "Order {OrderId} created successfully for CustomerId {CustomerId}.",
                    order.Id,
                    dto.CustomerId);

                // Load navigation properties for mapping (Customer + Items + Product)
                await _dbContext.Entry(order).Reference(o => o.Customer).LoadAsync();
                await _dbContext.Entry(order).Collection(o => o.Items).LoadAsync();
                foreach (var item in order.Items)
                {
                    await _dbContext.Entry(item).Reference(i => i.Product).LoadAsync();
                }

                var dtoResult = MapToOrderDto(order);

                // Log the resulting DTO as structured data
                _logger.LogInformation(
                    "Order {OrderId} data prepared for response. DTO: {@OrderDto}",
                    order.Id,
                    dtoResult);

                return dtoResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error occurred while saving order for CustomerId {CustomerId}.",
                    dto.CustomerId);

                throw; // let controller decide response
            }
        }

        public async Task<OrderDTO?> GetOrderByIdAsync(int id)
        {
            _logger.LogInformation(
                "Fetching order with OrderId {OrderId}.",
                id);

            var order = await _dbContext.Orders
                .Include(o => o.Customer)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .AsNoTracking()
                .SingleOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                _logger.LogWarning(
                    "Order with OrderId {OrderId} not found.",
                    id);

                return null;
            }

            _logger.LogDebug(
                "Order {OrderId} found for CustomerId {CustomerId}.",
                order.Id,
                order.CustomerId);

            var dto = MapToOrderDto(order);

            _logger.LogInformation(
                "Order {OrderId} mapped to DTO. DTO: {@OrderDto}",
                id,
                dto);

            return dto;
        }

        public async Task<IEnumerable<OrderDTO>> GetOrdersForCustomerAsync(int customerId)
        {
            _logger.LogInformation(
                "Fetching orders for CustomerId {CustomerId}.",
                customerId);

            var orders = await _dbContext.Orders
                .Include(o => o.Customer)
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .AsNoTracking()
                .Where(o => o.CustomerId == customerId)
                .ToListAsync();

            _logger.LogInformation(
                "Found {OrderCount} orders for CustomerId {CustomerId}.",
                orders.Count,
                customerId);

            var dtos = orders.Select(MapToOrderDto).ToList();

            _logger.LogDebug(
                "Orders for CustomerId {CustomerId} mapped to DTO list. DTOCount={DtoCount}.",
                customerId,
                dtos.Count);

            return dtos;
        }

        // Helper: Entity -> DTO mapping
        private static OrderDTO MapToOrderDto(Order order)
        {
            return new OrderDTO
            {
                Id = order.Id,
                CustomerId = order.CustomerId,
                CustomerName = order.Customer?.FullName ?? string.Empty,
                OrderDate = order.OrderDate,
                TotalAmount = order.TotalAmount,
                Status = order.Status,
                Items = order.Items.Select(i => new OrderItemDTO
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.Product?.Name ?? string.Empty,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    LineTotal = i.LineTotal
                }).ToList()
            };
        }
    }
}