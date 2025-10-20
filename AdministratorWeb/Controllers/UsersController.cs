using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdministratorWeb.Models;
using AdministratorWeb.Models.DTOs;
using AdministratorWeb.Data;

namespace AdministratorWeb.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var userViewModels = new List<object>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userViewModels.Add(new
                {
                    User = user,
                    Roles = roles
                });
            }

            return View(userViewModels);
        }

        public async Task<IActionResult> Create()
        {
            var createDto = new UsersCreateDto
            {
                AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync(),
                AvailableBeacons = await _context.BluetoothBeacons
                    .Where(b => b.IsActive && !b.IsBase)
                    .Select(b => new BeaconOption { MacAddress = b.MacAddress, DisplayName = $"{b.Name} ({b.RoomName})" })
                    .ToListAsync(),
                AvailableRooms = await _context.Rooms
                    .Where(r => r.IsActive)
                    .Select(r => new RoomOption { Name = r.Name, DisplayName = r.Name })
                    .ToListAsync()
            };
            ViewData["UsersCreateData"] = createDto;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string firstName, string lastName, string email, string password, string role, string? assignedBeaconMacAddress = null, string? roomName = null, string? roomDescription = null)
        {
            var createDto = new UsersCreateDto
            {
                AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync(),
                AvailableBeacons = await _context.BluetoothBeacons
                    .Where(b => b.IsActive && !b.IsBase)
                    .Select(b => new BeaconOption { MacAddress = b.MacAddress, DisplayName = $"{b.Name} ({b.RoomName})" })
                    .ToListAsync(),
                AvailableRooms = await _context.Rooms
                    .Where(r => r.IsActive)
                    .Select(r => new RoomOption { Name = r.Name, DisplayName = r.Name })
                    .ToListAsync()
            };
            ViewData["UsersCreateData"] = createDto;

            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || 
                string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || 
                string.IsNullOrWhiteSpace(role))
            {
                ModelState.AddModelError("", "All fields are required.");
                return View();
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                EmailConfirmed = true,
                IsActive = true,
                AssignedBeaconMacAddress = assignedBeaconMacAddress?.ToUpperInvariant(),
                RoomName = roomName,
                RoomDescription = roomDescription
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, role);
                TempData["Success"] = "User created successfully.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View();
        }

        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return NotFound();
            }

            var userRoles = await _userManager.GetRolesAsync(user);
            var editDto = new UsersEditDto
            {
                User = user,
                AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync(),
                UserRoles = userRoles,
                AvailableBeacons = await _context.BluetoothBeacons
                    .Where(b => b.IsActive && !b.IsBase)
                    .Select(b => new BeaconOption { MacAddress = b.MacAddress, DisplayName = $"{b.Name} ({b.RoomName})" })
                    .ToListAsync(),
                AvailableRooms = await _context.Rooms
                    .Where(r => r.IsActive)
                    .Select(r => new RoomOption { Name = r.Name, DisplayName = r.Name })
                    .ToListAsync()
            };
            ViewData["UsersEditData"] = editDto;

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, string firstName, string lastName, string email, string userName, string phoneNumber, string role, bool isActive, string? assignedBeaconMacAddress = null, string? roomName = null, string? roomDescription = null, string newPassword = "")
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            // Update basic user properties
            user.FirstName = firstName;
            user.LastName = lastName;
            user.Email = email;
            user.UserName = !string.IsNullOrWhiteSpace(userName) ? userName : email;
            user.PhoneNumber = phoneNumber;
            user.IsActive = isActive;
            user.AssignedBeaconMacAddress = assignedBeaconMacAddress?.ToUpperInvariant();
            user.RoomName = roomName;
            user.RoomDescription = roomDescription;

            // Update the user
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
                var editDto = new UsersEditDto
                {
                    User = user,
                    AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync(),
                    UserRoles = await _userManager.GetRolesAsync(user),
                    AvailableBeacons = await _context.BluetoothBeacons
                        .Where(b => b.IsActive && !b.IsBase)
                        .Select(b => new BeaconOption { MacAddress = b.MacAddress, DisplayName = $"{b.Name} ({b.RoomName})" })
                        .ToListAsync(),
                    AvailableRooms = await _context.Rooms
                        .Where(r => r.IsActive)
                        .Select(r => new RoomOption { Name = r.Name, DisplayName = r.Name })
                        .ToListAsync()
                };
                ViewData["UsersEditData"] = editDto;
                return View(user);
            }

            // Update password if provided
            if (!string.IsNullOrWhiteSpace(newPassword))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var passwordResult = await _userManager.ResetPasswordAsync(user, token, newPassword);
                if (!passwordResult.Succeeded)
                {
                    foreach (var error in passwordResult.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                    var editDto2 = new UsersEditDto
                    {
                        User = user,
                        AvailableRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync(),
                        UserRoles = await _userManager.GetRolesAsync(user),
                        AvailableBeacons = await _context.BluetoothBeacons
                            .Where(b => b.IsActive && !b.IsBase)
                            .Select(b => new BeaconOption { MacAddress = b.MacAddress, DisplayName = $"{b.Name} ({b.RoomName})" })
                            .ToListAsync()
                    };
                    ViewData["UsersEditData"] = editDto2;
                    return View(user);
                }
            }

            // Update roles
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, role);

            TempData["Success"] = "User updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user != null)
            {
                await _userManager.DeleteAsync(user);
                TempData["Success"] = "User deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}