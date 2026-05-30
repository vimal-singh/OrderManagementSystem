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
                Price = dbProduct.Price,
                StockQuantity = dbProduct.StockQuantity,
                Category = dbProduct.Category,
                IsActive = dbProduct.IsActive
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
            try
            {
                var cachedProducts = await _cache.GetStringAsync(cacheKey);

                if (!string.IsNullOrEmpty(cachedProducts))
                {
                    return JsonSerializer.Deserialize<List<ProductDTO>>(cachedProducts)
                           ?? new List<ProductDTO>();
                }
            }
            catch
            {
                // Redis/cache failure → ignore and continue
            }
            
            var dbProducts = await _repository.GetAllProductsAsync();
            var products = dbProducts.Select(p => new ProductDTO
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                Category = p.Category,
                IsActive = p.IsActive
            }).ToList();
            
            // Cache result (even if empty)
            try
            {
                var cacheOptions = new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                };

                await _cache.SetStringAsync(
                    cacheKey,
                    JsonSerializer.Serialize(products),
                    cacheOptions
                );
            }
            catch
            {
                // Cache write failure → ignore
            }
            return products;
        }

        public async Task<ProductDTO> CreateProductAsync(CreateProductDTO productDto)
        {
            var product = new Product
            {
                Name = productDto.Name,
                Price = productDto.Price,
                StockQuantity = productDto.StockQuantity,
                Category = productDto.Category,
                IsActive = true
            };
            
            await _repository.AddProductAsync(product);

            // Invalidate cache for all products (safe failure)
            try
            {
                await _cache.RemoveAsync("all_products");
            }
            catch
            {
                // Cache failure → ignore
            }

            return new ProductDTO
            {
                Id = product.Id,
                Name = product.Name,
                Price = product.Price,
                StockQuantity = product.StockQuantity,
                Category = product.Category,
                IsActive = product.IsActive
            };
        }

        public async Task<ProductDTO> UpdateProductAsync(int id, CreateProductDTO productDto)
        {
            var product = new Product
            {
                Id = id,
                Name = productDto.Name,
                Price = productDto.Price,
                StockQuantity = productDto.StockQuantity,
                Category = productDto.Category,
                IsActive = true
            };

            var updatedProduct = await _repository.UpdateProductAsync(product);

            // Invalidate caches (safe failure)
            try
            {
                await _cache.RemoveAsync("all_products");
                await _cache.RemoveAsync($"product_{id}");
            }
            catch
            {
                // Cache failure → ignore
            }

            return new ProductDTO
            {
                Id = updatedProduct.Id,
                Name = updatedProduct.Name,
                Price = updatedProduct.Price,
                StockQuantity = updatedProduct.StockQuantity,
                Category = updatedProduct.Category,
                IsActive = updatedProduct.IsActive
            };
        }

        public async Task<bool> DeleteProductAsync(int id)
        {
            try
            {
                await _repository.DeleteProductAsync(id);

                // Invalidate caches (safe failure)
                try
                {
                    await _cache.RemoveAsync("all_products");
                    await _cache.RemoveAsync($"product_{id}");
                }
                catch
                {
                    // Cache failure → ignore
                }

                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}