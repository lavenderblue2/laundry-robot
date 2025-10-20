using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdministratorWeb.Data;
using AdministratorWeb.Models;
using AdministratorWeb.Models.DTOs;
using System.Security.Claims;

namespace AdministratorWeb.Controllers
{
    /// <summary>
    /// Controller for managing Bluetooth beacons used in room tracking and robot navigation
    /// Provides CRUD operations for beacon configuration and monitoring
    /// </summary>
    [Authorize]
    public class BeaconController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BeaconController> _logger;

        public BeaconController(ApplicationDbContext context, ILogger<BeaconController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Display the main beacon management page with statistics and beacon list
        /// </summary>
        /// <param name="room">Optional room filter</param>
        /// <param name="status">Optional status filter (active, inactive, all)</param>
        /// <returns>Index view with beacon data</returns>
        public async Task<IActionResult> Index([FromQuery] string? room, [FromQuery] string? status)
        {
            try
            {
                var query = _context.BluetoothBeacons.AsQueryable();

                // Apply room filter
                if (!string.IsNullOrEmpty(room))
                {
                    query = query.Where(b => b.RoomName.Contains(room));
                }

                // Apply status filter
                if (!string.IsNullOrEmpty(status))
                {
                    switch (status.ToLower())
                    {
                        case "active":
                            query = query.Where(b => b.IsActive);
                            break;
                        case "inactive":
                            query = query.Where(b => !b.IsActive);
                            break;
                        // "all" shows everything, no additional filter needed
                    }
                }

                var beacons = await query.OrderBy(b => b.RoomName).ThenBy(b => b.Name).ToListAsync();
                
                var beaconWithStatus = new List<BeaconWithStatusDto>();
                foreach (var b in beacons)
                {
                    beaconWithStatus.Add(new BeaconWithStatusDto
                    {
                        Id = b.Id,
                        MacAddress = b.MacAddress,
                        Name = b.Name,
                        RoomName = b.RoomName,
                        RssiThreshold = b.RssiThreshold,
                        IsActive = b.IsActive,
                        IsNavigationTarget = b.IsNavigationTarget,
                        IsBase = b.IsBase,
                        Priority = b.Priority,
                        CreatedAt = b.CreatedAt,
                        LastSeenAt = b.LastSeenAt,
                        LastSeenByRobot = b.LastSeenByRobot,
                        LastRecordedRssi = b.LastRecordedRssi,
                        AssignedUserCount = await _context.Users.CountAsync(u => u.AssignedBeaconMacAddress == b.MacAddress)
                    });
                }

                // Calculate statistics
                var allBeacons = await _context.BluetoothBeacons.ToListAsync();
                var recentThreshold = DateTime.UtcNow.AddHours(-1);
                var baseBeacon = allBeacons.FirstOrDefault(b => b.IsBase);
                var totalAssignedUsers = await _context.Users.CountAsync(u => u.AssignedBeaconMacAddress != null);
                
                var dto = new BeaconIndexDto
                {
                    TotalBeacons = allBeacons.Count,
                    ActiveBeacons = allBeacons.Count(b => b.IsActive),
                    InactiveBeacons = allBeacons.Count(b => !b.IsActive),
                    NavigationTargets = allBeacons.Count(b => b.IsNavigationTarget),
                    RecentlyDetected = allBeacons.Count(b => b.LastSeenAt.HasValue && b.LastSeenAt.Value >= recentThreshold),
                    TotalRooms = allBeacons.Select(b => b.RoomName).Distinct().Count(),
                    AssignedUsers = totalAssignedUsers,
                    BaseBeacon = baseBeacon != null ? new BeaconWithStatusDto
                    {
                        Id = baseBeacon.Id,
                        MacAddress = baseBeacon.MacAddress,
                        Name = baseBeacon.Name,
                        RoomName = baseBeacon.RoomName,
                        IsBase = baseBeacon.IsBase,
                        IsActive = baseBeacon.IsActive,
                        AssignedUserCount = await _context.Users.CountAsync(u => u.AssignedBeaconMacAddress == baseBeacon.MacAddress)
                    } : null,
                    Beacons = beaconWithStatus,
                    AvailableRooms = allBeacons.Select(b => b.RoomName).Distinct().OrderBy(r => r).ToList()
                };

                return View(dto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading beacon index page");
                TempData["Error"] = "Failed to load beacon data.";
                return View(new BeaconIndexDto());
            }
        }

        /// <summary>
        /// Display beacon details
        /// </summary>
        /// <param name="id">Beacon ID</param>
        /// <returns>Details view</returns>
        public async Task<IActionResult> Details(int id)
        {
            var beacon = await _context.BluetoothBeacons.FindAsync(id);
            if (beacon == null)
            {
                return NotFound();
            }

            return View(beacon);
        }

        /// <summary>
        /// Display form to create a new beacon
        /// </summary>
        /// <returns>Create view</returns>
        public async Task<IActionResult> Create()
        {
            var beacon = new BluetoothBeacon
            {
                IsActive = true,
                RssiThreshold = -40,
                Priority = 1
            };
            
            var createDto = new BeaconCreateDto
            {
                AvailableRooms = await _context.Rooms
                    .Where(r => r.IsActive)
                    .OrderBy(r => r.Name)
                    .Select(r => new RoomOption { Name = r.Name, DisplayName = r.Name })
                    .ToListAsync()
            };
            ViewData["BeaconCreateData"] = createDto;
            
            return View(beacon);
        }

        /// <summary>
        /// Process beacon creation form
        /// </summary>
        /// <param name="beacon">Beacon data from form</param>
        /// <returns>Redirect to index or return to form with errors</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] BluetoothBeacon beacon)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // Normalize MAC address to uppercase
                    beacon.MacAddress = beacon.MacAddress.ToUpperInvariant();
                    
                    // Convert empty room name to null
                    if (string.IsNullOrWhiteSpace(beacon.RoomName))
                    {
                        beacon.RoomName = null;
                    }
                    
                    // Check for duplicate MAC address
                    var existingBeacon = await _context.BluetoothBeacons
                        .FirstOrDefaultAsync(b => b.MacAddress == beacon.MacAddress);
                    
                    if (existingBeacon != null)
                    {
                        ModelState.AddModelError("MacAddress", "A beacon with this MAC address already exists.");
                        
                        // Re-populate DTO for validation errors
                        var createDtoError = new BeaconCreateDto
                        {
                            AvailableRooms = await _context.Rooms
                                .Where(r => r.IsActive)
                                .OrderBy(r => r.Name)
                                .Select(r => new RoomOption { Name = r.Name, DisplayName = r.Name })
                                .ToListAsync()
                        };
                        ViewData["BeaconCreateData"] = createDtoError;
                        
                        return View(beacon);
                    }

                    // Set audit fields
                    beacon.CreatedAt = DateTime.UtcNow;
                    beacon.UpdatedAt = DateTime.UtcNow;
                    beacon.CreatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    beacon.UpdatedBy = beacon.CreatedBy;

                    _context.BluetoothBeacons.Add(beacon);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Beacon '{beacon.Name}' created successfully.";
                    _logger.LogInformation("Beacon created: {MacAddress} in {RoomName} by user {UserId}", 
                        beacon.MacAddress, beacon.RoomName, beacon.CreatedBy);
                    
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error creating beacon: {Message}", ex.Message);
                TempData["Error"] = $"Database error occurred while creating beacon. Details: {ex.InnerException?.Message ?? ex.Message}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating beacon: {Message}", ex.Message);
                TempData["Error"] = $"An unexpected error occurred while creating the beacon. Details: {ex.Message}";
            }

            // Re-populate DTO for validation errors or exceptions
            var createDtoFinal = new BeaconCreateDto
            {
                AvailableRooms = await _context.Rooms
                    .Where(r => r.IsActive)
                    .OrderBy(r => r.Name)
                    .Select(r => new RoomOption { Name = r.Name, DisplayName = r.Name })
                    .ToListAsync()
            };
            ViewData["BeaconCreateData"] = createDtoFinal;

            return View(beacon);
        }

        /// <summary>
        /// Display form to edit an existing beacon
        /// </summary>
        /// <param name="id">Beacon ID</param>
        /// <returns>Edit view</returns>
        public async Task<IActionResult> Edit(int id)
        {
            var beacon = await _context.BluetoothBeacons.FindAsync(id);
            if (beacon == null)
            {
                return NotFound();
            }

            var editDto = new BeaconEditDto
            {
                Beacon = beacon,
                AvailableRooms = await _context.Rooms
                    .Where(r => r.IsActive)
                    .OrderBy(r => r.Name)
                    .Select(r => new RoomOption { Name = r.Name, DisplayName = r.Name })
                    .ToListAsync()
            };
            ViewData["BeaconEditData"] = editDto;

            return View(beacon);
        }

        /// <summary>
        /// Process beacon edit form
        /// </summary>
        /// <param name="id">Beacon ID</param>
        /// <param name="beacon">Updated beacon data</param>
        /// <returns>Redirect to index or return to form with errors</returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [FromForm] BluetoothBeacon beacon)
        {
            _logger.LogDebug("Edit POST called for beacon ID {Id}, form beacon ID {BeaconId}", id, beacon?.Id);
            
            if (id != beacon.Id)
            {
                _logger.LogWarning("Route ID {RouteId} does not match beacon ID {BeaconId}", id, beacon.Id);
                TempData["Error"] = $"Route ID ({id}) does not match beacon ID ({beacon.Id}).";
                return NotFound();
            }

            try
            {
                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("ModelState is invalid for beacon {Id}. Errors: {Errors}", 
                        id, string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
                }
                
                if (ModelState.IsValid)
                {
                    // Normalize MAC address
                    beacon.MacAddress = beacon.MacAddress.ToUpperInvariant();
                    
                    // Convert empty room name to null
                    if (string.IsNullOrWhiteSpace(beacon.RoomName))
                    {
                        beacon.RoomName = null;
                    }
                    
                    // Check for duplicate MAC address (excluding current beacon)
                    var existingBeacon = await _context.BluetoothBeacons
                        .FirstOrDefaultAsync(b => b.MacAddress == beacon.MacAddress && b.Id != id);
                    
                    if (existingBeacon != null)
                    {
                        ModelState.AddModelError("MacAddress", "A beacon with this MAC address already exists.");
                        
                        // Re-populate DTO for validation errors
                        var editDto = new BeaconEditDto
                        {
                            Beacon = beacon,
                            AvailableRooms = await _context.Rooms
                                .Where(r => r.IsActive)
                                .OrderBy(r => r.Name)
                                .Select(r => new RoomOption { Name = r.Name, DisplayName = r.Name })
                                .ToListAsync()
                        };
                        ViewData["BeaconEditData"] = editDto;
                        
                        return View(beacon);
                    }

                    // Update audit fields
                    beacon.UpdatedAt = DateTime.UtcNow;
                    beacon.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                    _context.Update(beacon);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Beacon '{beacon.Name}' updated successfully.";
                    _logger.LogInformation("Beacon updated: {MacAddress} by user {UserId}", 
                        beacon.MacAddress, beacon.UpdatedBy);
                    
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!BeaconExists(beacon.Id))
                {
                    _logger.LogError(ex, "Beacon {Id} not found during concurrency update", id);
                    TempData["Error"] = $"Beacon with ID {id} no longer exists.";
                    return NotFound();
                }
                else
                {
                    _logger.LogError(ex, "Concurrency error updating beacon {Id}: {Message}", id, ex.Message);
                    TempData["Error"] = $"The beacon was modified by another user. Please refresh and try again. Details: {ex.Message}";
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error updating beacon {Id}: {Message}", id, ex.Message);
                TempData["Error"] = $"Database error occurred while updating beacon. Details: {ex.InnerException?.Message ?? ex.Message}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error updating beacon {Id}: {Message}", id, ex.Message);
                TempData["Error"] = $"An unexpected error occurred while updating the beacon. Details: {ex.Message}";
            }

            return View(beacon);
        }

        /// <summary>
        /// Display beacon deletion confirmation
        /// </summary>
        /// <param name="id">Beacon ID</param>
        /// <returns>Delete confirmation view</returns>
        public async Task<IActionResult> Delete(int id)
        {
            var beacon = await _context.BluetoothBeacons.FindAsync(id);
            if (beacon == null)
            {
                return NotFound();
            }

            return View(beacon);
        }

        /// <summary>
        /// Process beacon deletion
        /// </summary>
        /// <param name="id">Beacon ID</param>
        /// <returns>Redirect to index</returns>
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed([FromForm] int id)
        {
            try
            {
                var beacon = await _context.BluetoothBeacons.FindAsync(id);
                if (beacon != null)
                {
                    _context.BluetoothBeacons.Remove(beacon);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"Beacon '{beacon.Name}' deleted successfully.";
                    _logger.LogInformation("Beacon deleted: {MacAddress} by user {UserId}", 
                        beacon.MacAddress, User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting beacon {Id}", id);
                TempData["Error"] = "Failed to delete beacon.";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Toggle beacon active status via AJAX
        /// </summary>
        /// <param name="id">Beacon ID</param>
        /// <returns>JSON result</returns>
        [HttpPost]
        public async Task<IActionResult> ToggleStatus([FromForm] int id)
        {
            try
            {
                var beacon = await _context.BluetoothBeacons.FindAsync(id);
                if (beacon == null)
                {
                    return Json(new { success = false, message = "Beacon not found." });
                }

                beacon.IsActive = !beacon.IsActive;
                beacon.UpdatedAt = DateTime.UtcNow;
                beacon.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                await _context.SaveChangesAsync();

                var status = beacon.IsActive ? "activated" : "deactivated";
                _logger.LogInformation("Beacon {MacAddress} {Status} by user {UserId}", 
                    beacon.MacAddress, status, beacon.UpdatedBy);

                return Json(new { success = true, isActive = beacon.IsActive, message = $"Beacon {status} successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling beacon status for ID {Id}", id);
                return Json(new { success = false, message = "Failed to toggle beacon status." });
            }
        }

        /// <summary>
        /// Set a beacon as the base/laundry room location
        /// </summary>
        /// <param name="macAddress">Beacon MAC address to set as base</param>
        /// <returns>JSON result</returns>
        [HttpPost]
        public async Task<IActionResult> SetAsBase([FromForm] string macAddress)
        {
            try
            {
                if (string.IsNullOrEmpty(macAddress))
                {
                    return Json(new { success = false, message = "MAC address is required." });
                }

                // Normalize MAC address to uppercase
                macAddress = macAddress.ToUpperInvariant();

                var beacon = await _context.BluetoothBeacons.FirstOrDefaultAsync(b => b.MacAddress == macAddress);
                if (beacon == null)
                {
                    return Json(new { success = false, message = "Beacon not found." });
                }

                // First, remove base status from any existing base beacon
                var currentBase = await _context.BluetoothBeacons.FirstOrDefaultAsync(b => b.IsBase);
                if (currentBase != null && currentBase.MacAddress != macAddress)
                {
                    currentBase.IsBase = false;
                    currentBase.UpdatedAt = DateTime.UtcNow;
                    currentBase.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                }

                // Set the new base beacon
                beacon.IsBase = true;
                beacon.IsActive = true; // Base beacon must be active
                beacon.IsNavigationTarget = true; // Base beacon must be a navigation target
                beacon.UpdatedAt = DateTime.UtcNow;
                beacon.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Base beacon set to {MacAddress} ({Name}) by user {UserId}", 
                    beacon.MacAddress, beacon.Name, beacon.UpdatedBy);

                return Json(new { success = true, message = $"'{beacon.Name}' has been set as the base beacon." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting base beacon for MAC address {MacAddress}", macAddress);
                return Json(new { success = false, message = "Failed to set base beacon." });
            }
        }

        /// <summary>
        /// Remove base status from a beacon
        /// </summary>
        /// <param name="macAddress">Beacon MAC address to remove base status from</param>
        /// <returns>JSON result</returns>
        [HttpPost]
        public async Task<IActionResult> RemoveAsBase([FromForm] string macAddress)
        {
            try
            {
                if (string.IsNullOrEmpty(macAddress))
                {
                    return Json(new { success = false, message = "MAC address is required." });
                }

                // Normalize MAC address to uppercase
                macAddress = macAddress.ToUpperInvariant();

                var beacon = await _context.BluetoothBeacons.FirstOrDefaultAsync(b => b.MacAddress == macAddress);
                if (beacon == null)
                {
                    return Json(new { success = false, message = "Beacon not found." });
                }

                beacon.IsBase = false;
                beacon.UpdatedAt = DateTime.UtcNow;
                beacon.UpdatedBy = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Base beacon status removed from {MacAddress} ({Name}) by user {UserId}", 
                    beacon.MacAddress, beacon.Name, beacon.UpdatedBy);

                return Json(new { success = true, message = $"Base beacon status removed from '{beacon.Name}'." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing base beacon status for MAC address {MacAddress}", macAddress);
                return Json(new { success = false, message = "Failed to remove base beacon status." });
            }
        }

        /// <summary>
        /// Check if a beacon exists
        /// </summary>
        /// <param name="id">Beacon ID</param>
        /// <returns>True if beacon exists</returns>
        private bool BeaconExists(int id)
        {
            return _context.BluetoothBeacons.Any(e => e.Id == id);
        }
    }
}