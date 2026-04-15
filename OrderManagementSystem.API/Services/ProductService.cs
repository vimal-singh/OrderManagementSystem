using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using OrderManagementSystem.API.Data;
using OrderManagementSystem.API.DTOs;
using OrderManagementSystem.API.Entities;
using OrderManagementSystem.API.Repositories;

namespace OrderManagementSystem.API.Services
{
    public class ProductService(IProductRepository repository, IDistributedCache cache) : IProductService
    {
        private readonly IProductRepository _repository = repository;
        private readonly IDistributedCache _cache = cache;

        public async Task<ProductDTO> CreateProductAsync(CreateProductDTO productDto)
        {
            var product = new Product
            {
                Name = productDto.Name,
                Price = productDto.Price,
                StockQuantity = productDto.StockQuantity,
                Category = productDto.Category,

            };
            // invalidate cache for all products
            await _cache.RemoveAsync("all_products");
            
            await _repository.AddProductAsync(product);

            return new ProductDTO
            {
                Id = product.Id,
                Name = product.Name,
                Price = product.Price,
                StockQuantity = product.StockQuantity,
                Category = product.Category
            };
        }

        public async Task<ProductDTO?> GetProductByIdAsync(int id)
        {
            var cacheKey = $"product_{id}";

            // Try cache (safe failure)
            try
            {
                var cachedProduct = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedProduct))
                {
                    return JsonSerializer.Deserialize<ProductDTO>(cachedProduct);
                }
            }
            catch
            {
                // Redis/cache failure → ignore and continue
            }

            // Fetch from DB
            var dbProduct = await _repository.GetByIdAsync(id);

            if (dbProduct == null)
            {
                return null;
            }

            var product = new ProductDTO
            {
                Id = dbProduct.Id,
                Name = dbProduct.Name,
                Price = dbProduct.Price
            };

            // Store in cache (safe failure)
            try
            {
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                };

                await _cache.SetStringAsync(
                    cacheKey,
                    JsonSerializer.Serialize(product),
                    cacheOptions
                );
            }
            catch
            {
                // Cache write failure → ignore
            }

            return product;
        }

        public async Task<IEnumerable<ProductDTO>> GetProductsAsync()
        {
            var cacheKey = "all_products";

            // Try cache
            var cachedProducts = await _cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedProducts))
            {
                return JsonSerializer.Deserialize<List<ProductDTO>>(cachedProducts)
                       ?? new List<ProductDTO>();
            }
            
            var dbProducts = await _repository.GetAllProductsAsync();
            var products = dbProducts.Select(p => new ProductDTO
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price
            }).ToList();
            
            // Cache result (even if empty)
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            };

            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(products),
                cacheOptions
            );
            return products;
        }
    }
}