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

            // Check if robot is near any navigation target
            bool isInTarget = CheckIfAtNavigationTarget(detectedBeacons);

            // Get current weight reading
            double currentWeightKg = GetCurrentWeight();

            // Get current ultrasonic distance
            double ultrasonicDistance = GetUltrasonicDistance();

            // Create request payload
            var request = new RobotDataExchangeRequest
            {
                RobotName = _robotName,
                Timestamp = DateTime.UtcNow,
                DetectedBeacons = detectedBeacons,
                IsInTarget = isInTarget,
                WeightKg = currentWeightKg,
                USSensor1ObstacleDistance = ultrasonicDistance
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

    /// <summary>
    /// Check if robot is near any navigation target beacon
    /// </summary>
    private bool CheckIfAtNavigationTarget(List<BeaconInfo> detectedBeacons)
    {
        // Check beacon proximity
        if (ActiveBeacons == null || !ActiveBeacons.Any())
            return false;

        foreach (var detectedBeacon in detectedBeacons)
        {
            var targetBeacon = ActiveBeacons.FirstOrDefault(b =>
                b.IsNavigationTarget &&
                string.Equals(b.MacAddress, detectedBeacon.MacAddress, StringComparison.OrdinalIgnoreCase));

            if (targetBeacon != null && detectedBeacon.Rssi >= targetBeacon.RssiThreshold)
            {
                _logger.LogInformation("Robot is at navigation target: {BeaconName} (RSSI: {Rssi} >= {Threshold})",
                    targetBeacon.Name, detectedBeacon.Rssi, targetBeacon.RssiThreshold);
                return true;
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
                if (serverResponse.IsLineFollowing)
                {
                    _logger.LogInformation("Server instructed robot to start line following");
                    await motorService.StartLineFollowingAsync();
                }
                else
                {
                    _logger.LogInformation("Server instructed robot to stop line following");
                    await motorService.StopLineFollowingAsync();
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
            }

            // Update data exchange interval if changed
            if (serverResponse.DataExchangeIntervalSeconds > 0 &&
                serverResponse.DataExchangeIntervalSeconds * 1000 != _dataExchangeIntervalMs)
            {
                _logger.LogInformation("Server requested data exchange interval change to {IntervalSeconds}s",
                    serverResponse.DataExchangeIntervalSeconds);
                // TODO: Implement dynamic interval adjustment
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing server response");
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