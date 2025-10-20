using AdministratorWeb.Models;
using AdministratorWeb.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace AdministratorWeb.Services
{
    public interface IRobotManagementService
    {
        Task<bool> RegisterRobotAsync(string name, string ipAddress);
        Task<bool> PingRobotAsync(string name, string ipAddress);
        Task<bool> UpdateRobotCameraDataAsync(string name, RobotCameraData cameraData);
        Task<bool> UpdateRobotDetectedBeaconsAsync(string name, List<RobotProject.Shared.DTOs.BeaconInfo> detectedBeacons);
        Task<ConnectedRobot?> GetRobotAsync(string name);
        Task<List<ConnectedRobot>> GetAllRobotsAsync();
        Task<bool> ToggleRobotStatusAsync(string name);
        Task<bool> ToggleAcceptRequestsAsync(string name);
        Task<bool> SetRobotTaskAsync(string name, string? task);
        Task<bool> SetLineFollowingAsync(string name, bool followLine);
        Task<bool> TurnAroundAsync(string name);
        Task<bool> DisconnectRobotAsync(string name);
        Task CancelOfflineRobotRequestsAsync(); // Add method to handle offline robots
    }

    public class RobotManagementService : IRobotManagementService, IHostedService
    {  
        private readonly ConcurrentDictionary<string, ConnectedRobot> _connectedRobots = new();
        private readonly ILogger<RobotManagementService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private Timer? _offlineCheckTimer;
        private readonly TimeSpan _startupDelay = TimeSpan.FromSeconds(30); // Wait 30 seconds on startup (reduced from 2 minutes)

        public RobotManagementService(ILogger<RobotManagementService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting Robot Management Service (offline request cancellation DISABLED)");

            // DISABLED: Offline robot cancellation - was too aggressive
            // _offlineCheckTimer = new Timer(async _ => await CancelOfflineRobotRequestsAsync(),
            //     null, _startupDelay, TimeSpan.FromSeconds(30));

            await Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Robot Management Service");
            _offlineCheckTimer?.Dispose();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Cancel requests for robots that have gone offline
        /// </summary>
        public async Task CancelOfflineRobotRequestsAsync()
        {
            try
            {
                var offlineRobots = _connectedRobots.Values.Where(r => r.IsOffline).ToList();
                
                if (offlineRobots.Any())
                {
                    using var scope = _serviceProvider.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    foreach (var robot in offlineRobots)
                    {
                        // Cancel active requests for offline robots
                        var activeRequests = await context.LaundryRequests
                            .Where(r => r.AssignedRobotName == robot.Name &&
                                        r.Status != RequestStatus.Completed &&
                                        r.Status != RequestStatus.Cancelled &&
                                        r.Status != RequestStatus.Declined)
                            .ToListAsync();

                        if (activeRequests.Any())
                        {
                            foreach (var request in activeRequests)
                            {
                                request.Status = RequestStatus.Cancelled;
                                request.DeclineReason = $"Robot {robot.Name} went offline";
                                request.ProcessedAt = DateTime.UtcNow;
                            }

                            await context.SaveChangesAsync();

                            _logger.LogWarning("Cancelled {RequestCount} requests for offline robot '{RobotName}' (last ping: {LastPing})",
                                activeRequests.Count, robot.Name, robot.LastPing);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling requests for offline robots");
            }
        }

        public async Task<bool> RegisterRobotAsync(string name, string ipAddress)
        {
            if (string.IsNullOrEmpty(name))
            {
                return await Task.FromResult(false);
            }

            // Check if robot already exists (case-insensitive)
            var existingRobot = _connectedRobots.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));

            ConnectedRobot robot;
            if (existingRobot != null)
            {
                robot = existingRobot;
            }
            else
            {
                robot = new ConnectedRobot
                {
                    Name = name,
                    IpAddress = ipAddress,
                    ConnectedAt = DateTime.UtcNow,
                    LastPing = DateTime.UtcNow
                };
                _connectedRobots.TryAdd(name, robot);
            }

            // Update IP if it changed
            if (robot.IpAddress != ipAddress)
            {
                robot.IpAddress = ipAddress;
                robot.LastPing = DateTime.UtcNow;
                _logger.LogInformation("Robot {Name} updated IP address to {IP}", name, ipAddress);
            }

            var isNewRobot = robot.ConnectedAt > DateTime.UtcNow.AddSeconds(-2);

            if (isNewRobot)
            {
                _logger.LogInformation("New robot connected: {Name} from {IP}", name, ipAddress);
            }
            else
            {
                _logger.LogInformation("Robot {Name} reconnected from {IP}", name, ipAddress);
            }

            return await Task.FromResult(!isNewRobot);
        }

        public async Task<bool> PingRobotAsync(string name, string ipAddress)
        {
            if (string.IsNullOrEmpty(name))
            {
                return await Task.FromResult(false);
            }

            var robot = _connectedRobots.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            if (robot != null)
            {
                robot.LastPing = DateTime.UtcNow;
                robot.IpAddress = ipAddress; // Update IP in case it changed
                return await Task.FromResult(true);
            }

            // Robot not registered, register it now
            await RegisterRobotAsync(name, ipAddress);
            return await Task.FromResult(true);
        }

        public async Task<bool> UpdateRobotCameraDataAsync(string name, RobotCameraData cameraData)
        {
            if (string.IsNullOrEmpty(name))
            {
                return await Task.FromResult(false);
            }

            var robot = _connectedRobots.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            if (robot != null)
            {
                robot.CameraData = cameraData;
                robot.LastPing = DateTime.UtcNow; // Camera updates count as pings too
                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }

        public async Task<ConnectedRobot?> GetRobotAsync(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return await Task.FromResult<ConnectedRobot?>(null);
            }

            // Case-insensitive robot name lookup
            var robot = _connectedRobots.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            return await Task.FromResult(robot);
        }

        public async Task<List<ConnectedRobot>> GetAllRobotsAsync()
        {
            return await Task.FromResult(_connectedRobots.Values.ToList());
        }

        public async Task<bool> ToggleRobotStatusAsync(string name)
        {
            var robot = _connectedRobots.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            if (robot != null)
            {
                robot.IsActive = !robot.IsActive;
                _logger.LogInformation("Robot {Name} status toggled to {Status}", name,
                    robot.IsActive ? "Active" : "Inactive");
                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }

        public async Task<bool> ToggleAcceptRequestsAsync(string name)
        {
            var robot = _connectedRobots.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            if (robot != null)
            {
                robot.CanAcceptRequests = !robot.CanAcceptRequests;
                _logger.LogInformation("Robot {Name} accept requests toggled to {Status}", name,
                    robot.CanAcceptRequests);
                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }

        public async Task<bool> SetRobotTaskAsync(string name, string? task)
        {
            var robot = _connectedRobots.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            if (robot != null)
            {
                robot.CurrentTask = task;
                robot.Status = string.IsNullOrEmpty(task) ? RobotStatus.Available : RobotStatus.Busy;
                _logger.LogInformation("Robot {Name} task set to: {Task}", name, task ?? "None");
                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }

        public async Task<bool> SetLineFollowingAsync(string name, bool followLine)
        {
            var robot = _connectedRobots.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));

            if (robot != null)
            {
                robot.IsFollowingLine = followLine;
                robot.CurrentTask = followLine ? "Following line" : null;
                robot.Status = followLine ? RobotStatus.Busy : RobotStatus.Available;
                _logger.LogInformation("Robot {Name} line following set to: {Status}", name, followLine);

                // Persist state to database
                await PersistRobotStateAsync(robot);

                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }

        private async Task PersistRobotStateAsync(ConnectedRobot robot)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var existingState = await context.RobotStates
                    .FirstOrDefaultAsync(rs => rs.RobotName == robot.Name);

                var nearestBeacon = robot.DetectedBeacons.Values
                    .Where(b => b.IsInRange)
                    .OrderByDescending(b => b.CurrentRssi)
                    .FirstOrDefault();

                if (existingState != null)
                {
                    // Update existing state
                    existingState.IpAddress = robot.IpAddress;
                    existingState.IsActive = robot.IsActive;
                    existingState.CanAcceptRequests = robot.CanAcceptRequests;
                    existingState.Status = robot.Status;
                    existingState.CurrentTask = robot.CurrentTask;
                    existingState.CurrentLocation = robot.CurrentLocation;
                    existingState.IsFollowingLine = robot.IsFollowingLine;
                    existingState.FollowColorR = robot.FollowColorR;
                    existingState.FollowColorG = robot.FollowColorG;
                    existingState.FollowColorB = robot.FollowColorB;
                    existingState.LastUpdated = DateTime.UtcNow;
                    existingState.LastSeen = robot.LastPing;
                    existingState.LastKnownBeaconMac = nearestBeacon?.MacAddress;
                    existingState.LastKnownRoom = nearestBeacon?.RoomName;
                    existingState.LastLinePosition = robot.CameraData?.LinePosition;
                }
                else
                {
                    // Create new state
                    context.RobotStates.Add(new RobotState
                    {
                        RobotName = robot.Name,
                        IpAddress = robot.IpAddress,
                        IsActive = robot.IsActive,
                        CanAcceptRequests = robot.CanAcceptRequests,
                        Status = robot.Status,
                        CurrentTask = robot.CurrentTask,
                        CurrentLocation = robot.CurrentLocation,
                        IsFollowingLine = robot.IsFollowingLine,
                        FollowColorR = robot.FollowColorR,
                        FollowColorG = robot.FollowColorG,
                        FollowColorB = robot.FollowColorB,
                        LastUpdated = DateTime.UtcNow,
                        LastSeen = robot.LastPing,
                        LastKnownBeaconMac = nearestBeacon?.MacAddress,
                        LastKnownRoom = nearestBeacon?.RoomName,
                        LastLinePosition = robot.CameraData?.LinePosition
                    });
                }

                await context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist robot state for {RobotName}", robot.Name);
            }
        }

        public async Task<bool> TurnAroundAsync(string name)
        {
            var robot = _connectedRobots.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));

            if (robot == null)
            {
                _logger.LogWarning("Robot {Name} not found for turn around command", name);
                return false;
            }

            try
            {
                using var httpClient = new HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                var robotUrl = $"http://{robot.IpAddress}:8080/Motor/turn-around";
                _logger.LogInformation("Sending turn around command to robot {Name} at {Url}", name, robotUrl);

                var response = await httpClient.PostAsync(robotUrl, null);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Robot {Name} successfully received turn around command", name);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Robot {Name} returned status {StatusCode} for turn around command",
                        name, response.StatusCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send turn around command to robot {Name}", name);
                return false;
            }
        }

        public async Task<bool> DisconnectRobotAsync(string name)
        {
            if (_connectedRobots.TryRemove(name, out var robot))
            {
                _logger.LogInformation("Robot {Name} disconnected", name);
                return await Task.FromResult(true);
            }

            return await Task.FromResult(false);
        }

        public async Task<bool> UpdateRobotDetectedBeaconsAsync(string name,
            List<RobotProject.Shared.DTOs.BeaconInfo> detectedBeacons)
        {
            if (string.IsNullOrEmpty(name))
            {
                return await Task.FromResult(false);
            }

            var robot = _connectedRobots.Values.FirstOrDefault(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            if (robot == null)
            {
                return await Task.FromResult(false);
            }

            var currentTime = DateTime.UtcNow;
            var detectedMacAddresses = new HashSet<string>();

            // Process each detected beacon
            if (detectedBeacons != null)
            {
                foreach (var beaconInfo in detectedBeacons)
                {
                    if (string.IsNullOrWhiteSpace(beaconInfo.MacAddress))
                        continue;

                    var macAddress = beaconInfo.MacAddress.ToUpper();
                    detectedMacAddresses.Add(macAddress);

                    // Calculate distance (rough estimate based on RSSI)
                    var distanceMeters = Math.Pow(10, (-59.0 - beaconInfo.Rssi) / (10.0 * 2.0));

                    robot.DetectedBeacons.AddOrUpdate(macAddress,
                        // Add new detection
                        _ => new RobotProject.Shared.DTOs.RobotDetectedBeacon
                        {
                            MacAddress = macAddress,
                            BeaconName = beaconInfo.Name,
                            RoomName = beaconInfo.RoomName,
                            CurrentRssi = beaconInfo.Rssi,
                            DistanceMeters = distanceMeters,
                            IsInRange = beaconInfo.Rssi >= beaconInfo.RssiThreshold,
                            FirstDetected = currentTime,
                            LastDetected = currentTime,
                            DetectionCount = 1,
                            AverageRssi = beaconInfo.Rssi,
                            Status = RobotProject.Shared.DTOs.BeaconDetectionStatus.Active
                        },
                        // Update existing detection
                        (_, existing) =>
                        {
                            existing.BeaconName = beaconInfo.Name;
                            existing.RoomName = beaconInfo.RoomName;
                            existing.CurrentRssi = beaconInfo.Rssi;
                            existing.DistanceMeters = distanceMeters;
                            existing.IsInRange = beaconInfo.Rssi >= beaconInfo.RssiThreshold;
                            existing.LastDetected = currentTime;
                            existing.DetectionCount++;

                            // Update rolling average RSSI
                            existing.AverageRssi =
                                (existing.AverageRssi * (existing.DetectionCount - 1) + beaconInfo.Rssi) /
                                existing.DetectionCount;
                            existing.Status = RobotProject.Shared.DTOs.BeaconDetectionStatus.Active;

                            return existing;
                        });
                }
            }

            // Mark beacons as lost/timeout if they weren't detected in this cycle
            var lostThreshold = currentTime.AddSeconds(-10); // Lost after 10 seconds
            var timeoutThreshold = currentTime.AddSeconds(-30); // Timeout after 30 seconds

            foreach (var kvp in robot.DetectedBeacons.ToList())
            {
                if (!detectedMacAddresses.Contains(kvp.Key))
                {
                    var beacon = kvp.Value;

                    if (beacon.LastDetected < timeoutThreshold)
                    {
                        // Remove completely after timeout
                        robot.DetectedBeacons.TryRemove(kvp.Key, out _);
                    }
                    else if (beacon.LastDetected < lostThreshold)
                    {
                        beacon.Status = RobotProject.Shared.DTOs.BeaconDetectionStatus.Timeout;
                    }
                    else
                    {
                        beacon.Status = RobotProject.Shared.DTOs.BeaconDetectionStatus.Lost;
                    }
                }
            }

            // Update robot ping time as beacon updates count as activity
            robot.LastPing = currentTime;

            return await Task.FromResult(true); 
        }

        public void Dispose()
        {
            _offlineCheckTimer?.Dispose();
        }
    }
}