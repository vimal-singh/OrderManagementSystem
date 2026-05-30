using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderManagementSystem.API.DTOs;
using OrderManagementSystem.API.Repositories;

namespace OrderManagementSystem.API.Services
{
    public class ProductCacheRefresherWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IDistributedCache _cache;
        private readonly ILogger<ProductCacheRefresherWorker> _logger;
        private const string CacheKey = "all_products";
        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(8);

        public ProductCacheRefresherWorker(
            IServiceProvider serviceProvider,
            IDistributedCache cache,
            ILogger<ProductCacheRefresherWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _cache = cache;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Product Cache Refresher Worker starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Refreshing product cache in background...");

                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var repository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
                        var dbProducts = await repository.GetAllProductsAsync();

                        var products = dbProducts.Select(p => new ProductDTO
                        {
                            Id = p.Id,
                            Name = p.Name,
                            Price = p.Price,
                            StockQuantity = p.StockQuantity,
                            Category = p.Category,
                            IsActive = p.IsActive
                        }).ToList();

                        var cacheOptions = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                        };

                        await _cache.SetStringAsync(
                            CacheKey,
                            JsonSerializer.Serialize(products),
                            cacheOptions,
                            stoppingToken
                        );

                        _logger.LogInformation("Product cache refreshed successfully with {Count} products.", products.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while refreshing product cache.");
                }

                // Wait for the next refresh interval
                try
                {
                    await Task.Delay(RefreshInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Triggered when background worker is stopping
                    break;
                }
            }

            _logger.LogInformation("Product Cache Refresher Worker stopping.");
        }
    }
}
