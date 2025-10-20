using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using AdministratorWeb.Services;
using AdministratorWeb.Models;

namespace AdministratorWeb.Controllers.Api
{
    /// <summary>
    /// MOBILE APP CONTROLLER - Used by mobile application
    /// Authentication endpoints - some methods use [AllowAnonymous] for login/register
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly JwtTokenService _jwtTokenService;
        private readonly UserManager<ApplicationUser> _userManager;

        public AuthController(JwtTokenService jwtTokenService, UserManager<ApplicationUser> userManager)
        {
            _jwtTokenService = jwtTokenService;
            _userManager = userManager;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrEmpty(request.FirstName) || string.IsNullOrEmpty(request.LastName) || 
                string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { success = false, message = "All fields are required" });
            }

            if (request.Password != request.ConfirmPassword)
            {
                return BadRequest(new { success = false, message = "Passwords do not match" });
            }

            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                return BadRequest(new { success = false, message = "Email already exists" });
            }

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "Member");
                return Ok(new { success = true, message = "Account created successfully" });
            }

            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return BadRequest(new { success = false, message = errors });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { success = false, message = "Username and password are required" });
            }

            var user = await _userManager.FindByNameAsync(request.Username) 
                    ?? await _userManager.FindByEmailAsync(request.Username);
                    
            if (user == null || !user.IsActive)
            {
                return Unauthorized(new { success = false, message = "Invalid credentials" });
            }

            var result = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!result)
            {
                return Unauthorized(new { success = false, message = "Invalid credentials" });
            }

            var token = _jwtTokenService.GenerateToken(user.Id, user.FullName);
            
            return Ok(new
            {
                token = token,
                customerId = user.Id,
                customerName = user.FullName,
                expiresAt = DateTime.UtcNow.AddHours(24)
            });
        }

        [HttpPost("token")]
        public IActionResult GenerateToken([FromBody] TokenRequest request)
        {
            if (string.IsNullOrEmpty(request.CustomerId) || string.IsNullOrEmpty(request.CustomerName))
            {
                return BadRequest("CustomerId and CustomerName are required");
            }

            var token = _jwtTokenService.GenerateToken(request.CustomerId, request.CustomerName);
            
            return Ok(new
            {
                token = token,
                customerId = request.CustomerId,
                customerName = request.CustomerName,
                expiresAt = DateTime.UtcNow.AddHours(24)
            });
        }

        [HttpGet("generate200")]
        [Authorize(Policy = "ApiPolicy")]
        public IActionResult Generate200()
        {
            return Ok(new { success = true, message = "Token is valid" });
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class TokenRequest
    {
        public string CustomerId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
    }

    public class RegisterRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}