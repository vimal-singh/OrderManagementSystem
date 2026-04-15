using Microsoft.EntityFrameworkCore;
using OrderManagementSystem.API.Data;
using OrderManagementSystem.API.Entities;

namespace OrderManagementSystem.API.Repositories
{
    public class ProductRepository(OrderManagementDbContext dbContext) : IProductRepository
    {
        private readonly OrderManagementDbContext _dbContext = dbContext;

        public async Task<IEnumerable<Product>> GetAllProductsAsync()
        {
            var products = await _dbContext.Products
                .AsNoTracking()
                .ToListAsync();
            return products;
        }

        public async Task<Product?> GetByIdAsync(int id)
        {
            var product = await _dbContext.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id);
            return product;
        }

        public async Task<Product> AddProductAsync(Product product)
        {
            _dbContext.Products.Add(product);
            await _dbContext.SaveChangesAsync();
            return product;
        }

        public async Task<Product> DeleteProductAsync(int id)
        {
            var product = await GetByIdAsync(id);
            if (product == null)
            {
                throw new InvalidOperationException("Product not found");
            }

            _dbContext.Products.Remove(product);
            await _dbContext.SaveChangesAsync();
            return product;
        }

        public async Task<Product> UpdateProductAsync(Product product)
        {
            var existingProduct = await GetByIdAsync(product.Id) ?? throw new InvalidOperationException("Product not found");
            _dbContext.Entry(existingProduct).CurrentValues.SetValues(product);
            await _dbContext.SaveChangesAsync();
            return existingProduct;
        }
    }
}