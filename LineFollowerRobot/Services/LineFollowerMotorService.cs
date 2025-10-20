using System.Device.Gpio;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LineFollowerRobot.Services;

public enum MotorState
{
    Stopped,
    Forward,
    Backward,
    TurnLeft,
    TurnRight,
    LeftForward,
    RightForward,
    SearchLeft,
    SearchRight
}

public class LineFollowerMotorService : IDisposable
{
    private readonly ILogger<LineFollowerMotorService> _logger;
    private readonly IConfiguration _config;
    private GpioController? _gpio;

    // GPIO Pin mappings - matching Python script exactly
    // Python comments show the exact pin assignments:
    private readonly int _frontLeftPin1 = 5; // FL_IN1 = 5 (Python)
    private readonly int _frontLeftPin2 = 6; // FL_IN2 = 6 (Python)
    private readonly int _frontRightPin1 = 19; // FR_IN1 = 19 (Python)
    private readonly int _frontRightPin2 = 26; // FR_IN2 = 26 (Python)  
    private readonly int _backLeftPin1 = 16; // BL_IN1 = 16 (Python)
    private readonly int _backLeftPin2 = 20; // BL_IN2 = 20 (Python)
    private readonly int _backRightPin1 = 13; // BR_IN1 = 13 (Python)
    private readonly int _backRightPin2 = 21; // BR_IN2 = 21 (Python)

    private bool _isInitialized = false;
    private MotorState _currentMotorState = MotorState.Stopped;
    private static readonly object _motorLock = new();

    // Line following state - controlled by server commands
    private volatile bool _isLineFollowingActive = false;
    public bool IsLineFollowingActive => _isLineFollowingActive;

    public MotorState CurrentState => _currentMotorState;

    public LineFollowerMotorService(ILogger<LineFollowerMotorService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;

        _logger.LogInformation("🔌 Line Follower Motor GPIO mapping (Python-matched):");
        _logger.LogInformation("   Front Left: {FL1},{FL2} (Python: FL_IN1=5, FL_IN2=6)", _frontLeftPin1,
            _frontLeftPin2);
        _logger.LogInformation("   Front Right: {FR1},{FR2} (Python: FR_IN1=19, FR_IN2=26)", _frontRightPin1,
            _frontRightPin2);
        _logger.LogInformation("   Back Left: {BL1},{BL2} (Python: BL_IN1=16, BL_IN2=20)", _backLeftPin1,
            _backLeftPin2);
        _logger.LogInformation("   Back Right: {BR1},{BR2} (Python: BR_IN1=13, BR_IN2=21)", _backRightPin1,
            _backRightPin2);
    }

    public async Task InitializeAsync()
    {
        try
        {
            _gpio = new GpioController();

            var pins = new[]
            {
                _frontLeftPin1, _frontLeftPin2,
                _frontRightPin1, _frontRightPin2,
                _backLeftPin1, _backLeftPin2,
                _backRightPin1, _backRightPin2
            };

            foreach (var pin in pins)
            {
                _gpio.OpenPin(pin, PinMode.Output);
                _gpio.Write(pin, PinValue.Low);
                _logger.LogDebug("🔌 GPIO pin {Pin} initialized as output", pin);
            }

            _isInitialized = true;
            _logger.LogInformation("✅ Line follower motor service initialized successfully (Python-matched pins)");

            // Start with motors stopped
            Stop();
            await Task.Delay(500); // Allow pins to settle
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to initialize line follower motor service");
            throw;
        }
    }

    private void SetPin(int pin, bool value)
    {
        try
        {
            if (_gpio == null || !_isInitialized) return;
            _gpio.Write(pin, value ? PinValue.High : PinValue.Low);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error setting pin {Pin} to {Value}", pin, value);
        }
    }

    public void MoveForward()
    {
        lock (_motorLock)
        {
            if (_currentMotorState == MotorState.Forward) return;

            _logger.LogDebug("🔄 Moving forward (Python algorithm)");

            // All wheels forward - matching Python exactly:
            // Python: set_pin(FL_IN1, 0), set_pin(FL_IN2, 1) = Front Left forward
            // Front Left forward
            SetPin(_frontLeftPin1, false); // FL_IN1 = 0
            SetPin(_frontLeftPin2, true); // FL_IN2 = 1

            // Python: set_pin(FR_IN1, 0), set_pin(FR_IN2, 1) = Front Right forward
            // Front Right forward  
            SetPin(_frontRightPin1, false); // FR_IN1 = 0
            SetPin(_frontRightPin2, true); // FR_IN2 = 1

            // Python: set_pin(BL_IN1, 0), set_pin(BL_IN2, 1) = Back Left forward
            // Back Left forward
            SetPin(_backLeftPin1, false); // BL_IN1 = 0
            SetPin(_backLeftPin2, true); // BL_IN2 = 1

            // Python: set_pin(BR_IN1, 0), set_pin(BR_IN2, 1) = Back Right forward
            // Back Right forward
            SetPin(_backRightPin1, false); // BR_IN1 = 0
            SetPin(_backRightPin2, true); // BR_IN2 = 1

            _currentMotorState = MotorState.Forward;
        }
    }

    public void MoveBackward()
    {
        lock (_motorLock)
        {
            if (_currentMotorState == MotorState.Backward) return;

            _logger.LogDebug("🔄 Moving backward (Python algorithm)");

            // All wheels backward - matching Python exactly:
            // Python: set_pin(FL_IN1, 1), set_pin(FL_IN2, 0) = Front Left backward
            // Front Left backward
            SetPin(_frontLeftPin1, true); // FL_IN1 = 1
            SetPin(_frontLeftPin2, false); // FL_IN2 = 0

            // Python: set_pin(FR_IN1, 1), set_pin(FR_IN2, 0) = Front Right backward
            // Front Right backward
            SetPin(_frontRightPin1, true); // FR_IN1 = 1
            SetPin(_frontRightPin2, false); // FR_IN2 = 0

            // Python: set_pin(BL_IN1, 1), set_pin(BL_IN2, 0) = Back Left backward
            // Back Left backward
            SetPin(_backLeftPin1, true); // BL_IN1 = 1
            SetPin(_backLeftPin2, false); // BL_IN2 = 0

            // Python: set_pin(BR_IN1, 1), set_pin(BR_IN2, 0) = Back Right backward
            // Back Right backward
            SetPin(_backRightPin1, true); // BR_IN1 = 1
            SetPin(_backRightPin2, false); // BR_IN2 = 0

            _currentMotorState = MotorState.Backward;
        }
    }

    public void TurnLeft()
    {
        lock (_motorLock)
        {
            if (_currentMotorState == MotorState.TurnLeft) return;

            _logger.LogDebug("🔄 Turning left (Python algorithm)");

            // Left wheels forward, Right wheels backward - matching Python exactly:
            // Python turn_left(): Left motors forward, Right motors backward

            // Left motors forward
            // Front Left forward
            SetPin(_frontLeftPin1, true); // FL_IN1 = 1
            SetPin(_frontLeftPin2, false); // FL_IN2 = 0

            // Back Left forward
            SetPin(_backLeftPin1, true); // BL_IN1 = 1
            SetPin(_backLeftPin2, false); // BL_IN2 = 0

            // Right motors backward
            // Front Right backward
            SetPin(_frontRightPin1, false); // FR_IN1 = 0
            SetPin(_frontRightPin2, true); // FR_IN2 = 1

            // Back Right backward
            SetPin(_backRightPin1, false); // BR_IN1 = 0
            SetPin(_backRightPin2, true); // BR_IN2 = 1

            _currentMotorState = MotorState.TurnLeft;
        }
    }

    public void TurnRight()
    {
        lock (_motorLock)
        {
            if (_currentMotorState == MotorState.TurnRight) return;

            _logger.LogDebug("🔄 Turning right (Python algorithm)");

            // Left wheels backward, Right wheels forward - matching Python exactly:
            // Python turn_right(): Left motors backward, Right motors forward

            // Left motors backward
            // Front Left backward
            SetPin(_frontLeftPin1, false); // FL_IN1 = 0
            SetPin(_frontLeftPin2, true); // FL_IN2 = 1

            // Back Left backward
            SetPin(_backLeftPin1, false); // BL_IN1 = 0
            SetPin(_backLeftPin2, true); // BL_IN2 = 1

            // Right motors forward
            // Front Right forward
            SetPin(_frontRightPin1, true); // FR_IN1 = 1
            SetPin(_frontRightPin2, false); // FR_IN2 = 0

            // Back Right forward
            SetPin(_backRightPin1, true); // BR_IN1 = 1
            SetPin(_backRightPin2, false); // BR_IN2 = 0

            _currentMotorState = MotorState.TurnRight;
        }
    }

    public void LeftForward()
    {
        lock (_motorLock)
        {
            if (_currentMotorState == MotorState.LeftForward) return;

            _logger.LogDebug("🔄 Left forward (veering left) - Python algorithm");

            // Python left_forward(): All left motors off, All right motors on
            // All left motors off
            SetPin(_frontLeftPin1, false); // FL_IN1 = 0
            SetPin(_frontLeftPin2, false); // FL_IN2 = 0
            SetPin(_backLeftPin1, false); // BL_IN1 = 0
            SetPin(_backLeftPin2, false); // BL_IN2 = 0

            // All right motors on (forward motion)
            SetPin(_frontRightPin1, false); // FR_IN1 = 0
            SetPin(_frontRightPin2, true); // FR_IN2 = 1
            SetPin(_backRightPin1, false); // BR_IN1 = 0
            SetPin(_backRightPin2, true); // BR_IN2 = 1

            _currentMotorState = MotorState.LeftForward;
        }
    }

    public void RightForward()
    {
        lock (_motorLock)
        {
            if (_currentMotorState == MotorState.RightForward) return;

            _logger.LogDebug("🔄 Right forward (veering right) - Python algorithm");

            // Python right_forward(): All right motors off, All left motors on
            // All right motors off
            SetPin(_frontRightPin1, false); // FR_IN1 = 0
            SetPin(_frontRightPin2, false); // FR_IN2 = 0
            SetPin(_backRightPin1, false); // BR_IN1 = 0
            SetPin(_backRightPin2, false); // BR_IN2 = 0

            // All left motors on (forward motion)
            SetPin(_frontLeftPin1, false); // FL_IN1 = 0
            SetPin(_frontLeftPin2, true); // FL_IN2 = 1
            SetPin(_backLeftPin1, false); // BL_IN1 = 0
            SetPin(_backLeftPin2, true); // BL_IN2 = 1

            _currentMotorState = MotorState.RightForward;
        }
    }

    public void SearchLeft()
    {
        lock (_motorLock)
        {
            if (_currentMotorState == MotorState.SearchLeft) return;

            _logger.LogDebug("🔍 Search left (slow left turn) - Python algorithm");

            // Python search pattern: Stop right motors, power left motors forward
            // This creates a slow left turn for searching

            // Stop right motors
            SetPin(_frontRightPin1, false); // FR_IN1 = 0
            SetPin(_frontRightPin2, false); // FR_IN2 = 0
            SetPin(_backRightPin1, false); // BR_IN1 = 0
            SetPin(_backRightPin2, false); // BR_IN2 = 0

            // Left motors forward (creates left turn)
            SetPin(_frontLeftPin1, false); // FL_IN1 = 0
            SetPin(_frontLeftPin2, true); // FL_IN2 = 1
            SetPin(_backLeftPin1, false); // BL_IN1 = 0
            SetPin(_backLeftPin2, true); // BL_IN2 = 1

            _currentMotorState = MotorState.SearchLeft;
        }
    }

    public void SearchRight()
    {
        lock (_motorLock)
        {
            if (_currentMotorState == MotorState.SearchRight) return;

            _logger.LogDebug("🔍 Search right (slow right turn) - Python algorithm");

            // Python search pattern: Stop left motors, power right motors forward
            // This creates a slow right turn for searching

            // Stop left motors  
            SetPin(_frontLeftPin1, false); // FL_IN1 = 0
            SetPin(_frontLeftPin2, false); // FL_IN2 = 0
            SetPin(_backLeftPin1, false); // BL_IN1 = 0
            SetPin(_backLeftPin2, false); // BL_IN2 = 0

            // Right motors forward (creates right turn)
            SetPin(_frontRightPin1, false); // FR_IN1 = 0
            SetPin(_frontRightPin2, true); // FR_IN2 = 1
            SetPin(_backRightPin1, false); // BR_IN1 = 0
            SetPin(_backRightPin2, true); // BR_IN2 = 1

            _currentMotorState = MotorState.SearchRight;
        }
    }

    public void Stop()
    {
        lock (_motorLock)
        {
            if (_currentMotorState == MotorState.Stopped) return;

            _logger.LogDebug("🛑 Stopping all motors (Python algorithm)");

            // Stop all motors - matching Python exactly
            SetPin(_frontLeftPin1, false); // FL_IN1 = 0
            SetPin(_frontLeftPin2, false); // FL_IN2 = 0
            SetPin(_frontRightPin1, false); // FR_IN1 = 0
            SetPin(_frontRightPin2, false); // FR_IN2 = 0
            SetPin(_backLeftPin1, false); // BL_IN1 = 0
            SetPin(_backLeftPin2, false); // BL_IN2 = 0
            SetPin(_backRightPin1, false); // BR_IN1 = 0
            SetPin(_backRightPin2, false); // BR_IN2 = 0

            _currentMotorState = MotorState.Stopped;

            // Verify stop by checking all pins are LOW after a brief delay
            Task.Delay(50).Wait(); // Allow GPIO to settle
            VerifyMotorsStopped();
        }
    }

    private void VerifyMotorsStopped()
    {
        try
        {
            if (_gpio == null || !_isInitialized) return;

            var allPins = new[]
            {
                _frontLeftPin1, _frontLeftPin2,
                _frontRightPin1, _frontRightPin2,
                _backLeftPin1, _backLeftPin2,
                _backRightPin1, _backRightPin2
            };

            bool allStopped = true;
            foreach (var pin in allPins)
            {
                var value = _gpio.Read(pin);
                if (value == PinValue.High)
                {
                    _logger.LogError("⚠️ Motor verification failed: Pin {Pin} is still HIGH after stop command", pin);
                    allStopped = false;
                }
            }

            if (allStopped)
            {
                _logger.LogInformation("✅ Motor stop verified: All pins are LOW");
            }
            else
            {
                _logger.LogCritical("❌ MOTOR STOP VERIFICATION FAILED - Some pins still active!");
                // Retry stop command
                Stop();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error verifying motor stop");
        }
    }

    // Navigation control methods for robot commands
    public async Task StartNavigationAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🚀 Starting navigation (line following mode)");

        // Start with forward movement and let line detection handle the rest
        MoveForward();

        await Task.CompletedTask;
    }

    public async Task StartLineFollowingAsync(CancellationToken cancellationToken = default)
    {
        // _logger.LogInformation("📍 Starting line following mode");

        _isLineFollowingActive = true;

        await Task.CompletedTask;
    }

    public async Task StopLineFollowingAsync()
    {
        // _logger.LogInformation("🛑 Stopping line following mode");

        _isLineFollowingActive = false;
        Stop();

        await Task.CompletedTask;
    }

    public async Task HoldPositionAsync()
    {
        //  _logger.LogInformation("🔒 Holding current position");
        Stop();
        await Task.CompletedTask;
    }

    public async Task EmergencyStopAsync()
    {
        _logger.LogCritical("🚨 EMERGENCY STOP - Immediate halt of all motors");

        lock (_motorLock)
        {
            // Immediately stop all motors without any delay
            SetPin(_frontLeftPin1, false);
            SetPin(_frontLeftPin2, false);
            SetPin(_frontRightPin1, false);
            SetPin(_frontRightPin2, false);
            SetPin(_backLeftPin1, false);
            SetPin(_backLeftPin2, false);
            SetPin(_backRightPin1, false);
            SetPin(_backRightPin2, false);

            _currentMotorState = MotorState.Stopped;
        }

        await Task.CompletedTask;
    }

    public async Task TurnAroundAsync()
    {
        _logger.LogInformation("🔄 Turning 180 degrees - Right wheels forward, Left wheels backward for 3 seconds");

        lock (_motorLock)
        {
            // Right wheels forward
            SetPin(_frontRightPin1, false); // FR_IN1 = 0
            SetPin(_frontRightPin2, true); // FR_IN2 = 1
            SetPin(_backRightPin1, false); // BR_IN1 = 0
            SetPin(_backRightPin2, true); // BR_IN2 = 1

            // Left wheels backward
            SetPin(_frontLeftPin1, true); // FL_IN1 = 1
            SetPin(_frontLeftPin2, false); // FL_IN2 = 0
            SetPin(_backLeftPin1, true); // BL_IN1 = 1
            SetPin(_backLeftPin2, false); // BL_IN2 = 0

            _currentMotorState = MotorState.TurnRight;

            Task.Delay(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult(); // 3 seconds
        }

        Stop();

        _logger.LogInformation("✅ 180-degree turn completed");
    }

    public void Dispose()
    {
        try
        {
            Stop();
            _gpio?.Dispose();
            _logger.LogInformation("🗑️ Line follower motor service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error disposing line follower motor service");
        }
    }
}