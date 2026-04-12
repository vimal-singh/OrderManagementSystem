using System.ComponentModel.DataAnnotations;

namespace OrderManagementSystem.Auth.DTOs
{
    public class RegisterDTO
    {
        [Required(ErrorMessage = "Username is Required")]
        public string Username { get; set; } = null!;
        [EmailAddress(ErrorMessage = "Please provide a valid Email")]
        [Required(ErrorMessage = "Email is Required")]
        public string Email { get; set; } = null!;
        [Required(ErrorMessage = "Password is Required")]
        public string Password { get; set; } = null!;
    }
}