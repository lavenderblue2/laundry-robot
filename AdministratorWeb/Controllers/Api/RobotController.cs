using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdministratorWeb.Data;
using AdministratorWeb.Services;
using AdministratorWeb.Models;
using RobotProject.Shared.DTOs;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace AdministratorWeb.Controllers.Api
{
    /// <summary>
    /// ROBOT API CONTROLLER - This handles API endpoints used by actual robots
    /// 
    /// IMPORTANT: This is NOT for admin frontend views. For admin robot management UI,
    /// see Controllers/RobotsController.cs (plural)
    /// 
    /// AUTHENTICATION: This controller MUST be [AllowAnonymous] - robots communicate without authentication
    /// Do NOT add [Authorize] here as it will break robot communication
    /// 
    /// This API controller provides:
    /// - Two-way data exchange endpoint for robots (/api/Robot/{name}/data-exchange)
    /// - Robot status reporting endpoints
    /// - Server configuration distribution to robots
    /// - Real-time beacon detection data collection
    /// </summary>
    [AllowAnonymous]
    [Route("api/[controller]")]
    [ApiController]
    public class RobotController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RobotController> _logger;
        private readonly IRobotManagementService _robotService;

        public RobotController(ApplicationDbContext context, ILogger<RobotController> logger,
            IRobotManagementService robotService)
        {
            _context = context;
            _logger = logger;
            _robotService = robotService;
        }

        /// <summary>
        /// Robot registration endpoint - robots call this to register themselves
        /// </summary>
        [HttpPost("{name}/register")]
        public async Task<ActionResult> RegisterRobot(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return BadRequest(new { error = "Robot name is required" });
                }

                // Get the real client IP address from Cloudflare headers
                var ipAddress = GetClientIpAddress();

                var success = await _robotService.RegisterRobotAsync(name, ipAddress);
                if (success)
                {
                    _logger.LogInformation("Robot '{RobotName}' registered from IP {IpAddress}", name, ipAddress);
                    return Ok(new { message = "Robot registered successfully", name, ipAddress });
                }

                return StatusCode(500, new { error = "Failed to register robot" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering robot '{RobotName}'", name);
                return StatusCode(500, new { error = $"Registration failed: {ex.Message}" });
            }
        }

        /// <summary>
        /// Robot ping endpoint - robots call this to update their last seen time
        /// </summary>
        [HttpPost("{name}/ping")]
        public async Task<ActionResult> PingRobot(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return BadRequest(new { error = "Robot name is required" });
                }

                // Get the real client IP address from Cloudflare headers
                var ipAddress = GetClientIpAddress();
                var success = await _robotService.PingRobotAsync(name, ipAddress);

                if (success)
                {
                    return Ok(new { message = "Ping successful", name, timestamp = DateTime.UtcNow });
                }

                return NotFound(new { error = "Robot not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pinging robot '{RobotName}'", name);
                return StatusCode(500, new { error = $"Ping failed: {ex.Message}" });
            }
        }

        /// <summary>
        /// Two-way data exchange endpoint for robots
        /// Robots POST their data and receive current server configuration
        /// </summary>
        [HttpPost("{name}/data-exchange")] 
        public async Task<ActionResult<RobotDataExchangeResponse>> DataExchange(
            string name,
            [FromBody] RobotDataExchangeRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return BadRequest(new { error = "Robot name is required" });
                }

                if (request == null)
                {
                    return BadRequest(new { error = "Request data is required" });
                }

                // Validate robot name matches request
                if (!string.Equals(name, request.RobotName, StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new { error = "Robot name mismatch between URL and request body" });
                }

                _logger.LogInformation(
                    "Data exchange request from robot '{RobotName}' with {BeaconCount} detected beacons, IsInTarget: {IsInTarget}",
                    name, request.DetectedBeacons?.Count ?? 0, request.IsInTarget);

                // Process detected beacon data from robot  
                await ProcessDetectedBeacons(name, request.DetectedBeacons);

                // Update robot's in-memory beacon detection data
                await _robotService.UpdateRobotDetectedBeaconsAsync(name,
                    request.DetectedBeacons ?? new List<RobotProject.Shared.DTOs.BeaconInfo>());

                // Update robot weight data
                await UpdateRobotWeightData(name, request.WeightKg);

                // Update robot ultrasonic sensor data
                await UpdateRobotUltrasonicData(name, request.USSensor1ObstacleDistance);

                // Handle robot arrival at target
                if (request.IsInTarget)
                {
                    _logger.LogInformation("ROBOT IS AT TARGET!");
                    await HandleRobotArrivedAtTarget(name);
                }

                // Get current active beacons for robot
                var activeBeacons = await GetActiveBeaconsForRobot();

                // Get current server configuration
                var serverConfig = await GetServerConfiguration();

                // Get robot navigation status
                var (robotStatus, atUserRoom) = await GetRobotNavigationStatus(name);

                // Get target room from active request IF there is an active target to go to
                var targetRoomName = await GetTargetRoomForRobot(name);

                // Get robot follow color from in-memory storage
                var followColor = await GetRobotFollowColor(name);

                // Get target room floor color
                var stopAtColor = await GetTargetRoomFloorColor(targetRoomName);

                // FIXED: Only set navigation targets if robot has NOT reached target
                if (!request.IsInTarget)
                {
                    if (!string.IsNullOrWhiteSpace(targetRoomName)) // if we have a target
                        await SetNavigationTargetForRoomBeacons(activeBeacons, targetRoomName);
                }
                else
                {
                    // Clear all navigation targets since robot has reached destination
                    foreach (var beacon in activeBeacons)
                    {
                        beacon.IsNavigationTarget = false;
                    }

                    _logger.LogInformation("Robot {RobotName} reached target - clearing all navigation targets", name);
                }

                // Determine if robot should be line following
                bool isLineFollowing = false;

                // Check if we have a cancelled request that needs to return to base
                var cancelledRequest = await _context.LaundryRequests
                    .FirstOrDefaultAsync(r => r.AssignedRobotName == name && r.Status == RequestStatus.Cancelled);

                if (request.IsInTarget && cancelledRequest == null)
                {
                    // Robot has reached target - stop line following (unless request is cancelled)
                    isLineFollowing = false;
                    _logger.LogInformation("Robot {RobotName} is in target - stopping line following", name);
                }
                else if (cancelledRequest != null)
                {
                    // Request was cancelled - check if robot needs to return to base OR is already there
                    if (request.IsInTarget && targetRoomName == "Base")
                    {
                        // Robot is already at base - STOP line following and complete the cancellation
                        isLineFollowing = false;
                        _logger.LogInformation("Robot {RobotName} has CANCELLED request and is now at Base - stopping line following", name);

                        // Mark the cancelled request as completed so it doesn't keep the robot moving
                        cancelledRequest.Status = RequestStatus.Completed;
                        cancelledRequest.CompletedAt = DateTime.UtcNow;
                        cancelledRequest.DeclineReason = (cancelledRequest.DeclineReason ?? "") + " [Robot returned to base]";
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        // Robot is NOT at base yet - continue line following to return to base
                        isLineFollowing = true;
                        _logger.LogWarning("Robot {RobotName} has CANCELLED request - must return to base (IsInTarget: {IsInTarget})",
                            name, request.IsInTarget);
                    }
                }
                else
                {
                    // FIRST: Check if admin manually set line following via "Follow Line" button
                    var robotState = await _robotService.GetRobotAsync(name);
                    bool manualLineFollowing = robotState?.IsFollowingLine ?? false;

                    // SECOND: Check if there's an active request assigned to this robot
                    var activeRequest = await _context.LaundryRequests
                        .FirstOrDefaultAsync(r => r.AssignedRobotName == name &&
                                                  (r.Status == RequestStatus.Accepted ||
                                                   r.Status == RequestStatus.LaundryLoaded ||
                                                   r.Status == RequestStatus.FinishedWashingGoingToRoom ||
                                                   r.Status == RequestStatus.FinishedWashingGoingToBase ||
                                                   r.Status == RequestStatus.Cancelled)); // Cancelled requests need robot to return to base

                    // Robot should line follow if EITHER manual flag is set OR there's an active request
                    isLineFollowing = manualLineFollowing || activeRequest != null;

                    if (manualLineFollowing && activeRequest == null)
                    {
                        _logger.LogInformation(
                            "Robot {RobotName} should line follow - MANUAL mode (Follow Line button pressed)", name);
                    }
                    else if (activeRequest != null)
                    {
                        _logger.LogInformation(
                            "Robot {RobotName} should line follow - active request #{RequestId} found", name, activeRequest.Id);
                    }
                    else
                    {
                        _logger.LogInformation("Robot {RobotName} should stop line following - no active request or manual command",
                            name);
                    }
                }

                // Get laundry settings for weight limits
                var laundrySettings = await _context.LaundrySettings.FirstOrDefaultAsync();

                // Create response with current server state
                var response = new RobotDataExchangeResponse
                {
                    Timestamp = DateTime.UtcNow,
                    ActiveBeacons = activeBeacons,
                    Configuration = serverConfig,
                    IsLineFollowing = isLineFollowing,
                    RobotStatus = robotStatus,
                    AtUserRoom = atUserRoom,
                    FollowColor = followColor,
                    StopAtColor = stopAtColor,
                    MaxWeightKg = laundrySettings?.MaxWeightPerRequest,
                    MinWeightKg = laundrySettings?.MinWeightPerRequest,
                    Success = true,
                    Messages = new List<string> { "Data exchange successful" },
                    DataExchangeIntervalSeconds = 1
                };

                _logger.LogInformation(
                    "Sending {ActiveBeaconCount} active beacons to robot '{RobotName}', IsLineFollowing: {IsLineFollowing}, NavigationTargets: {NavigationTargets}",
                    activeBeacons.Count, name, isLineFollowing, activeBeacons.Count(b => b.IsNavigationTarget));

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data exchange with robot '{RobotName}'", name);
                return StatusCode(500, new RobotDataExchangeResponse
                {
                    Timestamp = DateTime.UtcNow,
                    Success = false,
                    Messages = new List<string> { $"Server error during data exchange: {ex.Message}" },
                    ActiveBeacons = new List<BeaconConfigurationDto>(),
                    Configuration = null,
                    IsLineFollowing = false
                });
            }
        }

        /// <summary>
        /// Process beacon detection data from robot
        /// </summary>
        private async Task ProcessDetectedBeacons(string robotName, List<BeaconInfo>? detectedBeacons)
        {
            if (detectedBeacons == null || !detectedBeacons.Any())
                return;

            var timestamp = DateTime.UtcNow;

            foreach (var beaconInfo in detectedBeacons)
            {
                if (string.IsNullOrWhiteSpace(beaconInfo.MacAddress))
                    continue;

                // Find matching beacon in database
                var beacon = await _context.BluetoothBeacons
                    .FirstOrDefaultAsync(b => b.MacAddress.ToUpper() == beaconInfo.MacAddress.ToUpper());

                if (beacon != null)
                {
                    // Update detection information
                    beacon.LastSeenAt = timestamp;
                    beacon.LastSeenByRobot = robotName;
                    beacon.LastRecordedRssi = beaconInfo.Rssi;
                    beacon.UpdatedAt = timestamp;
                    beacon.UpdatedBy = $"Robot:{robotName}";

                    _logger.LogDebug(
                        "Updated beacon '{BeaconName}' detection from robot '{RobotName}' with RSSI {Rssi}",
                        beacon.Name, robotName, beaconInfo.Rssi);
                }
                else
                {
                    _logger.LogWarning("Robot '{RobotName}' detected unknown beacon with MAC '{MacAddress}'",
                        robotName, beaconInfo.MacAddress);
                }
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Get all active beacons that robots should track
        /// </summary>
        private async Task<List<BeaconConfigurationDto>> GetActiveBeaconsForRobot()
        {
            var activeBeacons = await _context.BluetoothBeacons
                .Where(b => b.IsActive)
                .OrderBy(b => b.RoomName)
                .ThenBy(b => b.Name)
                .Select(b => new BeaconConfigurationDto
                {
                    MacAddress = b.MacAddress,
                    Name = b.Name,
                    RoomName = b.RoomName,
                    RssiThreshold = b.RssiThreshold,
                    IsActive = b.IsActive,
                    IsNavigationTarget = b.IsNavigationTarget,
                    Priority = b.Priority
                })
                .ToListAsync();

            return activeBeacons;
        }

        /// <summary>
        /// Get current server configuration for robots
        /// </summary>
        private async Task<Dictionary<string, object>?> GetServerConfiguration()
        {
            // Get detection mode from settings
            var settings = await _context.LaundrySettings.FirstOrDefaultAsync();
            var detectionMode = settings?.DetectionMode ?? RoomDetectionMode.Beacon;

            // This can be expanded to include various server settings
            var config = new Dictionary<string, object>
            {
                ["timestamp"] = DateTime.UtcNow.ToString("O"),
                ["server_version"] = "1.0.0",
                ["beacon_scan_interval_ms"] = 100,
                ["data_exchange_interval_ms"] = 1000,
                ["rssi_default_threshold"] = -20,
                ["navigation_enabled"] = true,
                ["total_active_beacons"] = await _context.BluetoothBeacons.CountAsync(b => b.IsActive),
                ["total_navigation_targets"] =
                    await _context.BluetoothBeacons.CountAsync(b => b.IsActive && b.IsNavigationTarget),
                ["room_detection_mode"] = detectionMode.ToString().ToLower() // "beacon" or "color"
            };

            return config;
        }

        /// <summary>
        /// Get target room name for robot from active request and customer
        /// </summary>
        private async Task<string?> GetTargetRoomForRobot(string robotName)
        {
            try
            {
                var activeRequest = await _context.LaundryRequests
                    .FirstOrDefaultAsync(r => r.AssignedRobotName == robotName &&
                                              (r.Status == RequestStatus
                                                   .Accepted || // this will make the robot go to customer room
                                               r.Status == RequestStatus
                                                   .LaundryLoaded || // this will make the robot go to base
                                               r.Status == RequestStatus
                                                   .FinishedWashingGoingToRoom || // makes the robot go to customer
                                               r.Status == RequestStatus.FinishedWashingGoingToBase || // go to base
                                               r.Status == RequestStatus.Cancelled)); // cancelled - robot returns to base

                if (activeRequest == null)
                {
                    _logger.LogInformation("No active request found for robot {RobotName}", robotName);
                    return null;
                }

                // Get the customer who made the request
                if (string.IsNullOrEmpty(activeRequest.CustomerId))
                {
                    _logger.LogWarning("Active request {RequestId} for robot {RobotName} has null/empty CustomerId",
                        activeRequest.Id, robotName);
                    return null;
                }

                var customer = await _context.Users.FindAsync(activeRequest.CustomerId);

                if (activeRequest.Status == RequestStatus.LaundryLoaded ||
                    activeRequest.Status == RequestStatus.FinishedWashingGoingToBase ||
                    activeRequest.Status == RequestStatus.Cancelled)
                {
                    // Robot needs to return to base
                    _logger.LogInformation(
                        "Target room lookup for robot {RobotName}: Returning to Base (Request #{RequestId}, Status: {Status})",
                        robotName, activeRequest.Id, activeRequest.Status);
                    return "Base";
                }

                if (activeRequest.Status == RequestStatus.FinishedWashingGoingToRoom)
                {
                    // Robot needs to go to customer room for delivery
                    if (customer == null)
                    {
                        _logger.LogWarning("Customer {CustomerId} not found for delivery request {RequestId}",
                            activeRequest.CustomerId, activeRequest.Id);
                        return null;
                    }

                    _logger.LogInformation(
                        "Target room lookup for robot {RobotName}: Delivering to customer room {RoomName} (Request #{RequestId})",
                        robotName, customer.RoomName, activeRequest.Id);
                    return customer.RoomName;
                }


                if (customer == null)
                {
                    _logger.LogWarning("Customer {CustomerId} not found for request {RequestId}",
                        activeRequest.CustomerId, activeRequest.Id);
                    return null;
                }

                var targetRoomName = customer.RoomName;

                _logger.LogInformation(
                    "Target room lookup for robot {RobotName}: Found request {RequestId}, customer {CustomerId}, target room: {TargetRoom}",
                    robotName, activeRequest.Id, activeRequest.CustomerId, targetRoomName ?? "None");

                return targetRoomName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting target room for robot {RobotName}", robotName);
                return null;
            }
        }

        /// <summary>
        /// Set IsNavigationTarget property for beacons based on robot's target room
        /// </summary>
        private async Task SetNavigationTargetForRoomBeacons(List<BeaconConfigurationDto> beacons,
            string? targetRoomName)
        {
            await Task.Run(() =>
            {
                foreach (var beacon in beacons)
                {
                    // Set IsNavigationTarget to true for ALL beacons in the target room
                    beacon.IsNavigationTarget = !string.IsNullOrEmpty(targetRoomName) &&
                                                string.Equals(beacon.RoomName, targetRoomName,
                                                    StringComparison.OrdinalIgnoreCase);
                }

                var targetBeaconCount = beacons.Count(b => b.IsNavigationTarget);
                _logger.LogInformation(
                    "Navigation targets set: {TargetBeaconCount} beacons in room '{TargetRoom}' marked as navigation targets among {TotalBeaconCount} beacons",
                    targetBeaconCount, targetRoomName ?? "None", beacons.Count);
            });
        }

        /// <summary>
        /// Get robot status information
        /// </summary>
        [HttpGet("{name}/status")]
        public async Task<ActionResult> GetRobotStatus(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { error = "Robot name is required" });
            }

            try
            {
                // Get recent beacon detections by this robot
                var recentDetections = await _context.BluetoothBeacons
                    .Where(b => b.LastSeenByRobot == name &&
                                b.LastSeenAt.HasValue &&
                                b.LastSeenAt.Value > DateTime.UtcNow.AddHours(-1))
                    .OrderByDescending(b => b.LastSeenAt)
                    .Select(b => new
                    {
                        b.Name,
                        b.RoomName,
                        b.MacAddress,
                        b.LastSeenAt,
                        b.LastRecordedRssi
                    })
                    .ToListAsync();

                var status = new
                {
                    robot_name = name,
                    timestamp = DateTime.UtcNow,
                    recent_detections = recentDetections,
                    detection_count = recentDetections.Count,
                    active_beacon_count = await _context.BluetoothBeacons.CountAsync(b => b.IsActive),
                    last_communication = recentDetections.FirstOrDefault()?.LastSeenAt
                };

                return Ok(status);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting status for robot '{RobotName}'", name);
                return StatusCode(500, new { error = "Failed to get robot status" });
            }
        }

        /// <summary>
        /// Upload camera image from robot
        /// </summary>
        [HttpPost("{name}/upload-image")]
        public async Task<ActionResult> UploadImage(
            string name,
            [FromForm] IFormFile image,
            [FromForm] string? metadata = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return BadRequest(new { error = "Robot name is required" });
                }

                if (image == null || image.Length == 0)
                {
                    return BadRequest(new { error = "Image file is required" });
                }

                // Validate image type
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png" };
                if (!allowedTypes.Contains(image.ContentType.ToLower()))
                {
                    return BadRequest(new { error = "Only JPEG and PNG images are allowed" });
                }

                // Size limit (10MB)
                if (image.Length > 10 * 1024 * 1024)
                {
                    return BadRequest(new { error = "Image size cannot exceed 10MB" });
                }

                // Read image data into byte array
                byte[] imageData;
                using (var memoryStream = new MemoryStream())
                {
                    await image.CopyToAsync(memoryStream);
                    imageData = memoryStream.ToArray();
                }

                // Parse metadata if provided
                Dictionary<string, object>? imageMetadata = null;
                if (!string.IsNullOrEmpty(metadata))
                {
                    try
                    {
                        imageMetadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metadata);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse image metadata from robot '{RobotName}'", name);
                    }
                }

                // Update robot's camera data in memory
                await UpdateRobotCameraImage(name, imageData, imageMetadata);

                return Ok(new
                {
                    message = "Image uploaded successfully",
                    robotName = name,
                    size = image.Length,
                    timestamp = DateTime.UtcNow,
                    metadata = imageMetadata
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading image from robot '{RobotName}'", name);
                return StatusCode(500, new { error = $"Image upload failed: {ex.Message}" });
            }
        }

        /// <summary>
        /// Get latest camera image from specific robot
        /// </summary>
        [HttpGet("{name}/latest-image")]
        public async Task<ActionResult> GetLatestImage(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return BadRequest(new { error = "Robot name is required" });
                }

                // Get robot camera data from robot management service
                var robot = await _robotService.GetRobotAsync(name);
                if (robot?.CameraData?.ImageData == null)
                {
                    return NotFound(new { error = "No camera image available for this robot" });
                }

                var imageData = robot.CameraData.ImageData;
                var contentType = "image/jpeg"; // Default to JPEG

                Response.Headers.Add("X-Image-Timestamp", robot.CameraData.LastUpdate.ToString("O"));
                Response.Headers.Add("X-Robot-Name", name);

                return File(imageData, contentType, $"{name}_latest.jpg");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving latest image for robot '{RobotName}'", name);
                return StatusCode(500, new { error = "Failed to retrieve image" });
            }
        }

        /// <summary>
        /// Update robot's camera image data in memory
        /// </summary>
        private async Task UpdateRobotCameraImage(string robotName, byte[] imageData,
            Dictionary<string, object>? metadata)
        {
            try
            {
                // Get or create robot record
                var robot = await _robotService.GetRobotAsync(robotName);
                if (robot != null)
                {
                    // Initialize camera data if it doesn't exist
                    robot.CameraData ??= new RobotCameraData();

                    // Update image data
                    robot.CameraData.ImageData = imageData;
                    robot.CameraData.LastUpdate = DateTime.UtcNow;

                    // Update robot's last ping time since it's actively sending data
                    robot.LastPing = DateTime.UtcNow;

                    _logger.LogDebug("Updated robot '{RobotName}' camera image: {ImageSize} bytes",
                        robotName, imageData.Length);

                    if (metadata != null)
                    {
                        _logger.LogDebug("Image metadata: {Metadata}", JsonSerializer.Serialize(metadata));
                    }
                }
                else
                {
                    _logger.LogWarning("Robot '{RobotName}' not found when updating camera image", robotName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating robot camera image for '{RobotName}'", robotName);
            }
        }

        /// <summary>
        /// Get all robots that have communicated recently
        /// </summary>
        [HttpGet("active")]
        public async Task<ActionResult> GetActiveRobots()
        {
            try
            {
                var activeRobots = await _context.BluetoothBeacons
                    .Where(b => !string.IsNullOrEmpty(b.LastSeenByRobot) &&
                                b.LastSeenAt.HasValue &&
                                b.LastSeenAt.Value > DateTime.UtcNow.AddMinutes(-5))
                    .GroupBy(b => b.LastSeenByRobot)
                    .Select(g => new
                    {
                        robot_name = g.Key,
                        last_communication = g.Max(b => b.LastSeenAt),
                        detected_beacons = g.Count(),
                        detected_rooms = g.Select(b => b.RoomName).Distinct().Count()
                    })
                    .OrderByDescending(r => r.last_communication)
                    .ToListAsync();

                return Ok(new
                {
                    timestamp = DateTime.UtcNow,
                    active_robot_count = activeRobots.Count,
                    robots = activeRobots
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active robots list");
                return StatusCode(500, new { error = "Failed to get active robots" });
            }
        }

        /// <summary>
        /// Handle robot arrival at target destination
        /// Updates request status to ArrivedAtRoom and stops navigation
        /// </summary>
        private async Task HandleRobotArrivedAtTarget(string robotName)
        {
            try
            {
                var activeRequest = await _context.LaundryRequests
                    .FirstOrDefaultAsync(r => r.AssignedRobotName == robotName &&
                                              (r.Status == RequestStatus.Accepted ||
                                               r.Status == RequestStatus.LaundryLoaded ||
                                               r.Status == RequestStatus.FinishedWashingGoingToRoom ||
                                               r.Status == RequestStatus.FinishedWashingGoingToBase ||
                                               r.Status == RequestStatus.Cancelled));

                if (activeRequest != null)
                {
                    _logger.LogInformation($"current request status: {activeRequest.Status}");
                    if (activeRequest.Status == RequestStatus.Accepted)
                    {
                        // Robot has arrived at customer room
                        activeRequest.Status = RequestStatus.ArrivedAtRoom;
                        activeRequest.ArrivedAtRoomAt = DateTime.UtcNow;

                        // Update robot status
                        var robot = await _robotService.GetRobotAsync(robotName);
                        if (robot != null)
                        {
                            robot.CurrentTask = "Arrived at customer room - waiting for laundry loading";
                            robot.Status = RobotStatus.Busy; // Still busy but not moving
                        }

                        _logger.LogInformation("Robot {RobotName} has arrived at customer room for request {RequestId}",
                            robotName, activeRequest.Id);
                    }
                    else if (activeRequest.Status == RequestStatus.LaundryLoaded)
                    {
                        // Robot has returned to base, laundry is now washing
                        activeRequest.Status = RequestStatus.Washing;
                        activeRequest.ReturnedToBaseAt = DateTime.UtcNow;

                        // Update robot status - now available for new requests
                        var robot = await _robotService.GetRobotAsync(robotName);
                        if (robot != null)
                        {
                            robot.CurrentTask = "Returned to base - laundry is washing";
                            robot.Status = RobotStatus.Available; // Now available
                        }

                        _logger.LogInformation(
                            "Robot {RobotName} has returned to base for request {RequestId} - laundry is washing",
                            robotName, activeRequest.Id);

                        // AUTO-QUEUE: Process next pending request if auto-accept is enabled
                        await ProcessNextPendingRequestInQueueAsync(robot);
                    }
                    else if (activeRequest.Status == RequestStatus.FinishedWashingGoingToRoom)
                    {
                        // Robot has arrived at customer room for delivery
                        activeRequest.Status = RequestStatus.FinishedWashingArrivedAtRoom;
                        activeRequest.ArrivedAtRoomAt = DateTime.UtcNow; // UPDATE: Set new arrival timestamp for delivery

                        var robot = await _robotService.GetRobotAsync(robotName);
                        if (robot != null)
                        {
                            robot.CurrentTask = "Arrived at customer room - waiting for laundry unloading";
                            robot.Status = RobotStatus.Busy;
                        }

                        _logger.LogInformation(
                            "Robot {RobotName} has arrived at customer room for delivery (Request #{RequestId})",
                            robotName, activeRequest.Id);
                    }
                    else if (activeRequest.Status == RequestStatus.FinishedWashingGoingToBase)
                    {
                        // Robot has returned to base with clean laundry - waiting for admin to complete
                        activeRequest.Status = RequestStatus.FinishedWashingAtBase;

                        var robot = await _robotService.GetRobotAsync(robotName);
                        if (robot != null)
                        {
                            robot.CurrentTask = "Returned to base with clean laundry - awaiting admin completion";
                            robot.Status = RobotStatus.Available;
                        }

                        _logger.LogInformation(
                            "Robot {RobotName} has returned to base with clean laundry (Request #{RequestId}) - awaiting admin completion",
                            robotName, activeRequest.Id);
                    }
                    else if (activeRequest.Status == RequestStatus.Cancelled)
                    {
                        // Robot has returned to base after request cancellation (timeout or manual cancel)
                        // Clear robot assignment and mark robot as available
                        _logger.LogWarning(
                            "Robot {RobotName} has returned to base with CANCELLED request #{RequestId} - clearing assignment",
                            robotName, activeRequest.Id);

                        activeRequest.AssignedRobotName = null; // Clear robot assignment
                        activeRequest.ProcessedAt = DateTime.UtcNow;

                        var robot = await _robotService.GetRobotAsync(robotName);
                        if (robot != null)
                        {
                            robot.CurrentTask = "Returned to base after request cancellation";
                            robot.Status = RobotStatus.Available; // Now available for new requests
                        }

                        _logger.LogInformation(
                            "Robot {RobotName} is now available after handling cancelled request #{RequestId}",
                            robotName, activeRequest.Id);

                        // AUTO-QUEUE: Process next pending request if auto-accept is enabled
                        await ProcessNextPendingRequestInQueueAsync(robot);
                    }

                    await _context.SaveChangesAsync();
                }
                else
                {
                    _logger.LogWarning("Robot {RobotName} reported arrival but no active request found", robotName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling robot {RobotName} arrival at target", robotName);
            }
        }

        /// <summary>
        /// Get robot's current navigation status
        /// </summary>
        private async Task<(string? status, bool atUserRoom)> GetRobotNavigationStatus(string robotName)
        {
            try
            {
                var activeRequest = await _context.LaundryRequests
                    .FirstOrDefaultAsync(r => r.AssignedRobotName == robotName &&
                                              r.Status == RequestStatus.Accepted);

                if (activeRequest == null)
                    return ("Available", false);

                // Check if robot is at user's room by checking beacon proximity
                if (!string.IsNullOrEmpty(activeRequest.AssignedBeaconMacAddress))
                {
                    var robot = await _robotService.GetRobotAsync(robotName);
                    if (robot?.DetectedBeacons != null)
                    {
                        var targetBeacon = robot.DetectedBeacons.Values.FirstOrDefault(b =>
                            string.Equals(b.MacAddress, activeRequest.AssignedBeaconMacAddress,
                                StringComparison.OrdinalIgnoreCase));

                        if (targetBeacon != null && targetBeacon.CurrentRssi >= -35)
                        {
                            return ($"At {targetBeacon.RoomName}", true);
                        }
                    }
                }

                return ($"Going to {await GetRoomNameForBeacon(activeRequest.AssignedBeaconMacAddress)}", false);
            }
            catch
            {
                return ("Unknown", false);
            }
        }

        /// <summary>
        /// Get room name for a beacon MAC address
        /// </summary>
        private async Task<string> GetRoomNameForBeacon(string beaconMacAddress)
        {
            try
            {
                var beacon = await _context.BluetoothBeacons
                    .FirstOrDefaultAsync(b => b.MacAddress.ToUpper() == beaconMacAddress.ToUpper());
                return beacon?.RoomName ?? "Unknown Room";
            }
            catch
            {
                return "Unknown Room";
            }
        }

        /// <summary>
        /// Update robot's weight sensor reading and assigned request weight
        /// </summary>
        private async Task UpdateRobotWeightData(string robotName, double weightKg)
        {
            try
            {
                var robot = await _robotService.GetRobotAsync(robotName);
                if (robot != null)
                {
                    robot.WeightKg = weightKg;
                    _logger.LogDebug("Updated weight for robot {RobotName}: {WeightKg}kg", robotName, weightKg);

                    // Find ALL active requests assigned to this robot that are in ArrivedAtRoom status
                    var arrivedRequests = await _context.LaundryRequests
                        .Where(r => r.AssignedRobotName.ToLower() == robotName.ToLower() &&
                                    r.Status == RequestStatus.ArrivedAtRoom)
                        .ToListAsync();

                    if (arrivedRequests.Any())
                    {
                        foreach (var request in arrivedRequests)
                        {
                            request.Weight = (decimal)weightKg;
                            _logger.LogDebug("Updated weight for ArrivedAtRoom request {RequestId}: {WeightKg}kg",
                                request.Id, weightKg);
                        }

                        await _context.SaveChangesAsync();
                        _logger.LogInformation(
                            "Updated weight for {Count} ArrivedAtRoom requests for robot {RobotName}: {WeightKg}kg",
                            arrivedRequests.Count, robotName, weightKg);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating weight data for robot {RobotName}", robotName);
            }
        }

        /// <summary>
        /// Update robot's ultrasonic sensor distance reading
        /// </summary>
        private async Task UpdateRobotUltrasonicData(string robotName, double distance)
        {
            try
            {
                var robot = await _robotService.GetRobotAsync(robotName);
                if (robot != null)
                {
                    robot.USSensor1ObstacleDistance = distance;
                    _logger.LogDebug("Updated ultrasonic distance for robot {RobotName}: {Distance}m", robotName, distance);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating ultrasonic data for robot {RobotName}", robotName);
            }
        }

        /// <summary>
        /// Get robot's follow color from database settings (persists across restarts)
        /// </summary>
        private async Task<byte[]?> GetRobotFollowColor(string robotName)
        {
            try
            {
                // Get color from LaundrySettings database (persists across restarts)
                var settings = await _context.LaundrySettings.FirstOrDefaultAsync();
                if (settings != null)
                {
                    var colorBytes = new byte[] { settings.LineFollowColorR, settings.LineFollowColorG, settings.LineFollowColorB };
                    _logger.LogDebug("Robot '{RobotName}' follow color from settings: RGB({R},{G},{B})",
                        robotName, colorBytes[0], colorBytes[1], colorBytes[2]);
                    return colorBytes;
                }

                _logger.LogDebug("Robot '{RobotName}' no settings found, using default black color", robotName);
                return new byte[] { 0, 0, 0 }; // Default to black
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting follow color for robot '{RobotName}', using default", robotName);
                return new byte[] { 0, 0, 0 }; // Default to black
            }
        }

        /// <summary>
        /// Get target room's floor color from database
        /// </summary>
        private async Task<byte[]?> GetTargetRoomFloorColor(string? targetRoomName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(targetRoomName))
                {
                    return null;
                }

                var room = await _context.Rooms.FirstOrDefaultAsync(r => r.Name == targetRoomName);
                if (room == null)
                {
                    _logger.LogDebug("Target room '{RoomName}' not found in database", targetRoomName);
                    return null;
                }

                var floorColor = room.FloorColorRgb;
                if (floorColor != null)
                {
                    _logger.LogDebug("Target room '{RoomName}' floor color: RGB({R},{G},{B})",
                        targetRoomName, floorColor[0], floorColor[1], floorColor[2]);
                }
                else
                {
                    _logger.LogDebug("Target room '{RoomName}' has no floor color set (beacon-only navigation)", targetRoomName);
                }

                return floorColor;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting floor color for room '{RoomName}'", targetRoomName);
                return null;
            }
        }

        /// <summary>
        /// Auto-process next pending request in queue when robot becomes available
        /// </summary>
        private async Task ProcessNextPendingRequestInQueueAsync(ConnectedRobot? robot)
        {
            try
            {
                if (robot == null)
                {
                    _logger.LogWarning("Cannot process next pending request - robot is null");
                    return;
                }

                // Check if auto-accept is enabled
                var settings = await _context.LaundrySettings.FirstOrDefaultAsync();
                if (settings?.AutoAcceptRequests != true)
                {
                    _logger.LogDebug("Auto-accept is disabled, skipping queue processing");
                    return;
                }

                // Find the oldest pending request
                var nextRequest = await _context.LaundryRequests
                    .Where(r => r.Status == RequestStatus.Pending)
                    .OrderBy(r => r.RequestedAt)
                    .FirstOrDefaultAsync();

                if (nextRequest == null)
                {
                    _logger.LogInformation("No pending requests in queue - robot {RobotName} will remain idle", robot.Name);
                    return;
                }

                // Accept the queued request
                nextRequest.Status = RequestStatus.Accepted;
                nextRequest.AcceptedAt = DateTime.UtcNow;
                nextRequest.ProcessedAt = DateTime.UtcNow;
                nextRequest.AssignedRobotName = robot.Name;

                // Update robot status
                robot.Status = RobotStatus.Busy;
                robot.CurrentTask = $"Handling queued request #{nextRequest.Id}";

                await _context.SaveChangesAsync();

                // Start robot line following
                var lineFollowingStarted = await _robotService.SetLineFollowingAsync(robot.Name, true);

                if (!lineFollowingStarted)
                {
                    _logger.LogWarning("Failed to start line following for robot {RobotName} on queued request", robot.Name);
                }

                _logger.LogInformation(
                    "🚀 AUTO-QUEUE: Queued request #{RequestId} (customer: {CustomerId}) automatically accepted and assigned to robot {RobotName}",
                    nextRequest.Id, nextRequest.CustomerId, robot.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing next pending request from queue");
            }
        }

        /// <summary>
        /// Get real client IP address from Cloudflare headers or fallback methods
        /// </summary>
        private string GetClientIpAddress()
        {
            try
            {
                // Cloudflare headers (in order of preference)
                var cloudflareIp = HttpContext.Request.Headers["CF-Connecting-IP"].FirstOrDefault();
                if (!string.IsNullOrEmpty(cloudflareIp))
                {
                    _logger.LogDebug("Using Cloudflare IP: {IP}", cloudflareIp);
                    return cloudflareIp;
                }

                // X-Forwarded-For header (common proxy header)
                var forwardedFor = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(forwardedFor))
                {
                    // X-Forwarded-For can contain multiple IPs, take the first (original client)
                    var clientIp = forwardedFor.Split(',')[0].Trim();
                    _logger.LogDebug("Using X-Forwarded-For IP: {IP}", clientIp);
                    return clientIp;
                }

                // X-Real-IP header (nginx and other proxies)
                var realIp = HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
                if (!string.IsNullOrEmpty(realIp))
                {
                    _logger.LogDebug("Using X-Real-IP: {IP}", realIp);
                    return realIp;
                }

                // X-Forwarded-Proto and X-Original-For (additional fallbacks)
                var originalFor = HttpContext.Request.Headers["X-Original-For"].FirstOrDefault();
                if (!string.IsNullOrEmpty(originalFor))
                {
                    _logger.LogDebug("Using X-Original-For IP: {IP}", originalFor);
                    return originalFor;
                }

                // Fallback to connection IP
                var connectionIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
                _logger.LogDebug("Using connection IP: {IP}", connectionIp);
                return connectionIp;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting client IP address, using fallback");
                return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            }
        }
    }
}