using System.ComponentModel.DataAnnotations;

namespace OrderManagementSystem.API.DTOs
{
    public class CreateProductDTO
    {
        [Required(ErrorMessage = "Product name is required.")]
        [MaxLength(100, ErrorMessage = "Product name cannot exceed 100 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Product price is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be a positive number.")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Product stock quantity is required.")]
        [Range(0, int.MaxValue, ErrorMessage = "Stock quantity must be a positive number.")]
        public int StockQuantity { get; set; }
        
        [Required(ErrorMessage = "Product category is required.")]
        [StringLength(100, ErrorMessage = "Category name cannot exceed 100 characters.")]
        public string Category { get; set; } = string.Empty;
    }
}