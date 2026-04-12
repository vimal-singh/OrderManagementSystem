using System.ComponentModel.DataAnnotations;

namespace OrderManagementSystem.Auth.DTOs
{
    public class ValidateSSOTokenRequestDTO
    {
        [Required(ErrorMessage = "SSOToken is Required")]
        public string SSOToken { get; set; } = null!;
    }
}