using System.Text;

namespace LineFollowerRobot.Services;

/// <summary>
/// Service that handles robot registration and periodic ping with the server
/// Ensures the robot is registered and maintains connection status
/// </summary>
public class RobotRegistrationService : BackgroundService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RobotRegistrationService> _logger;
    private readonly IConfiguration _configuration;
    
    private readonly string _robotName;
    private readonly string _serverBaseUrl;
    private readonly int _pingIntervalMs;
    private bool _isRegistered = false;

    public RobotRegistrationService(
        ILogger<RobotRegistrationService> logger,
        IConfiguration configuration)
    {
        // Initialize HttpClient with proper redirect handling
        var handler = new HttpClientHandler()
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        };
        _httpClient = new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(10);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "LineFollowerRobot/1.0");
        
        _logger = logger;
        _configuration = configuration;

        _robotName = _configuration["Robot:Name"] ?? Environment.MachineName;
        _serverBaseUrl = _configuration["Robot:ServerBaseUrl"] ?? "http://localhost:5000";
        _pingIntervalMs = _configuration.GetValue<int>("Robot:PingIntervalMs", 5000);

        _logger.LogInformation("Robot '{RobotName}' will register with server at '{ServerUrl}' and ping every {PingMs}ms",
            _robotName, _serverBaseUrl, _pingIntervalMs);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting robot registration service");

        // Wait a bit for other services to initialize
        await Task.Delay(1000, stoppingToken);

        // Initial registration attempt
        await RegisterRobot(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_isRegistered)
                {
                    await RegisterRobot(stoppingToken);
                }
                else
                {
                    await PingServer(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during robot registration/ping");
                _isRegistered = false; // Reset registration status on error
            }

            try
            {
                await Task.Delay(_pingIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Robot registration service stopped");
    }

    /// <summary>
    /// Register the robot with the server
    /// </summary>
    private async Task RegisterRobot(CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = $"{_serverBaseUrl}/api/Robot/{_robotName}/register";
            var response = await _httpClient.PostAsync(endpoint, null, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogInformation("Successfully registered robot '{RobotName}' with server", _robotName);
                _logger.LogDebug("Registration response: {Response}", responseJson);
                _isRegistered = true;
            }
            else
            {
                _logger.LogWarning("Failed to register robot '{RobotName}' with server. Status: {StatusCode}", 
                    _robotName, response.StatusCode);
                _isRegistered = false;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error during robot registration - server may be unreachable");
            _isRegistered = false;
        }
        catch (TaskCanceledException ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Robot registration timed out");
            }
            _isRegistered = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during robot registration");
            _isRegistered = false;
        }
    }

    /// <summary>
    /// Send periodic ping to server to maintain registration
    /// </summary>
    private async Task PingServer(CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = $"{_serverBaseUrl}/api/Robot/{_robotName}/ping";
            var response = await _httpClient.PostAsync(endpoint, null, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Successfully pinged server for robot '{RobotName}'", _robotName);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Robot '{RobotName}' not found on server - will re-register", _robotName);
                _isRegistered = false;
            }
            else
            {
                _logger.LogWarning("Failed to ping server for robot '{RobotName}'. Status: {StatusCode}", 
                    _robotName, response.StatusCode);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error during ping - server may be unreachable");
        }
        catch (TaskCanceledException ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Ping timed out");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during ping");
            _isRegistered = false; // Reset registration on error
        }
    }

    /// <summary>
    /// Check if robot is currently registered with the server
    /// </summary>
    public bool IsRegistered => _isRegistered;

    /// <summary>
    /// Get the robot name
    /// </summary>
    public string RobotName => _robotName;

    /// <summary>
    /// Dispose of HttpClient resources
    /// </summary>
    public override void Dispose()
    {
        _httpClient?.Dispose();
        base.Dispose();
    }
}