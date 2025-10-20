using System.Device.Gpio;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LineFollowerRobot.Services;

/// <summary>
/// Service to control LED array headlights on GPIO 14 based on line following status
/// Uses periodic timer to monitor IsLineFollowingActive and control GPIO pin accordingly
/// </summary>
public class HeadlightService : BackgroundService, IDisposable
{
    private readonly ILogger<HeadlightService> _logger;
    private readonly IConfiguration _config;
    private readonly LineFollowerMotorService _motorService;
    private GpioController? _gpio;
    private PeriodicTimer? _periodicTimer;

    // GPIO Pin for LED array headlights
    private readonly int _headlightPin = 14;

    private bool _isInitialized = false;
    private bool _headlightsOn = false; 
    private bool _lastLineFollowingState = false;
    private readonly object _headlightLock = new();

    public HeadlightService(
        ILogger<HeadlightService> logger, 
        IConfiguration config,
        LineFollowerMotorService motorService)
    {
        _logger = logger;
        _config = config;
        _motorService = motorService;

        // Check if running on actual hardware (not in development/simulation)
        var isSimulation = _config.GetValue<bool>("Robot:IsSimulation", false);
        
        if (!isSimulation)
        {
            try
            {
                InitializeGpio();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize GPIO for headlight control. Running without headlight control.");
            }
        }
        else
        {
            _logger.LogInformation("HeadlightService running in simulation mode - GPIO control disabled");
        }
    }

    private void InitializeGpio()
    {
        try
        {
            _gpio = new GpioController();
            _gpio.OpenPin(_headlightPin, PinMode.Output);
            _gpio.Write(_headlightPin, PinValue.Low); // Start with headlights off
            
            _isInitialized = true;
            _logger.LogInformation("HeadlightService initialized - GPIO {Pin} ready for LED array control", _headlightPin);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize GPIO controller for headlights");
            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("HeadlightService started - monitoring line following status with 200ms periodic timer");

        // Wait a bit for other services to initialize
        await Task.Delay(3000, stoppingToken);

        // Create periodic timer for 200ms intervals
        _periodicTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));

        try
        {
            while (await _periodicTimer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await MonitorLineFollowingStatus();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in headlight monitoring loop");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }

        // Turn off headlights when service stops
        await TurnOffHeadlightsAsync();
        _logger.LogInformation("HeadlightService stopped");
    }

    /// <summary>
    /// Monitor the line following status and control headlights accordingly
    /// </summary>
    private async Task MonitorLineFollowingStatus()
    {
        try
        {
            bool currentLineFollowingState = _motorService.IsLineFollowingActive;

            // Only change headlight state if line following status changed
            if (currentLineFollowingState != _lastLineFollowingState)
            {
                if (currentLineFollowingState)
                {
                    await TurnOnHeadlightsAsync();
                }
                else
                {
                    await TurnOffHeadlightsAsync();
                }

                _lastLineFollowingState = currentLineFollowingState;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error monitoring line following status for headlight control");
        }
    }

    /// <summary>
    /// Turn on the LED array headlights
    /// </summary>
    private async Task TurnOnHeadlightsAsync()
    {
        await Task.Run(() =>
        {
            lock (_headlightLock)
            {
                if (!_headlightsOn && _isInitialized && _gpio != null)
                {
                    try
                    {
                        _gpio.Write(_headlightPin, PinValue.High);
                        _headlightsOn = true;
                        _logger.LogInformation("🔦 Headlights turned ON - Robot is navigating/following line");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to turn on headlights");
                    }
                }
                else if (!_isInitialized)
                {
                    _logger.LogDebug("Headlights would be turned ON (simulation mode)");
                }
            }
        });
    }

    /// <summary>
    /// Turn off the LED array headlights
    /// </summary>
    private async Task TurnOffHeadlightsAsync()
    {
        await Task.Run(() =>
        {
            lock (_headlightLock)
            {
                if (_headlightsOn && _isInitialized && _gpio != null)
                {
                    try
                    {
                        _gpio.Write(_headlightPin, PinValue.Low);
                        _headlightsOn = false;
                        _logger.LogInformation("🔦 Headlights turned OFF - Robot stopped navigating");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to turn off headlights");
                    }
                }
                else if (!_isInitialized)
                {
                    _logger.LogDebug("Headlights would be turned OFF (simulation mode)");
                }
            }
        });
    }

    /// <summary>
    /// Get the current headlight status
    /// </summary>
    public bool AreHeadlightsOn => _headlightsOn;

    /// <summary>
    /// Manual control method for testing - turn on headlights
    /// </summary>
    public async Task ManualTurnOnAsync()
    {
        _logger.LogInformation("Manual headlight control: Turning ON");
        await TurnOnHeadlightsAsync();
    }

    /// <summary>
    /// Manual control method for testing - turn off headlights
    /// </summary>
    public async Task ManualTurnOffAsync()
    {
        _logger.LogInformation("Manual headlight control: Turning OFF");
        await TurnOffHeadlightsAsync();
    }

    /// <summary>
    /// Dispose of GPIO and timer resources
    /// </summary>
    public override void Dispose()
    {
        try
        {
            // Make sure headlights are off before disposal
            if (_headlightsOn && _gpio != null)
            {
                _gpio.Write(_headlightPin, PinValue.Low);
                _logger.LogInformation("Headlights turned off during disposal");
            }

            _periodicTimer?.Dispose();
            _gpio?.Dispose();
            _logger.LogInformation("HeadlightService resources disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing HeadlightService resources");
        }

        base.Dispose();
    }
}