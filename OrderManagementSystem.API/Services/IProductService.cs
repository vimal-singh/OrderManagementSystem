using OrderManagementSystem.API.DTOs;

namespace OrderManagementSystem.API.Services
{
    public interface IProductService
    {
        Task<IEnumerable<ProductDTO>> GetProductsAsync();
        Task<ProductDTO?> GetProductByIdAsync(int id);
        Task<ProductDTO> CreateProductAsync(CreateProductDTO productDto);
        Task<ProductDTO> UpdateProductAsync(int id, CreateProductDTO productDto);
        Task<bool> DeleteProductAsync(int id);
    }
}