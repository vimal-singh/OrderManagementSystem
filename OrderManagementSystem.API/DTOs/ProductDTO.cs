namespace OrderManagementSystem.API.DTOs
{
    public class ProductDTO
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
        public int StockQuantity { get; set; }
        public string Category { get; set; } = string.Empty;
    }
}