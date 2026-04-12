using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OrderManagementSystem.Auth.Data;
using OrderManagementSystem.Auth.DTOs;
using OrderManagementSystem.Auth.Models;

namespace OrderManagementSystem.Auth.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthenticationController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        // Constructor to inject the required services:
        // UserManager, ApplicationDbContext, IConfiguration
        public AuthenticationController(
            UserManager<IdentityUser> userManager,
            ApplicationDbContext context,
            IConfiguration configuration)	
        {
            _userManager = userManager;
            _context = context;
            _configuration = configuration;
        }
        // POST: api/Authentication/Register
        [HttpPost("Register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterDTO dto)
        {
            // Create a new IdentityUser based on the incoming model data
            var user = new IdentityUser
            {
                UserName = dto.Username,
                Email = dto.Email
            };
            // Create the user in the identity system
            var result = await _userManager.CreateAsync(user, dto.Password);
            // If creation succeeded, return a success message
            if (result.Succeeded)
            {	
                return Ok(new { Result = "User Registered Successfully" });
            }
            // If creation failed, return the validation errors
            return BadRequest(result.Errors);
        }
        // POST: api/Authentication/login
        [HttpPost("Login")]
        [AllowAnonymous]
        public async Task<ActionResult<LoginResponseDTO>> Login([FromBody] LoginDTO dto)
        {
            // Find the user based on the username
            var user = await _userManager.FindByNameAsync(dto.Username);
            // Validate the user's credentials
            if (user != null && await _userManager.CheckPasswordAsync(user, dto.Password))
            {
                // Generate the JWT token and return it
                var token = GenerateJwtToken(user);
                LoginResponseDTO loginResponseDTO = new LoginResponseDTO()	
                {
                    Token = token,
                };
                return Ok(loginResponseDTO);
            }
            // If authentication fails, return an unauthorized response
            return Unauthorized("Invalid username or password");
        }
        // POST: api/Authentication/GenerateSSOToken
        [HttpPost("GenerateSSOToken")]
        [Authorize] // Ensures that the user is authenticated
        public async Task<ActionResult<SSOTokenResponseDTO>> GenerateSSOToken()
        {
            try
            {
                // Get the UserId from the JWT Token Claims
                var UserId = User.FindFirstValue("User_Id");
                if (UserId == null)
                {
                    return NotFound("Invalid token");	

                }
                // Fetch the user from the database using the UserId
                var user = await _userManager.FindByIdAsync(UserId);
                // Check if the user exists
                if (user == null)
                {
                    return NotFound("User not found");
                }
                // Create a new SSO token and add it to the database
                var ssoToken = new SSOToken
                {
                    UserId = user.Id,
                    Token = Guid.NewGuid().ToString(), // Generate a unique SSO token
                    ExpiryDate = DateTime.UtcNow.AddMinutes(10), // Set an expiration time for the token
                    IsUsed = false
                };
                // Add the token to the database
                _context.SSOTokens.Add(ssoToken);	
                await _context.SaveChangesAsync();
                //Prepare the Response
                SSOTokenResponseDTO ssoTokenResponseDTO = new SSOTokenResponseDTO()
                {
                    SSOToken = ssoToken.Token
                };
                // Return the newly created SSO token
                return Ok(ssoTokenResponseDTO);
            }
            catch (Exception ex)
            {
                // Handle any exceptions that occur and return a server error response
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
        // POST: api/Authentication/ValidateSSOToken
        [HttpPost("ValidateSSOToken")]
        [AllowAnonymous]
        public async Task<ActionResult<ValidateSSOTokenResponseDTO>> ValidateSSOToken([FromBody] ValidateSSOTokenRequestDTO request)	
        {
            try
            {
                // Fetch the SSO token from the database
                var ssoToken = await _context.SSOTokens.FirstOrDefaultAsync(t => t.Token == request.SSOToken);
                // Check if the token is valid (exists, not used, not expired)
                if (ssoToken == null || ssoToken.IsUsed || ssoToken.IsExpired)
                {
                    return BadRequest("Invalid or expired SSO token");
                }
                // Mark the token as used
                ssoToken.IsUsed = true;
                await _context.SaveChangesAsync();
                // Find the user associated with the SSO token
                var user = await _userManager.FindByIdAsync(ssoToken.UserId);
                if (user == null)
                {
                    return BadRequest("Invalid User");
                }	
                // Generate a new JWT
                var newJwtToken = GenerateJwtToken(user);
                // Return the new JWT token along with user details (e.g., username and email)
                ValidateSSOTokenResponseDTO validateSSOTokenResponseDTO = new ValidateSSOTokenResponseDTO()
                {
                    Token = newJwtToken,
                    UserDetails = new UserDetails()
                    {
                        UserId = user.Id,
                        Email = user.Email,
                        Username = user.UserName
                    }
                };
                return Ok(validateSSOTokenResponseDTO);
            }
            catch (Exception ex)
            {
                // Handle exceptions and return a server error response
                return StatusCode(500, $"Internal server error: {ex.Message}");	
            }
        }
        // Helper method to generate a JWT token for the authenticated user
        private string GenerateJwtToken(IdentityUser user)
        {
            // Defines a set of claims to be included in the token.
            var claims = new List<Claim>
            {
                // Custom claim using the user's ID.
                new Claim("User_Id", user.Id.ToString()),
                
                // Standard claim for user identifier, using username.
                new Claim(ClaimTypes.NameIdentifier, user.UserName ?? string.Empty),
                
                // Standard claim for user's email.
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                
                // Standard JWT claim for subject, using user ID.
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString())
            };
            // Get the symmetric key from the configuration and create signing credentials
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? string.Empty));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            // Create the JWT token
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(30),
                signingCredentials: creds);
            // Serialize the token and return it as a string
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}