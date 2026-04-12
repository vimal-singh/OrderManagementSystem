using System.ComponentModel.DataAnnotations;

namespace OrderManagementSystem.Auth.DTOs
{
    public class LoginDTO
    {
        [Required(ErrorMessage = "Username is Required")]
        public string Username { get; set; } = null!;
        [Required(ErrorMessage = "Password is Required")]
        public string Password { get; set; } = null!;
    }
}