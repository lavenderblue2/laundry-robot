using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RobotProject.Shared.DTOs;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace LineFollowerRobot.Services;

/// <summary>
/// Service for two-way communication between robot and administrator server
/// Handles data exchange including beacon reporting and server configuration retrieval
/// </summary>
public class RobotServerCommunicationService : BackgroundService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RobotServerCommunicationService> _logger;
    private readonly BluetoothBeaconService _beaconService;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    private readonly string _robotName;
    private readonly string _serverBaseUrl;
    private readonly int _dataExchangeIntervalMs;

    // Public property to expose active beacons
    public List<BeaconConfigurationDto>? ActiveBeacons { get; private set; }

    // Weight limit settings from server
    private decimal? _maxWeightKg;
    private decimal? _minWeightKg;
    private bool _maxWeightExceeded = false;

    public RobotServerCommunicationService(
        ILogger<RobotServerCommunicationService> logger,
        BluetoothBeaconService beaconService,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        // Initialize HttpClient with proper redirect handling to avoid JSON parsing errors
        var handler = new HttpClientHandler()
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        };
        _httpClient = new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "LineFollowerRobot/1.0");

        _logger = logger;
        _beaconService = beaconService;
        _configuration = configuration;
        _serviceProvider = serviceProvider;

        _robotName = _configuration["Robot:Name"] ?? Environment.MachineName;
        _serverBaseUrl = _configuration["Robot:ServerBaseUrl"] ?? "http://localhost:5000";
        _dataExchangeIntervalMs = _configuration.GetValue<int>("Robot:DataExchangeIntervalMs", 1000);

        _logger.LogInformation("Robot '{RobotName}' will communicate with server at '{ServerUrl}' every {IntervalMs}ms",
            _robotName, _serverBaseUrl, _dataExchangeIntervalMs);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting robot-server communication service");

        // Wait a bit for other services to initialize
        await Task.Delay(2000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformDataExchange(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data exchange with server");
            }

            try
            {
                await Task.Delay(_dataExchangeIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Robot-server communication service stopped");
    }

    /// <summary>
    /// Get current weight reading from HX711 service
    /// </summary>
    private double GetCurrentWeight()
    {
        try
        {
            var hx711Service = _serviceProvider.GetService<Hx711Service>();
            return hx711Service?.LastWeightReadInKg ?? 0.0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting weight reading from HX711 service");
            return 0.0;
        }
    }

    /// <summary>
    /// Get current ultrasonic distance from UltrasonicSensorService
    /// </summary>
    private double GetUltrasonicDistance()
    {
        try
        {
            var ultrasonicService = _serviceProvider.GetService<UltrasonicSensorService>();
            return ultrasonicService?.GetDistance() ?? 0.0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting ultrasonic distance from UltrasonicSensorService");
            return 0.0;
        }
    }

    /// <summary>
    /// Perform bidirectional data exchange with the server
    /// </summary>
    private async Task PerformDataExchange(CancellationToken cancellationToken)
    {
        try
        {
            // Get detected beacons from beacon service
            var detectedBeacons = GetDetectedBeacons();

            // Server-side arrival detection DISABLED - robot-side handles all beacon detection
            bool isInTarget = false;

            // Get current weight reading
            double currentWeightKg = GetCurrentWeight();

            // Check if weight exceeds maximum
            CheckWeightLimits(currentWeightKg);

            // Get current ultrasonic distance
            double ultrasonicDistance = GetUltrasonicDistance();

            // Create request payload with error messages if applicable
            var errors = new List<string>();
            if (_maxWeightExceeded && _maxWeightKg.HasValue)
            {
                errors.Add($"MAX WEIGHT EXCEEDED: {currentWeightKg:F2}kg > {_maxWeightKg:F2}kg - Please remove some weight");
            }

            var request = new RobotDataExchangeRequest
            {
                RobotName = _robotName,
                Timestamp = DateTime.UtcNow,
                DetectedBeacons = detectedBeacons,
                IsInTarget = isInTarget,
                WeightKg = currentWeightKg,
                USSensor1ObstacleDistance = ultrasonicDistance,
                Errors = errors
            };

            // Serialize request to JSON
            var jsonContent = JsonSerializer.Serialize(request, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Send POST request to data exchange endpoint
            var endpoint = $"{_serverBaseUrl}/api/Robot/{_robotName}/data-exchange";
            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var serverResponse = JsonSerializer.Deserialize<RobotDataExchangeResponse>(responseJson,
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                if (serverResponse?.Success == true)
                {
                    await ProcessServerResponse(serverResponse);
                    _logger.LogDebug(
                        "Successfully exchanged data with server - received {BeaconCount} beacons, IsLineFollowing: {IsLineFollowing}",
                        serverResponse.ActiveBeacons?.Count ?? 0, serverResponse.IsLineFollowing);
                }
                else
                {
                    _logger.LogWarning("Server returned unsuccessful response: {Messages}",
                        string.Join(", ", serverResponse?.Messages ?? new List<string>()));
                }
            }
            else
            {
                _logger.LogWarning("Data exchange failed with status {StatusCode}: {ReasonPhrase}",
                    response.StatusCode, response.ReasonPhrase);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error during data exchange - server may be unreachable");
        }
        catch (TaskCanceledException ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Data exchange timed out");
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to serialize/deserialize data exchange payload");
        }
    }

    // Store detection mode from server
    private string _detectionMode = "beacon"; // default to beacon mode

    // Arrival confirmation system - requires 3 consecutive positive detections
    private int _arrivalConfirmationCount = 0;
    private const int REQUIRED_CONFIRMATIONS = 3; // Must detect target 3 times in a row
    private const int CONFIRMATION_DELAY_MS = 1000; // Minimum 1 second between confirmations
    private DateTime? _firstDetectionTime = null;
    private DateTime? _lastConfirmationTime = null; // Track last confirmation time
    private string? _currentTargetRoom = null; // Track which room we're confirming arrival at

    /// <summary>
    /// Check if robot is near BASE beacon (for return trips)
    /// Uses 3-confirmation system @ 1000ms intervals to prevent false positives
    /// Server handles base beacon verification because robot doesn't reliably know which one is base
    /// </summary>
    private bool CheckIfAtBaseBeacon(List<BeaconInfo> detectedBeacons)
    {
        bool currentlyDetected = false;
        string? targetRoomName = null;

        // Beacon detection mode - ONLY check BASE beacons (not navigation targets)
        if (ActiveBeacons != null && ActiveBeacons.Any())
        {
            // Get ONLY the base beacons (IsBase=true, NOT IsNavigationTarget)
            var baseBeacons = ActiveBeacons.Where(b => b.IsBase).ToList();

            _logger.LogDebug("Checking base beacons: {Count} base beacon(s) configured", baseBeacons.Count);

            foreach (var detectedBeacon in detectedBeacons)
            {
                var baseBeacon = baseBeacons.FirstOrDefault(b =>
                    string.Equals(b.MacAddress, detectedBeacon.MacAddress, StringComparison.OrdinalIgnoreCase));

                if (baseBeacon != null)
                {
                    _logger.LogDebug("Base beacon {Mac} ({Room}): RSSI={Rssi}, Threshold={Threshold}, InRange={InRange}",
                        detectedBeacon.MacAddress, baseBeacon.RoomName, detectedBeacon.Rssi,
                        baseBeacon.RssiThreshold, detectedBeacon.Rssi >= baseBeacon.RssiThreshold);

                    if (detectedBeacon.Rssi >= baseBeacon.RssiThreshold)
                    {
                        currentlyDetected = true;
                        targetRoomName = baseBeacon.RoomName;
                        break;
                    }
                }
            }
        }

        // Check if target room has changed - if so, reset confirmation counter
        if (targetRoomName != _currentTargetRoom)
        {
            if (_arrivalConfirmationCount > 0)
            {
                _logger.LogWarning(
                    "Base beacon changed from '{OldTarget}' to '{NewTarget}' - resetting confirmation counter (was at {Count}/{Required})",
                    _currentTargetRoom ?? "None", targetRoomName ?? "None", _arrivalConfirmationCount, REQUIRED_CONFIRMATIONS);
            }
            _arrivalConfirmationCount = 0;
            _firstDetectionTime = null;
            _currentTargetRoom = targetRoomName;
        }

        // Confirmation logic - ONLY stop if we have a base beacon AND it's within threshold
        bool hasBaseBeacon = ActiveBeacons != null && ActiveBeacons.Any(b => b.IsBase);

        if (currentlyDetected && hasBaseBeacon)
        {
            // Base beacon is currently detected
            if (_arrivalConfirmationCount == 0)
            {
                // FIRST DETECTION - STOP IMMEDIATELY to avoid overshooting
                _logger.LogInformation("BASE beacon '{TargetRoom}' detected within threshold - STOPPING to verify (1/{RequiredConfirmations})",
                    targetRoomName, REQUIRED_CONFIRMATIONS);

                try
                {
                    var motorService = _serviceProvider.GetService<LineFollowerMotorService>();
                    motorService?.StopLineFollowingAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping robot for base beacon arrival verification");
                }

                _firstDetectionTime = DateTime.UtcNow;
                _lastConfirmationTime = DateTime.UtcNow;
                _arrivalConfirmationCount = 1;
            }
            else
            {
                // Check if enough time has passed since last confirmation (enforce delay)
                var timeSinceLastConfirmation = DateTime.UtcNow - (_lastConfirmationTime ?? DateTime.UtcNow);
                if (timeSinceLastConfirmation.TotalMilliseconds < CONFIRMATION_DELAY_MS)
                {
                    // Not enough time has passed, skip this check
                    return false;
                }

                // Increment confirmation count (robot is now stopped and verifying)
                _arrivalConfirmationCount++;
                _lastConfirmationTime = DateTime.UtcNow;
                _logger.LogInformation("Verifying BASE beacon arrival at '{TargetRoom}' ({CurrentCount}/{RequiredConfirmations})...",
                    targetRoomName, _arrivalConfirmationCount, REQUIRED_CONFIRMATIONS);

                // Check if we've reached required confirmations
                if (_arrivalConfirmationCount >= REQUIRED_CONFIRMATIONS)
                {
                    var dwellTime = DateTime.UtcNow - _firstDetectionTime.Value;
                    _logger.LogInformation("✓ BASE BEACON ARRIVAL CONFIRMED at '{TargetRoom}' after {Confirmations} checks ({DwellSeconds:F1}s)",
                        targetRoomName, REQUIRED_CONFIRMATIONS, dwellTime.TotalSeconds);

                    // Reset counters AFTER confirming arrival
                    _arrivalConfirmationCount = 0;
                    _firstDetectionTime = null;
                    _lastConfirmationTime = null;
                    _currentTargetRoom = null; // Clear target so next target starts fresh
                    return true;
                }
            }
        }
        else
        {
            // Base beacon NOT detected - reset confirmation counter and resume if needed
            if (_arrivalConfirmationCount > 0)
            {
                var reason = !hasBaseBeacon ? "base beacon cleared" : "beacon signal lost";
                _logger.LogWarning("BASE beacon '{TargetRoom}' lost after {Count}/{RequiredConfirmations} checks ({Reason}) - FALSE ALARM! Resuming...",
                    _currentTargetRoom, _arrivalConfirmationCount, REQUIRED_CONFIRMATIONS, reason);
                _arrivalConfirmationCount = 0;
                _firstDetectionTime = null;
                _lastConfirmationTime = null;

                // Robot stopped for verification but lost signal - explicitly resume line following
                try
                {
                    var motorService = _serviceProvider.GetService<LineFollowerMotorService>();
                    if (motorService != null)
                    {
                        motorService.StartLineFollowingAsync().GetAwaiter().GetResult();
                        _logger.LogInformation("Line following resumed after base beacon false alarm");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resuming line following after base beacon false alarm");
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Check if robot is near any navigation target beacon OR if floor color was detected
    /// Uses 3-confirmation system to prevent false positives from signal fluctuations
    /// Detection method depends on server configuration (beacon or color mode)
    /// </summary>
    private bool CheckIfAtNavigationTarget(List<BeaconInfo> detectedBeacons)
    {
        bool currentlyDetected = false;
        string? targetRoomName = null;

        if (_detectionMode == "color")
        {
            // Color detection mode - ONLY check floor color
            var lineFollowerService = _serviceProvider.GetService<LineFollowerService>();
            currentlyDetected = lineFollowerService != null && lineFollowerService.FloorColorDetected;

            // Get target room name from active beacons
            if (currentlyDetected && ActiveBeacons != null)
            {
                targetRoomName = ActiveBeacons.FirstOrDefault(b => b.IsNavigationTarget)?.RoomName;
            }
        }
        else
        {
            // Beacon detection mode - ONLY check beacon proximity
            if (ActiveBeacons != null && ActiveBeacons.Any())
            {
                // Get ONLY the navigation target beacons (should be exactly 1)
                var navigationTargets = ActiveBeacons.Where(b => b.IsNavigationTarget).ToList();

                _logger.LogDebug("Checking navigation targets: {Count} target(s) configured", navigationTargets.Count);

                foreach (var detectedBeacon in detectedBeacons)
                {
                    var targetBeacon = navigationTargets.FirstOrDefault(b =>
                        string.Equals(b.MacAddress, detectedBeacon.MacAddress, StringComparison.OrdinalIgnoreCase));

                    if (targetBeacon != null)
                    {
                        _logger.LogDebug("Navigation target beacon {Mac} ({Room}): RSSI={Rssi}, Threshold={Threshold}, InRange={InRange}",
                            detectedBeacon.MacAddress, targetBeacon.RoomName, detectedBeacon.Rssi,
                            targetBeacon.RssiThreshold, detectedBeacon.Rssi >= targetBeacon.RssiThreshold);

                        if (detectedBeacon.Rssi >= targetBeacon.RssiThreshold)
                        {
                            currentlyDetected = true;
                            targetRoomName = targetBeacon.RoomName;
                            break;
                        }
                    }
                }
            }
        }

        // Check if target room has changed - if so, reset confirmation counter
        if (targetRoomName != _currentTargetRoom)
        {
            if (_arrivalConfirmationCount > 0)
            {
                _logger.LogWarning(
                    "Target changed from '{OldTarget}' to '{NewTarget}' - resetting confirmation counter (was at {Count}/{Required})",
                    _currentTargetRoom ?? "None", targetRoomName ?? "None", _arrivalConfirmationCount, REQUIRED_CONFIRMATIONS);
            }
            _arrivalConfirmationCount = 0;
            _firstDetectionTime = null;
            _currentTargetRoom = targetRoomName;
        }

        // Confirmation logic - ONLY stop if we have a navigation target AND it's within threshold
        // Check if we actually have any navigation targets set
        bool hasNavigationTarget = ActiveBeacons != null && ActiveBeacons.Any(b => b.IsNavigationTarget);

        if (currentlyDetected && hasNavigationTarget)
        {
            // Target is currently detected AND we have an active navigation target
            if (_arrivalConfirmationCount == 0)
            {
                // FIRST DETECTION - STOP IMMEDIATELY to avoid overshooting
                _logger.LogInformation("Target '{TargetRoom}' detected within threshold - STOPPING to verify (1/{RequiredConfirmations})",
                    targetRoomName, REQUIRED_CONFIRMATIONS);

                try
                {
                    var motorService = _serviceProvider.GetService<LineFollowerMotorService>();
                    motorService?.StopLineFollowingAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error stopping robot for arrival verification");
                }

                _firstDetectionTime = DateTime.UtcNow;
                _lastConfirmationTime = DateTime.UtcNow;
                _arrivalConfirmationCount = 1;
            }
            else
            {
                // Check if enough time has passed since last confirmation (enforce delay)
                var timeSinceLastConfirmation = DateTime.UtcNow - (_lastConfirmationTime ?? DateTime.UtcNow);
                if (timeSinceLastConfirmation.TotalMilliseconds < CONFIRMATION_DELAY_MS)
                {
                    // Not enough time has passed, skip this check
                    return false;
                }

                // Increment confirmation count (robot is now stopped and verifying)
                _arrivalConfirmationCount++;
                _lastConfirmationTime = DateTime.UtcNow;
                _logger.LogInformation("Verifying arrival at '{TargetRoom}' ({CurrentCount}/{RequiredConfirmations})...",
                    targetRoomName, _arrivalConfirmationCount, REQUIRED_CONFIRMATIONS);

                // Check if we've reached required confirmations
                if (_arrivalConfirmationCount >= REQUIRED_CONFIRMATIONS)
                {
                    var mode = _detectionMode == "color" ? "FLOOR COLOR" : "BEACON";
                    var dwellTime = DateTime.UtcNow - _firstDetectionTime.Value;
                    _logger.LogInformation("✓ ARRIVAL CONFIRMED at '{TargetRoom}' via {Mode} after {Confirmations} checks ({DwellSeconds:F1}s)",
                        targetRoomName, mode, REQUIRED_CONFIRMATIONS, dwellTime.TotalSeconds);

                    // Reset counters AFTER confirming arrival
                    _arrivalConfirmationCount = 0;
                    _firstDetectionTime = null;
                    _lastConfirmationTime = null;
                    _currentTargetRoom = null; // Clear target so next target starts fresh
                    return true;
                }
            }
        }
        else
        {
            // Target NOT detected OR no navigation target set - reset confirmation counter and resume if needed
            if (_arrivalConfirmationCount > 0)
            {
                var reason = !hasNavigationTarget ? "navigation target cleared" : "beacon signal lost";
                _logger.LogWarning("Target '{TargetRoom}' lost after {Count}/{RequiredConfirmations} checks ({Reason}) - FALSE ALARM! Resuming...",
                    _currentTargetRoom, _arrivalConfirmationCount, REQUIRED_CONFIRMATIONS, reason);
                _arrivalConfirmationCount = 0;
                _firstDetectionTime = null;
                _lastConfirmationTime = null;

                // Robot stopped for verification but lost signal - explicitly resume line following
                try
                {
                    var motorService = _serviceProvider.GetService<LineFollowerMotorService>();
                    if (motorService != null)
                    {
                        motorService.StartLineFollowingAsync().GetAwaiter().GetResult();
                        _logger.LogInformation("Line following resumed after false alarm");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resuming line following after false alarm");
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Convert detected beacons from BeaconService to DTOs for transmission
    /// </summary>
    private List<BeaconInfo> GetDetectedBeacons()
    {
        var detectedBeacons = new List<BeaconInfo>();

        var trackedBeacons = _beaconService.GetTrackedBeacons();
        foreach (var kvp in trackedBeacons)
        {
            var beaconData = kvp.Value;
            detectedBeacons.Add(new BeaconInfo
            {
                MacAddress = kvp.Key,
                Name = beaconData.Name ?? kvp.Key,
                Rssi = beaconData.Rssi,
                Distance = beaconData.Distance,
                LastSeen = beaconData.LastSeen,
                RoomName = beaconData.RoomName ?? "Unknown",
                IsActive = true,
                RssiThreshold = beaconData.RssiThreshold
            });
        }

        return detectedBeacons;
    }

    /// <summary>
    /// Process server response and update local configuration
    /// </summary>
    private async Task ProcessServerResponse(RobotDataExchangeResponse serverResponse)
    {
        try
        {
            var motorService = _serviceProvider.GetService<LineFollowerMotorService>();
            // Update beacon tracking list if server sent new beacon configurations
            if (serverResponse.ActiveBeacons?.Any() == true)
            {
                // Store active beacons
                ActiveBeacons = serverResponse.ActiveBeacons;

                var beaconConfigs =
                    new Dictionary<string, (string Name, string RoomName, int RssiThreshold, bool IsNavigationTarget,
                        int Priority)>();

                foreach (var beacon in serverResponse.ActiveBeacons)
                {
                    beaconConfigs[beacon.MacAddress] = (
                        beacon.Name,
                        beacon.RoomName,
                        beacon.RssiThreshold,
                        beacon.IsNavigationTarget,
                        beacon.Priority
                    );
                }

                await _beaconService.UpdateBeaconConfigurations(beaconConfigs);
                _logger.LogDebug("Updated beacon configurations from server: {BeaconCount} beacons",
                    beaconConfigs.Count);
            }
            else
            {
                ActiveBeacons = null;
            }

            // Process any server messages
            if (serverResponse.Messages?.Any() == true)
            {
                foreach (var message in serverResponse.Messages)
                {
                    _logger.LogDebug("Server message: {Message}", message);
                }
            }

            // Handle emergency stop
            if (serverResponse.EmergencyStop)
            {
                _logger.LogWarning("EMERGENCY STOP received from server!");
                if (motorService != null)
                {
                    await motorService.EmergencyStopAsync();
                }
            }

            // Handle maintenance mode
            if (serverResponse.MaintenanceMode)
            {
                _logger.LogInformation("Maintenance mode activated by server");
                if (motorService != null)
                {
                    await motorService.StopLineFollowingAsync();
                }
            }

            // Handle line following command from server
            if (motorService != null)
            {
                bool currentlyFollowing = motorService.IsLineFollowingActive;

                if (serverResponse.IsLineFollowing && !currentlyFollowing)
                {
                    _logger.LogInformation("Server instructed robot to start line following");
                    await motorService.StartLineFollowingAsync();
                }
                else if (!serverResponse.IsLineFollowing && currentlyFollowing)
                {
                    _logger.LogInformation("Server instructed robot to stop line following");
                    await motorService.StopLineFollowingAsync();
                }
                // Else: No state change, don't spam logs or re-call methods
            }

            // Update detection mode from server configuration
            if (serverResponse.Configuration != null && serverResponse.Configuration.ContainsKey("room_detection_mode"))
            {
                var detectionMode = serverResponse.Configuration["room_detection_mode"]?.ToString()?.ToLower() ?? "beacon";
                if (_detectionMode != detectionMode)
                {
                    _detectionMode = detectionMode;
                    _logger.LogWarning("Room detection mode changed to: {DetectionMode}", _detectionMode.ToUpper());
                }
            }

            // Update line color from server
            var lineFollowerService = _serviceProvider.GetService<LineFollowerService>();
            if (lineFollowerService != null)
            {
                lineFollowerService.LineColor = serverResponse.FollowColor;
                if (serverResponse.FollowColor != null && serverResponse.FollowColor.Length >= 3)
                {
                    _logger.LogInformation("Updated line color to RGB({R},{G},{B})",
                        serverResponse.FollowColor[0], serverResponse.FollowColor[1], serverResponse.FollowColor[2]);
                }
                else
                {
                    _logger.LogInformation("Reset line color to default (black)");
                }

                // Update stop-at floor color from server
                lineFollowerService.StopAtColor = serverResponse.StopAtColor;
                if (serverResponse.StopAtColor != null && serverResponse.StopAtColor.Length >= 3)
                {
                    _logger.LogInformation("Updated stop-at floor color to RGB({R},{G},{B})",
                        serverResponse.StopAtColor[0], serverResponse.StopAtColor[1], serverResponse.StopAtColor[2]);
                }
                else
                {
                    _logger.LogInformation("No floor color set (beacon-only navigation)");
                }
            }

            // Update data exchange interval if changed
            if (serverResponse.DataExchangeIntervalSeconds > 0 &&
                serverResponse.DataExchangeIntervalSeconds * 1000 != _dataExchangeIntervalMs)
            {
                _logger.LogInformation("Server requested data exchange interval change to {IntervalSeconds}s",
                    serverResponse.DataExchangeIntervalSeconds);
                // TODO: Implement dynamic interval adjustment
            }

            // Update weight limits from server
            if (serverResponse.MaxWeightKg.HasValue)
            {
                if (_maxWeightKg != serverResponse.MaxWeightKg)
                {
                    _maxWeightKg = serverResponse.MaxWeightKg;
                    _logger.LogInformation("Maximum weight limit updated: {MaxWeightKg}kg", _maxWeightKg);
                }
            }

            if (serverResponse.MinWeightKg.HasValue)
            {
                if (_minWeightKg != serverResponse.MinWeightKg)
                {
                    _minWeightKg = serverResponse.MinWeightKg;
                    _logger.LogInformation("Minimum weight limit updated: {MinWeightKg}kg (billing only)", _minWeightKg);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing server response");
        }
    }

    /// <summary>
    /// Check if current weight exceeds maximum limit and take action
    /// </summary>
    private void CheckWeightLimits(double currentWeightKg)
    {
        if (!_maxWeightKg.HasValue || currentWeightKg <= 0)
            return;

        bool wasExceeded = _maxWeightExceeded;
        _maxWeightExceeded = currentWeightKg > (double)_maxWeightKg.Value;

        // Only log when state changes (entering or leaving exceeded state)
        if (_maxWeightExceeded && !wasExceeded)
        {
            _logger.LogWarning(
                "⚠️  MAX WEIGHT EXCEEDED! Current: {CurrentWeight:F2}kg, Max: {MaxWeight:F2}kg - PLEASE REMOVE SOME WEIGHT",
                currentWeightKg, _maxWeightKg);

            // Try to stop line following
            try
            {
                var motorService = _serviceProvider.GetService<LineFollowerMotorService>();
                if (motorService != null)
                {
                    Task.Run(async () => await motorService.StopLineFollowingAsync());
                    _logger.LogWarning("Line following stopped due to max weight exceeded");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping line following due to max weight");
            }
        }
        else if (!_maxWeightExceeded && wasExceeded)
        {
            _logger.LogInformation(
                "✓ Weight is now within limits: {CurrentWeight:F2}kg <= {MaxWeight:F2}kg",
                currentWeightKg, _maxWeightKg);
        }
    }

    /// <summary>
    /// Dispose of HttpClient resources
    /// </summary>
    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}