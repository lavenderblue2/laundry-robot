using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdministratorWeb.Data;
using AdministratorWeb.Models;
using System.Security.Claims;

namespace AdministratorWeb.Controllers
{
    /// <summary>
    /// Controller for managing rooms used as templates and dropdown options
    /// Provides CRUD operations for room management
    /// </summary>
    [Authorize]
    public class RoomsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RoomsController> _logger;

        public RoomsController(ApplicationDbContext context, ILogger<RoomsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Display the main room management page with room list and statistics
        /// </summary>
        /// <param name="status">Optional status filter (active, inactive, all)</param>
        /// <returns>Index view with room data</returns>
        public async Task<IActionResult> Index([FromQuery] string? status)
        {
            try
            {
                var query = _context.Rooms.AsQueryable();

                // Apply status filter
                if (!string.IsNullOrEmpty(status))
                {
                    switch (status.ToLower())
                    {
                        case "active":
                            query = query.Where(r => r.IsActive);
                            break;
                        case "inactive":
                            query = query.Where(r => !r.IsActive);
                            break;
                        // "all" shows everything, no additional filter needed
                    }
                }

                var rooms = await query.OrderBy(r => r.Name).ToListAsync();
                
                return View(rooms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading rooms index page");
                TempData["Error"] = "Failed to load room data.";
                return View(new List<Room>());
            }
        }

        /// <summary>
        /// Display room details
        /// </summary>
        /// <param name="id">Room ID</param>
        /// <returns>Details view</returns>
        public async Task<IActionResult> Details(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
            {
                return NotFound();
            }

            // Get usage statistics
            var usersCount = await _context.Users.CountAsync(u => u.RoomName == room.Name);
            var beaconsCount = await _context.BluetoothBeacons.CountAsync(b => b.RoomName == room.Name);

            ViewBag.UsersCount = usersCount;
            ViewBag.BeaconsCount = beaconsCount;

            return View(room);
        }

        /// <summary>
        /// Display form to create a new room
        /// </summary>
        /// <returns>Create view</returns>
        public IActionResult Create()
        {
            var room = new Room
            {
                IsActive = true
            };
            return View(room);
        }

        /// <summary>
        /// Process room creation form
        /// </summary>
        /// <param name="room">Room data from form</param>
        /// <returns>Redirect to index or return to form with errors</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] Room room)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Check for duplicate room name
                    var existingRoom = await _context.Rooms
                        .FirstOrDefaultAsync(r => r.Name.ToLower() == room.Name.ToLower());
                    
                    if (existingRoom != null)
                    {
                        ModelState.AddModelError("Name", "A room with this name already exists.");
                        return View(room);
                    }

                    // Set audit fields
                    room.CreatedAt = DateTime.UtcNow;
                    room.UpdatedAt = DateTime.UtcNow;
                    room.CreatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    room.UpdatedBy = room.CreatedBy;

                    _context.Rooms.Add(room);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Room '{room.Name}' created successfully.";
                    _logger.LogInformation("Room created: {RoomName} by user {UserId}", 
                        room.Name, room.CreatedBy);
                    
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating room");
                TempData["Error"] = "Failed to create room.";
            }

            return View(room);
        }

        /// <summary>
        /// Display form to edit an existing room
        /// </summary>
        /// <param name="id">Room ID</param>
        /// <returns>Edit view</returns>
        public async Task<IActionResult> Edit(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
            {
                return NotFound();
            }

            // Get current detection mode from settings
            var settings = await _context.LaundrySettings.FirstOrDefaultAsync();
            ViewBag.DetectionMode = settings?.DetectionMode ?? RoomDetectionMode.Beacon;

            // Get beacon count for this room
            var beaconsCount = await _context.BluetoothBeacons.CountAsync(b => b.RoomName == room.Name);
            ViewBag.BeaconsCount = beaconsCount;

            return View(room);
        }

        /// <summary>
        /// Process room edit form
        /// </summary>
        /// <param name="id">Room ID</param>
        /// <param name="room">Updated room data</param>
        /// <returns>Redirect to index or return to form with errors</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [FromForm] Room room)
        {
            if (id != room.Id)
            {
                return NotFound();
            }

            try
            {
                if (ModelState.IsValid)
                {
                    // Check for duplicate room name (excluding current room)
                    var existingRoom = await _context.Rooms
                        .FirstOrDefaultAsync(r => r.Name.ToLower() == room.Name.ToLower() && r.Id != id);
                    
                    if (existingRoom != null)
                    {
                        ModelState.AddModelError("Name", "A room with this name already exists.");
                        return View(room);
                    }

                    // Update audit fields
                    room.UpdatedAt = DateTime.UtcNow;
                    room.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                    _context.Update(room);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Room '{room.Name}' updated successfully.";
                    _logger.LogInformation("Room updated: {RoomName} by user {UserId}", 
                        room.Name, room.UpdatedBy);
                    
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!RoomExists(room.Id))
                {
                    return NotFound();
                }
                else
                {
                    _logger.LogError(ex, "Concurrency error updating room {Id}", id);
                    TempData["Error"] = "The room was modified by another user. Please try again.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating room {Id}", id);
                TempData["Error"] = "Failed to update room.";
            }

            // Re-populate detection mode and beacon count for view
            var settings = await _context.LaundrySettings.FirstOrDefaultAsync();
            ViewBag.DetectionMode = settings?.DetectionMode ?? RoomDetectionMode.Beacon;

            var beaconsCount = await _context.BluetoothBeacons.CountAsync(b => b.RoomName == room.Name);
            ViewBag.BeaconsCount = beaconsCount;

            return View(room);
        }

        /// <summary>
        /// Display room deletion confirmation
        /// </summary>
        /// <param name="id">Room ID</param>
        /// <returns>Delete confirmation view</returns>
        public async Task<IActionResult> Delete(int id)
        {
            var room = await _context.Rooms.FindAsync(id);
            if (room == null)
            {
                return NotFound();
            }

            // Check if room is in use
            var usersCount = await _context.Users.CountAsync(u => u.RoomName == room.Name);
            var beaconsCount = await _context.BluetoothBeacons.CountAsync(b => b.RoomName == room.Name);

            ViewBag.UsersCount = usersCount;
            ViewBag.BeaconsCount = beaconsCount;
            ViewBag.IsInUse = usersCount > 0 || beaconsCount > 0;

            return View(room);
        }

        /// <summary>
        /// Process room deletion
        /// </summary>
        /// <param name="id">Room ID</param>
        /// <returns>Redirect to index</returns>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed([FromForm] int id)
        {
            try
            {
                var room = await _context.Rooms.FindAsync(id);
                if (room != null)
                {
                    // Check if room is still in use
                    var usersCount = await _context.Users.CountAsync(u => u.RoomName == room.Name);
                    var beaconsCount = await _context.BluetoothBeacons.CountAsync(b => b.RoomName == room.Name);

                    if (usersCount > 0 || beaconsCount > 0)
                    {
                        TempData["Error"] = $"Cannot delete room '{room.Name}' as it is still being used by {usersCount} users and {beaconsCount} beacons.";
                        return RedirectToAction(nameof(Delete), new { id });
                    }

                    _context.Rooms.Remove(room);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Room '{room.Name}' deleted successfully.";
                    _logger.LogInformation("Room deleted: {RoomName} by user {UserId}", 
                        room.Name, User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting room {Id}", id);
                TempData["Error"] = "Failed to delete room.";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Toggle room active status via AJAX
        /// </summary>
        /// <param name="id">Room ID</param>
        /// <returns>JSON result</returns>
        [HttpPost]
        public async Task<IActionResult> ToggleStatus([FromForm] int id)
        {
            try
            {
                var room = await _context.Rooms.FindAsync(id);
                if (room == null)
                {
                    return Json(new { success = false, message = "Room not found." });
                }

                room.IsActive = !room.IsActive;
                room.UpdatedAt = DateTime.UtcNow;
                room.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                await _context.SaveChangesAsync();

                var status = room.IsActive ? "activated" : "deactivated";
                _logger.LogInformation("Room {RoomName} {Status} by user {UserId}", 
                    room.Name, status, room.UpdatedBy);

                return Json(new { success = true, isActive = room.IsActive, message = $"Room {status} successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling room status for ID {Id}", id);
                return Json(new { success = false, message = "Failed to toggle room status." });
            }
        }

        /// <summary>
        /// Check if a room exists
        /// </summary>
        /// <param name="id">Room ID</param>
        /// <returns>True if room exists</returns>
        private bool RoomExists(int id)
        {
            return _context.Rooms.Any(e => e.Id == id);
        }
    }
}