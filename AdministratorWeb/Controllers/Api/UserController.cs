using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using AdministratorWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace AdministratorWeb.Controllers.Api
{
    /// <summary>
    /// MOBILE APP CONTROLLER - Used by mobile application
    /// MUST use [Authorize] for mobile app authentication
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "ApiPolicy")]
    public class UserController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UserController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        /// <summary>
        /// Get list of all administrators for customer to message
        /// </summary>
        [HttpGet("admins")]
        public async Task<IActionResult> GetAdmins()
        {
            // Get all users with Administrator role
            var adminRole = await _roleManager.FindByNameAsync("Administrator");
            if (adminRole == null)
            {
                return Ok(new List<object>()); // No admins
            }

            var admins = await _userManager.GetUsersInRoleAsync("Administrator");

            // Return ALL admins (active and inactive) - customers can message anyone
            var adminList = admins
                .Select(u => new
                {
                    id = u.Id,
                    firstName = u.FirstName,
                    lastName = u.LastName,
                    fullName = u.FullName,
                    email = u.Email,
                    isActive = u.IsActive // Include status for UI indication
                })
                .ToList();

            return Ok(adminList);
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            var customerId = User.FindFirst("CustomerId")?.Value;
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized("Customer ID not found in token");
            }

            var user = await _userManager.FindByIdAsync(customerId);
            if (user == null)
            {
                return NotFound("User not found");
            }

            return Ok(new
            {
                firstName = user.FirstName,
                lastName = user.LastName,
                email = user.Email,
                phone = user.PhoneNumber,
                roomName = user.RoomName,
                roomDescription = user.RoomDescription,
                assignedBeaconMacAddress = user.AssignedBeaconMacAddress
            });
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var customerId = User.FindFirst("CustomerId")?.Value;
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized("Customer ID not found in token");
            }

            var user = await _userManager.FindByIdAsync(customerId);
            if (user == null)
            {
                return NotFound("User not found");
            }

            if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName) || 
                string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { success = false, message = "First name, last name, and email are required" });
            }

            user.FirstName = request.FirstName.Trim();
            user.LastName = request.LastName.Trim();
            user.Email = request.Email.Trim();
            user.UserName = request.Email.Trim();
            user.PhoneNumber = request.Phone?.Trim();

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                return Ok(new { success = true, message = "Profile updated successfully" });
            }

            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return BadRequest(new { success = false, message = errors });
        }
    }

    public class UpdateProfileRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
    }
}