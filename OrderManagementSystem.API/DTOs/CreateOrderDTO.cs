using System.ComponentModel.DataAnnotations;

namespace OrderManagementSystem.API.DTOs
{
    public class CreateOrderDTO
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "CustomerId must be a positive number.")]
        public int CustomerId { get; set; }

        [Required(ErrorMessage = "At least one item is required.")]
        public List<CreateOrderItemDTO> Items { get; set; } = new();
    }
}