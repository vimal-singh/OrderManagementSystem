using OrderManagementSystem.API.DTOs;

namespace OrderManagementSystem.API.Services
{
    public interface IProductService
    {
        Task<IEnumerable<ProductDTO>> GetProductsAsync();
        Task<ProductDTO?> GetProductByIdAsync(int id);
    }
}