using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using OrderManagementSystem.API.DTOs;
using OrderManagementSystem.API.Entities;
using OrderManagementSystem.API.Repositories;
using OrderManagementSystem.API.Services;

namespace OrderManagementSystem.Tests.Service
{
    public class ProductServiceTests
    {
        private readonly Mock<IProductRepository> _repoMock;
        private readonly Mock<IDistributedCache> _cacheMock;
        private readonly ProductService _service;

        public ProductServiceTests()
        {
            _repoMock = new Mock<IProductRepository>();
            _cacheMock = new Mock<IDistributedCache>();

            _service = new ProductService(_repoMock.Object, _cacheMock.Object);
        }

        [Fact]
        public async Task GetProductByIdAsync_CacheHit_ReturnsProductFromCache()
        {
            // Arrange
            var product = new ProductDTO { Id = 1, Name = "Cached", Price = 100 };

            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(product));

            _cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), default))
                .ReturnsAsync(bytes);

            // Act
            var result = await _service.GetProductByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Cached", result.Name);

            _repoMock.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
        }

        [Fact]
        public async Task GetProductByIdAsync_CacheMiss_FetchesFromDatabase_AndCachesResult()
        {
            // Arrange
            _cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), default))
                .ReturnsAsync((byte[]?)null);

            var dbProduct = new Product { Id = 1, Name = "DB Product", Price = 200 };

            _repoMock.Setup(r => r.GetByIdAsync(1))
                .ReturnsAsync(dbProduct);

            // Act
            var result = await _service.GetProductByIdAsync(1);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("DB Product", result.Name);

            // ✅ Verify cache set
            _cacheMock.Verify(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                default), Times.Once);
        }

        [Fact]
        public async Task GetProductByIdAsync_ProductNotFound_ReturnsNull()
        {
            // Arrange
            _cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), default))
                .ReturnsAsync((byte[]?)null);

            _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((Product?)null);

            // Act
            var result = await _service.GetProductByIdAsync(1);

            // Assert
            Assert.Null(result);

            // 🔥 Ensure cache NOT set
            _cacheMock.Verify(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                default), Times.Never);
        }

        [Fact]
        public async Task CreateProductAsync_ValidProduct_SavesToDbAndInvalidatesCache()
        {
            // Arrange
            var createDto = new CreateProductDTO { Name = "New", Price = 10, StockQuantity = 5, Category = "Test" };
            var product = new Product { Id = 1, Name = "New", Price = 10, StockQuantity = 5, Category = "Test", IsActive = true };

            _repoMock.Setup(r => r.AddProductAsync(It.IsAny<Product>()))
                .ReturnsAsync(product);

            // Act
            var result = await _service.CreateProductAsync(createDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("New", result.Name);
            Assert.Equal(5, result.StockQuantity);

            // Verify DB save
            _repoMock.Verify(r => r.AddProductAsync(It.Is<Product>(p => p.Name == "New")), Times.Once);

            // Verify cache invalidated
            _cacheMock.Verify(c => c.RemoveAsync("all_products", default), Times.Once);
        }

        [Fact]
        public async Task UpdateProductAsync_ExistingProduct_UpdatesDbAndInvalidatesCache()
        {
            // Arrange
            var createDto = new CreateProductDTO { Name = "Updated", Price = 15, StockQuantity = 20, Category = "Test" };
            var product = new Product { Id = 1, Name = "Updated", Price = 15, StockQuantity = 20, Category = "Test", IsActive = true };

            _repoMock.Setup(r => r.UpdateProductAsync(It.IsAny<Product>()))
                .ReturnsAsync(product);

            // Act
            var result = await _service.UpdateProductAsync(1, createDto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Updated", result.Name);
            Assert.Equal(20, result.StockQuantity);

            // Verify DB update
            _repoMock.Verify(r => r.UpdateProductAsync(It.Is<Product>(p => p.Id == 1 && p.Name == "Updated")), Times.Once);

            // Verify cache entries invalidated
            _cacheMock.Verify(c => c.RemoveAsync("all_products", default), Times.Once);
            _cacheMock.Verify(c => c.RemoveAsync("product_1", default), Times.Once);
        }

        [Fact]
        public async Task DeleteProductAsync_ExistingProduct_RemovesFromDbAndInvalidatesCache()
        {
            // Arrange
            _repoMock.Setup(r => r.DeleteProductAsync(1))
                .ReturnsAsync(new Product { Id = 1, Name = "Deleted" });

            // Act
            var result = await _service.DeleteProductAsync(1);

            // Assert
            Assert.True(result);

            // Verify DB delete
            _repoMock.Verify(r => r.DeleteProductAsync(1), Times.Once);

            // Verify cache entries invalidated
            _cacheMock.Verify(c => c.RemoveAsync("all_products", default), Times.Once);
            _cacheMock.Verify(c => c.RemoveAsync("product_1", default), Times.Once);
        }
    }
}