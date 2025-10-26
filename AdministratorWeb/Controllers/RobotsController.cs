using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AdministratorWeb.Services;
using AdministratorWeb.Models.DTOs;
using RobotProject.Shared.DTOs;
using AdministratorWeb.Data;
using Microsoft.EntityFrameworkCore;
using AdministratorWeb.Models;

namespace AdministratorWeb.Controllers
{
    /// <summary>
    /// IMPORTANT: This controller handles ADMINISTRATOR FRONTEND VIEWS for robot management.
    /// For robot-specific API endpoints (used by actual robots), see Controllers/Api/RobotController.cs
    /// 
    /// This controller provides:
    /// - Robot status dashboard views
    /// - Robot configuration management UI  
    /// - Robot monitoring and control interfaces
    /// - Admin-only robot management features
    /// </summary>
    [Authorize(Roles = "Administrator")]
    public class RobotsController : Controller
    {
        private readonly IRobotManagementService _robotService;
        private readonly ILogger<RobotsController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public RobotsController(
            IRobotManagementService robotService,
            ILogger<RobotsController> logger,
            ApplicationDbContext context,
            INotificationService notificationService)
        {
            _robotService = robotService;
            _logger = logger;
            _context = context;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index()
        {
            var robots = await _robotService.GetAllRobotsAsync();
            
            // Get active requests for each robot
            var activeRequests = await _context.LaundryRequests
                .Where(r => r.AssignedRobotName != null && 
                           r.Status != RequestStatus.Completed && 
                           r.Status != RequestStatus.Cancelled && 
                           r.Status != RequestStatus.Declined)
                .ToListAsync();
            
            // Create a lookup dictionary that can handle multiple requests per robot
            var robotActiveRequests = activeRequests
                .Where(r => r.AssignedRobotName != null)
                .ToLookup(r => r.AssignedRobotName!, r => r);
            ViewData["ActiveRequests"] = robotActiveRequests;
            
            return View(robots);
        }

        public async Task<IActionResult> Details(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return NotFound();
            }

            var robot = await _robotService.GetRobotAsync(name);
            if (robot == null)
            {
                return NotFound();
            }

            // Map to DTO
            var robotDetailsDto = new RobotDetailsDto
            {
                Name = robot.Name,
                IpAddress = robot.IpAddress,
                IsActive = robot.IsActive,
                CanAcceptRequests = robot.CanAcceptRequests,
                Status = robot.Status,
                CurrentTask = robot.CurrentTask,
                CurrentLocation = robot.CurrentLocation,
                ConnectedAt = robot.ConnectedAt,
                LastPing = robot.LastPing,
                IsOffline = robot.IsOffline,
                IsFollowingLine = robot.IsFollowingLine,
                FollowColorR = robot.FollowColorR,
                FollowColorG = robot.FollowColorG,
                FollowColorB = robot.FollowColorB,
                CameraData = robot.CameraData != null ? new RobotCameraDataDto
                {
                    LineDetected = robot.CameraData.LineDetected,
                    LinePosition = robot.CameraData.LinePosition,
                    FrameWidth = robot.CameraData.FrameWidth,
                    FrameCenter = robot.CameraData.FrameCenter,
                    Error = robot.CameraData.Error,
                    DetectionMethod = robot.CameraData.DetectionMethod,
                    UsingMemory = robot.CameraData.UsingMemory,
                    TimeSinceLastLine = robot.CameraData.TimeSinceLastLine,
                    LastUpdate = robot.CameraData.LastUpdate,
                    ImageData = robot.CameraData.ImageData
                } : null,
                DetectedBeacons = robot.DetectedBeacons.Values.Select(beacon => new RobotDetectedBeaconDto
                {
                    MacAddress = beacon.MacAddress,
                    BeaconName = beacon.BeaconName,
                    RoomName = beacon.RoomName,
                    CurrentRssi = beacon.CurrentRssi,
                    DistanceMeters = beacon.DistanceMeters,
                    IsInRange = beacon.IsInRange,
                    FirstDetected = beacon.FirstDetected,
                    LastDetected = beacon.LastDetected,
                    DetectionCount = beacon.DetectionCount,
                    AverageRssi = beacon.AverageRssi,
                    SignalStrength = beacon.SignalStrength,
                    Status = beacon.Status
                }).OrderByDescending(b => b.CurrentRssi).ToList()
            };

            // Get active request for this robot
            var activeRequest = await _context.LaundryRequests
                .FirstOrDefaultAsync(r => r.AssignedRobotName == name &&
                                         r.Status != RequestStatus.Completed &&
                                         r.Status != RequestStatus.Cancelled &&
                                         r.Status != RequestStatus.Declined);

            ViewData["ActiveRequest"] = activeRequest;

            // Calculate actual line following state: manual flag OR has active request
            bool hasActiveRequest = activeRequest != null;
            robotDetailsDto.IsActuallyLineFollowing = robotDetailsDto.IsFollowingLine || hasActiveRequest;
            robotDetailsDto.IsFollowingDueToRequest = hasActiveRequest && !robotDetailsDto.IsFollowingLine;

            // Get target room color if there's an active request
            Room? targetRoom = null;
            if (activeRequest != null)
            {
                var customer = await _context.Users.FindAsync(activeRequest.CustomerId);
                if (customer != null && !string.IsNullOrEmpty(customer.RoomName))
                {
                    targetRoom = await _context.Rooms.FirstOrDefaultAsync(r => r.Name == customer.RoomName);
                }
            }
            ViewData["TargetRoom"] = targetRoom;

            return View(robotDetailsDto);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus([FromForm] string name)
        {
            var success = await _robotService.ToggleRobotStatusAsync(name);
            if (success)
            {
                TempData["Success"] = "Robot status updated successfully.";
            }
            else
            {
                TempData["Error"] = "Robot not found.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> ToggleAcceptRequests([FromForm] string name)
        {
            var success = await _robotService.ToggleAcceptRequestsAsync(name);
            if (success)
            {
                TempData["Success"] = "Robot request acceptance toggled successfully.";
            }
            else
            {
                TempData["Error"] = "Robot not found.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CancelRequestReturnToBase([FromForm] string name)
        {
            // Find active request for this robot
            var activeRequest = await _context.LaundryRequests
                .FirstOrDefaultAsync(r => r.AssignedRobotName == name &&
                                         (r.Status == RequestStatus.Accepted ||
                                          r.Status == RequestStatus.LaundryLoaded ||
                                          r.Status == RequestStatus.ArrivedAtRoom ||
                                          r.Status == RequestStatus.FinishedWashingGoingToRoom ||
                                          r.Status == RequestStatus.FinishedWashingGoingToBase));

            if (activeRequest == null)
            {
                TempData["Error"] = "No active request found for this robot.";
                return RedirectToAction(nameof(Details), new { name });
            }

            // Cancel the request
            activeRequest.Status = RequestStatus.Cancelled;
            activeRequest.ProcessedAt = DateTime.UtcNow;
            activeRequest.DeclineReason = $"Cancelled by admin at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - robot returning to base";

            await _context.SaveChangesAsync();

            _logger.LogWarning("Admin cancelled request {RequestId} for customer {CustomerName} - robot {RobotName} returning to base",
                activeRequest.Id, activeRequest.CustomerName, name);

            TempData["Success"] = $"Request #{activeRequest.Id} cancelled. Robot returning to base.";

            return RedirectToAction(nameof(Details), new { name });
        }

        [HttpPost]
        public async Task<IActionResult> ForceStopAndCancelRequests([FromForm] string name)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Get robot first to save position before stopping
                var robot = await _robotService.GetRobotAsync(name);
                if (robot == null)
                {
                    TempData["Error"] = "Robot not found.";
                    return RedirectToAction(nameof(Details), new { name });
                }

                // Save robot state snapshot before force stop
                var stateSnapshot = new
                {
                    robot.CurrentTask,
                    robot.CurrentLocation,
                    robot.Status,
                    LastKnownPosition = robot.CameraData?.LinePosition,
                    NearbyBeacons = robot.DetectedBeacons.Values
                        .Where(b => b.IsInRange)
                        .Select(b => new { b.MacAddress, b.BeaconName, b.RoomName, b.CurrentRssi })
                        .ToList(),
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogWarning("Force stop initiated for robot {RobotName}. State snapshot: {@StateSnapshot}",
                    name, stateSnapshot);

                // Stop the robot first
                var stopSuccess = await _robotService.SetLineFollowingAsync(name, false);

                // Find all requests assigned to this robot that are not already completed/cancelled/declined
                var activeRequests = await _context.LaundryRequests
                    .Where(r => r.AssignedRobotName == name &&
                               r.Status != RequestStatus.Completed &&
                               r.Status != RequestStatus.Cancelled &&
                               r.Status != RequestStatus.Declined)
                    .ToListAsync();

                int cancelledCount = 0;
                foreach (var request in activeRequests)
                {
                    request.Status = RequestStatus.Cancelled;
                    request.ProcessedAt = DateTime.UtcNow;
                    var cancelReason = $"Force stopped by admin at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}. " +
                                      $"Robot was: {stateSnapshot.CurrentTask ?? "idle"}";
                    request.DeclineReason = cancelReason;
                    cancelledCount++;

                    _logger.LogWarning("Cancelled request {RequestId} for customer {CustomerName} due to force stop",
                        request.Id, request.CustomerName);

                    // Notify customer about cancellation
                    await _notificationService.NotifyCustomerRequestCancelledAsync(request, cancelReason);
                }

                await _context.SaveChangesAsync();

                // Clear all navigation targets by cancelling admin navigation requests
                var adminNavRequests = await _context.LaundryRequests
                    .Where(r => r.AssignedRobotName == name &&
                               r.CustomerName == "ADMIN_NAVIGATION" &&
                               r.Status == RequestStatus.Accepted)
                    .ToListAsync();

                int navTargetsCleared = 0;
                foreach (var navRequest in adminNavRequests)
                {
                    navRequest.Status = RequestStatus.Cancelled;
                    navRequest.DeclineReason = $"Force stopped by admin at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}";
                    navTargetsCleared++;
                }

                if (navTargetsCleared > 0)
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Cleared {Count} navigation targets for robot {RobotName} during force stop",
                        navTargetsCleared, name);
                }

                // Free up the robot completely and update in-memory state
                robot.Status = RobotStatus.Available;
                robot.CurrentTask = null;
                robot.IsFollowingLine = false;

                // Commit transaction - all changes are atomic
                await transaction.CommitAsync();

                _logger.LogInformation("Force stop executed for robot {RobotName}: stopped robot, cancelled {RequestCount} requests, cleared {NavCount} navigation targets",
                    name, cancelledCount, navTargetsCleared);

                if (stopSuccess)
                {
                    TempData["Success"] = $"Robot stopped successfully! Cancelled {cancelledCount} request(s) and cleared {navTargetsCleared} navigation target(s).";
                }
                else
                {
                    TempData["Warning"] = $"Robot may be offline, but {cancelledCount} request(s) and {navTargetsCleared} navigation target(s) were cancelled.";
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error during force stop for robot {RobotName}. Transaction rolled back.", name);
                TempData["Error"] = "An error occurred while stopping robot and cancelling requests. No changes were made.";
            }

            return RedirectToAction(nameof(Details), new { name });
        }

        [HttpPost]
        public async Task<IActionResult> GoToRoom([FromForm] string name, [FromForm] string room)
        {
            try
            {
                // Find the robot
                var robot = await _robotService.GetRobotAsync(name);
                if (robot == null)
                {
                    TempData["Error"] = "Robot not found.";
                    return RedirectToAction(nameof(Details), new { name });
                }

                // Find the bluetooth beacon for the specified room (case insensitive)
                var beacon = await _context.BluetoothBeacons
                    .FirstOrDefaultAsync(b => b.RoomName.ToLower() == room.ToLower() && b.IsActive);

                if (beacon == null)
                {
                    TempData["Error"] = $"No active beacon found for room '{room}'.";
                    return RedirectToAction(nameof(Details), new { name });
                }

                // Cancel any existing admin navigation requests for this robot
                var existingAdminRequests = await _context.LaundryRequests
                    .Where(r => r.AssignedRobotName == name && 
                               r.CustomerName == "ADMIN_NAVIGATION" && 
                               r.Status == RequestStatus.Accepted)
                    .ToListAsync();

                foreach (var request in existingAdminRequests)
                {
                    request.Status = RequestStatus.Cancelled;
                }

                // Create a temporary admin navigation request to leverage existing navigation system
                var adminRequest = new LaundryRequest
                {
                    CustomerName = "ADMIN_NAVIGATION",
                    CustomerId = Guid.NewGuid().ToString(), // Temporary ID
                    CustomerPhone = "N/A",
                    Address = $"Navigation to {room}",
                    Weight = 0,
                    Status = RequestStatus.Accepted,
                    AssignedRobotName = name,
                    AssignedBeaconMacAddress = beacon.MacAddress,
                    RequestedAt = DateTime.UtcNow,
                    AcceptedAt = DateTime.UtcNow,
                    ProcessedAt = DateTime.UtcNow,
                    TotalCost = 0
                };

                _context.LaundryRequests.Add(adminRequest);
                await _context.SaveChangesAsync();

                // Update robot task
                await _robotService.SetRobotTaskAsync(name, $"Going to {room}");

                _logger.LogInformation("Robot {RobotName} instructed to go to room {Room} (beacon: {BeaconMac}) via admin navigation request {RequestId}", 
                    name, room, beacon.MacAddress, adminRequest.Id);

                TempData["Success"] = $"Robot {name} is now navigating to {room} (beacon: {beacon.Name}).";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error instructing robot {RobotName} to go to room {Room}", name, room);
                TempData["Error"] = $"Error instructing robot to go to {room}.";
            }

            return RedirectToAction(nameof(Details), new { name });
        }

        [HttpPost]
        public async Task<IActionResult> SetBeaconTarget([FromForm] string robotName, [FromForm] string beaconMac)
        {
            try
            {
                // Find the robot
                var robot = await _robotService.GetRobotAsync(robotName);
                if (robot == null)
                {
                    TempData["Error"] = "Robot not found.";
                    return RedirectToAction(nameof(Details), new { name = robotName });
                }

                // Find the bluetooth beacon by MAC address
                var beacon = await _context.BluetoothBeacons
                    .FirstOrDefaultAsync(b => b.MacAddress.ToLower() == beaconMac.ToLower() && b.IsActive);

                if (beacon == null)
                {
                    TempData["Error"] = $"No active beacon found with MAC address '{beaconMac}'.";
                    return RedirectToAction(nameof(Details), new { name = robotName });
                }

                // Cancel any existing admin navigation requests for this robot
                var existingAdminRequests = await _context.LaundryRequests
                    .Where(r => r.AssignedRobotName == robotName && 
                               r.CustomerName == "ADMIN_NAVIGATION" && 
                               r.Status == RequestStatus.Accepted)
                    .ToListAsync();

                foreach (var request in existingAdminRequests)
                {
                    request.Status = RequestStatus.Cancelled;
                }

                // Create a temporary admin navigation request for this specific beacon
                var adminRequest = new LaundryRequest
                {
                    CustomerName = "ADMIN_NAVIGATION",
                    CustomerId = Guid.NewGuid().ToString(),
                    CustomerPhone = "N/A",
                    Address = $"Navigation to beacon {beacon.Name} ({beacon.RoomName})",
                    Weight = 0,
                    Status = RequestStatus.Accepted,
                    AssignedRobotName = robotName,
                    AssignedBeaconMacAddress = beacon.MacAddress,
                    RequestedAt = DateTime.UtcNow,
                    AcceptedAt = DateTime.UtcNow,
                    ProcessedAt = DateTime.UtcNow,
                    TotalCost = 0
                };

                _context.LaundryRequests.Add(adminRequest);
                await _context.SaveChangesAsync();

                // Update robot task
                await _robotService.SetRobotTaskAsync(robotName, $"Going to beacon {beacon.Name}");

                _logger.LogInformation("Robot {RobotName} set to navigate to beacon {BeaconName} ({BeaconMac}) via admin request {RequestId}", 
                    robotName, beacon.Name, beacon.MacAddress, adminRequest.Id);

                TempData["Success"] = $"Robot {robotName} is now navigating to beacon {beacon.Name} ({beacon.RoomName}).";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting beacon target for robot {RobotName}", robotName);
                TempData["Error"] = "Error setting beacon as target.";
            }

            return RedirectToAction(nameof(Details), new { name = robotName });
        }

        [HttpPost]
        public async Task<IActionResult> UnsetTarget([FromForm] string robotName, [FromForm] string beaconMac)
        {
            try
            {
                // Find the robot
                var robot = await _robotService.GetRobotAsync(robotName);
                if (robot == null)
                {
                    TempData["Error"] = "Robot not found.";
                    return RedirectToAction(nameof(Details), new { name = robotName });
                }

                // Cancel any existing admin navigation requests for this robot targeting this beacon
                var existingAdminRequests = await _context.LaundryRequests
                    .Where(r => r.AssignedRobotName == robotName && 
                               r.CustomerName == "ADMIN_NAVIGATION" && 
                               r.Status == RequestStatus.Accepted &&
                               string.Equals(r.AssignedBeaconMacAddress, beaconMac, StringComparison.OrdinalIgnoreCase))
                    .ToListAsync();

                if (existingAdminRequests.Any())
                {
                    foreach (var request in existingAdminRequests)
                    {
                        request.Status = RequestStatus.Cancelled;
                        request.DeclineReason = $"Navigation target cancelled by admin for beacon {beaconMac}";
                    }
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Cancelled {RequestCount} admin navigation requests for robot {RobotName} targeting beacon {BeaconMac}", 
                        existingAdminRequests.Count, robotName, beaconMac);

                    TempData["Success"] = $"Navigation target cancelled for robot {robotName} targeting beacon {beaconMac}.";
                }
                else
                {
                    TempData["Info"] = $"Robot {robotName} has no active navigation target for beacon {beaconMac} to cancel.";
                }

                // Update robot task to indicate target was cancelled
                await _robotService.SetRobotTaskAsync(robotName, $"Navigation target cancelled for beacon {beaconMac}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsetting target for robot {RobotName} targeting beacon {BeaconMac}", robotName, beaconMac);
                TempData["Error"] = "Error cancelling navigation target.";
            }

            return RedirectToAction(nameof(Details), new { name = robotName });
        }

        [HttpPost]
        public async Task<IActionResult> Disconnect([FromForm] string name)
        {
            var success = await _robotService.DisconnectRobotAsync(name);
            if (success)
            {
                TempData["Success"] = "Robot disconnected.";
            }
            else
            {
                TempData["Error"] = "Robot not found.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> UpdateRobotColor([FromForm] string robotName, [FromForm] int r, [FromForm] int g, [FromForm] int b)
        {
            try
            {
                var robot = await _robotService.GetRobotAsync(robotName);
                if (robot == null)
                {
                    return Json(new { success = false, message = "Robot not found" });
                }

                // Validate RGB values
                if (r < 0 || r > 255 || g < 0 || g > 255 || b < 0 || b > 255)
                {
                    return Json(new { success = false, message = "RGB values must be between 0 and 255" });
                }

                // Update robot color
                robot.FollowColorR = (byte)r;
                robot.FollowColorG = (byte)g;
                robot.FollowColorB = (byte)b;

                _logger.LogInformation("Updated robot '{RobotName}' follow color to RGB({R},{G},{B})", robotName, r, g, b);

                return Json(new {
                    success = true,
                    message = $"Robot color updated to RGB({r},{g},{b}). Changes will take effect on next data exchange."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating robot color for '{RobotName}'", robotName);
                return Json(new { success = false, message = "Failed to update robot color" });
            }
        }

        // API endpoints for real-time data used by admin frontend
        [HttpGet]
        [Route("api/robots")]
        public async Task<IActionResult> GetRobotsData()
        {
            var robots = await _robotService.GetAllRobotsAsync();
            _logger.LogInformation($"Robots: {robots.Count}");
            
            // Get active requests for each robot
            var activeRequests = await _context.LaundryRequests
                .Where(r => r.AssignedRobotName != null && 
                           r.Status != RequestStatus.Completed && 
                           r.Status != RequestStatus.Cancelled && 
                           r.Status != RequestStatus.Declined)
                .ToListAsync();
            
            // Group by robot name and take the most recent request per robot to avoid duplicate keys
            var robotActiveRequests = activeRequests
                .Where(r => r.AssignedRobotName != null)
                .GroupBy(r => r.AssignedRobotName!)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.RequestedAt).First());
            
            var robotsData = robots.Select(robot => new
            {
                name = robot.Name,
                ipAddress = robot.IpAddress,
                isActive = robot.IsActive,
                canAcceptRequests = robot.CanAcceptRequests,
                status = robot.Status.ToString(),
                currentTask = robot.CurrentTask,
                currentLocation = robot.CurrentLocation,
                connectedAt = robot.ConnectedAt,
                lastPing = robot.LastPing,
                isOffline = robot.IsOffline,
                isFollowingLine = robot.IsFollowingLine,
                cameraData = robot.CameraData != null
                    ? new
                    {
                        lineDetected = robot.CameraData.LineDetected,
                        linePosition = robot.CameraData.LinePosition,
                        frameWidth = robot.CameraData.FrameWidth,
                        frameCenter = robot.CameraData.FrameCenter,
                        error = robot.CameraData.Error,
                        detectionMethod = robot.CameraData.DetectionMethod,
                        usingMemory = robot.CameraData.UsingMemory,
                        timeSinceLastLine = robot.CameraData.TimeSinceLastLine,
                        lastUpdate = robot.CameraData.LastUpdate
                    }
                    : null,
                activeRequest = robotActiveRequests.ContainsKey(robot.Name) ? new
                {
                    id = robotActiveRequests[robot.Name].Id,
                    customerId = robotActiveRequests[robot.Name].CustomerId,
                    customerName = robotActiveRequests[robot.Name].CustomerName,
                    status = robotActiveRequests[robot.Name].Status.ToString()
                } : null
            }).ToArray();
            
            return Json(robotsData);
        }

        [HttpGet]
        public async Task<IActionResult> GetRobotStatus([FromQuery] string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return BadRequest("Robot name cannot be null or empty");
            }

            var robot = await _robotService.GetRobotAsync(name);
            if (robot == null)
            {
                return NotFound();
            }

            // Map detected beacons to DTO format for JSON response
            var detectedBeacons = robot.DetectedBeacons.Values.Select(beacon => new
            {
                macAddress = beacon.MacAddress,
                beaconName = beacon.BeaconName,
                roomName = beacon.RoomName,
                currentRssi = beacon.CurrentRssi,
                distanceMeters = beacon.DistanceMeters,
                isInRange = beacon.IsInRange,
                firstDetected = beacon.FirstDetected,
                lastDetected = beacon.LastDetected,
                detectionCount = beacon.DetectionCount,
                averageRssi = beacon.AverageRssi,
                signalStrength = beacon.SignalStrength.ToString(),
                status = beacon.Status.ToString(),
                // Calculated properties for UI
                timeSinceLastDetection = DateTime.UtcNow - beacon.LastDetected,
                detectionDuration = beacon.LastDetected - beacon.FirstDetected,
                signalPercentage = Math.Max(0, Math.Min(100, (int)((beacon.CurrentRssi + 100) * 1.25))),
                signalStrengthCssClass = beacon.SignalStrength switch
                {
                    BeaconSignalStrength.Excellent => "text-emerald-400",
                    BeaconSignalStrength.Good => "text-green-400",
                    BeaconSignalStrength.Fair => "text-yellow-400",
                    BeaconSignalStrength.Weak => "text-orange-400",
                    BeaconSignalStrength.VeryWeak => "text-red-400",
                    _ => "text-slate-400"
                },
                statusCssClass = beacon.Status switch
                {
                    BeaconDetectionStatus.Active => "bg-emerald-900/50 text-emerald-300 border-emerald-700/50",
                    BeaconDetectionStatus.Lost => "bg-orange-900/50 text-orange-300 border-orange-700/50",
                    BeaconDetectionStatus.Timeout => "bg-red-900/50 text-red-300 border-red-700/50",
                    _ => "bg-slate-900/50 text-slate-300 border-slate-700/50"
                },
                statusIcon = beacon.Status switch
                {
                    BeaconDetectionStatus.Active => "check-circle",
                    BeaconDetectionStatus.Lost => "alert-circle",
                    _ => "x-circle"
                },
                signalStrengthColor = beacon.SignalStrength switch
                {
                    BeaconSignalStrength.Excellent => "bg-emerald-500",
                    BeaconSignalStrength.Good => "bg-green-500",
                    BeaconSignalStrength.Fair => "bg-yellow-500",
                    BeaconSignalStrength.Weak => "bg-orange-500",
                    _ => "bg-red-500"
                }
            }).OrderByDescending(b => b.currentRssi).ToList();

            // Calculate beacon summary stats
            var activeBeacons = detectedBeacons.Count(b => b.status == "Active");
            var lostBeacons = detectedBeacons.Count(b => b.status == "Lost");
            var timeoutBeacons = detectedBeacons.Count(b => b.status == "Timeout");

            return Json(new
            {
                name = robot.Name,
                ipAddress = robot.IpAddress,
                isActive = robot.IsActive,
                canAcceptRequests = robot.CanAcceptRequests,
                status = robot.Status.ToString(),
                currentTask = robot.CurrentTask,
                currentLocation = robot.CurrentLocation,
                connectedAt = robot.ConnectedAt,
                lastPing = robot.LastPing,
                isOffline = robot.IsOffline,
                isFollowingLine = robot.IsFollowingLine,
                weightKg = robot.WeightKg,
                usSensor1ObstacleDistance = robot.USSensor1ObstacleDistance,
                cameraData = robot.CameraData != null
                    ? new
                    {
                        lineDetected = robot.CameraData.LineDetected,
                        linePosition = robot.CameraData.LinePosition,
                        frameWidth = robot.CameraData.FrameWidth,
                        frameCenter = robot.CameraData.FrameCenter,
                        error = robot.CameraData.Error,
                        detectionMethod = robot.CameraData.DetectionMethod,
                        usingMemory = robot.CameraData.UsingMemory,
                        timeSinceLastLine = robot.CameraData.TimeSinceLastLine,
                        lastUpdate = robot.CameraData.LastUpdate
                    }
                    : null,
                // Beacon detection data
                detectedBeacons = detectedBeacons,
                beaconStats = new
                {
                    total = detectedBeacons.Count,
                    active = activeBeacons,
                    lost = lostBeacons,
                    timeout = timeoutBeacons
                }
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetRobotImage([FromQuery] string name)
        {
            _logger.LogDebug("🖼️ GetRobotImage called with name: '{Name}' (Length: {Length})", name ?? "NULL",
                name?.Length ?? -1);

            if (string.IsNullOrEmpty(name))
            {
                _logger.LogWarning("⚠️ GetRobotImage called with null or empty name");
                return BadRequest("Robot name cannot be null or empty");
            }

            var robot = await _robotService.GetRobotAsync(name);
            if (robot?.CameraData?.ImageData == null || robot.CameraData.ImageData.Length == 0)
            {
                return NotFound("No camera image available");
            }

            return File(robot.CameraData.ImageData, "image/jpeg");
        }
    }
}