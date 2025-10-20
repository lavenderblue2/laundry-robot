using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RobotProject.Shared.DTOs;

namespace LineFollowerRobot.Services;

public class LineFollowerService : BackgroundService
{
    private readonly ILogger<LineFollowerService> _logger;
    private readonly IConfiguration _config;
    private readonly LineDetectionCameraService _cameraService;
    private readonly LineFollowerMotorService _motorService;
    private readonly BluetoothBeaconService _beaconService;
    private readonly UltrasonicSensorService _ultrasonicService;
    private readonly IServiceProvider _serviceProvider;
    private bool _obstacleDetected = false;

    // PID control variables - matching Python exactly
    private double _previousError = 0;
    private double _integral = 0;
    private readonly double _kp = 0.2; // Exact match to Python
    private readonly double _ki = 0.0; // Exact match to Python  
    private readonly double _kd = 0.05; // Exact match to Python

    // Movement thresholds - matching Python exactly
    private readonly int _smallErrorThreshold = 30; // Python: < 30 = go straight
    private readonly int _mediumErrorThreshold = 80; // Python: < 80 = gentle correction
    private readonly int _extremeErrorThreshold = 150; // Python: > 150 = full turn

    private readonly int _frameDelayMs = 1000 / 30;

    // Line tracking state - matching Python exactly
    private bool _lineDetected = false;
    private int _lineLostCounter = 0;
    private readonly int _maxLineLostFrames = 20; // Exact match to Python
    private string _lastDirection = "forward"; // Track last direction

    // Statistics and FPS tracking
    private int _framesProcessed = 0;
    private DateTime _startTime;
    private DateTime _lastFpsReport;

    // Dynamic line color setting
    public byte[]? LineColor { get; set; } = null;
    public byte[]? StopAtColor { get; set; } = null;
    public bool FloorColorDetected => _floorColorDetected;
    private bool _floorColorDetected = false;

    // Beacon detection logging throttle
    private int _beaconDetectionCount = 0;

    // 3-Check Beacon Verification System
    private int _consecutiveBeaconDetections = 0;
    private const int REQUIRED_CONSECUTIVE_DETECTIONS = 3;
    private DateTime _lastBeaconVerificationCheck = DateTime.MinValue;
    private const int VERIFICATION_CHECK_INTERVAL_MS = 500; // Check every 500ms
    private bool _arrivalConfirmed = false; // Flag to prevent further beacon processing after arrival
    private bool _verifyingBeacon = false; // Flag to pause line following during beacon verification
    private DateTime _navigationStartTime = DateTime.MinValue; // Track when navigation started
    private const int BEACON_COOLDOWN_MS = 3000; // Wait 3 seconds after starting navigation before checking beacons

    public LineFollowerService(
        ILogger<LineFollowerService> logger,
        IConfiguration config,
        LineDetectionCameraService cameraService,
        LineFollowerMotorService motorService,
        BluetoothBeaconService beaconService,
        UltrasonicSensorService ultrasonicService,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _config = config;
        _cameraService = cameraService;
        _motorService = motorService;
        _beaconService = beaconService;
        _ultrasonicService = ultrasonicService;
        _serviceProvider = serviceProvider;

        // Subscribe to beacon detection events for real-time proximity checking
        _beaconService.BeaconDetected += OnBeaconDetected;
        _ultrasonicService.DistanceChanged += OnDistanceChanged;

        _logger.LogInformation("Line Follower Service initialized (15fps target)");
        _logger.LogInformation("   PID: Kp={Kp}, Ki={Ki}, Kd={Kd}", _kp, _ki, _kd);
        _logger.LogInformation("   Thresholds: Small={Small}, Medium={Medium}, Extreme={Extreme}",
            _smallErrorThreshold, _mediumErrorThreshold, _extremeErrorThreshold);
        _logger.LogInformation("   (frame delay: {FrameDelay}ms)", _frameDelayMs);
        _logger.LogInformation("   Line lost max frames: {MaxFrames}", _maxLineLostFrames);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Line Follower Robot...");

        // Initialize services

        await _cameraService.InitializeAsync();

        await _motorService.InitializeAsync();

        await _ultrasonicService.InitializeAsync();
        _ultrasonicService.Start();

        _startTime = DateTime.UtcNow;
        _lastFpsReport = _startTime;

        _logger.LogInformation("Line Follower Robot started successfully");

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("⚡ LineFollowerService ExecuteAsync STARTED - entering main loop");

        bool wasFollowingLine = false;

        try
        {
            _logger.LogInformation("⚡ Entering while loop...");
            while (!stoppingToken.IsCancellationRequested)
            {
                var loopStart = DateTime.UtcNow;

                // Check if we should be following the line
                bool shouldFollowLine = _motorService.IsLineFollowingActive;

                // DEBUG: Log every loop iteration
                if (_framesProcessed % 50 == 0)
                {
                    _logger.LogInformation("DEBUG: shouldFollowLine={ShouldFollow}, wasFollowingLine={WasFollowing}",
                        shouldFollowLine, wasFollowingLine);
                }

                if (shouldFollowLine && !wasFollowingLine)
                {
                    _logger.LogInformation("Starting line following (server command received)");
                    _startTime = DateTime.UtcNow;
                    _lastFpsReport = _startTime;
                    _framesProcessed = 0;
                    // Reset PID state
                    _previousError = 0;
                    _integral = 0;
                    _lineLostCounter = 0;
                    // Reset beacon verification
                    _consecutiveBeaconDetections = 0;
                    _lastBeaconVerificationCheck = DateTime.MinValue;
                    _arrivalConfirmed = false; // Reset arrival flag when starting new navigation
                    _verifyingBeacon = false; // Reset verification pause flag
                    _navigationStartTime = DateTime.UtcNow; // Start cooldown timer
                    wasFollowingLine = true;
                }
                else if (!shouldFollowLine && wasFollowingLine)
                {
                    _logger.LogInformation("Stopping line following (server command received)");
                    _motorService.Stop();
                    wasFollowingLine = false;
                }

                if (shouldFollowLine)
                {
                    // PAUSE line following if we're verifying a beacon
                    if (_verifyingBeacon)
                    {
                        _motorService.Stop(); // Ensure motors stay stopped
                        await Task.Delay(100, stoppingToken);
                        continue; // Skip line detection until verification completes
                    }

                    // Check for obstacle - TEMPORARILY DISABLED
                    // if (_obstacleDetected)
                    // {
                    //     _logger.LogWarning("⚡ OBSTACLE DETECTED - stopping motors");
                    //     _motorService.Stop();
                    //     await Task.Delay(100, stoppingToken);
                    //     continue;
                    // }

                    var currentTime = DateTime.UtcNow;

                    // Detect line
                    _logger.LogDebug("⚡ Detecting line...");
                    var detection = _cameraService.DetectLine(this.LineColor);

                    // Update statistics
                    _framesProcessed++;

                    // Log every 10th frame to avoid spam
                    if (_framesProcessed % 10 == 0)
                    {
                        _logger.LogInformation(
                            "Frame #{Frame}: LineDetected={Detected}, Position={Pos}, Error={Error}, Method={Method}",
                            _framesProcessed, detection.LineDetected, detection.LinePosition, detection.Error,
                            detection.DetectionMethod);
                    }

                    // Report FPS every 5 seconds - matching Python
                    if ((currentTime - _lastFpsReport).TotalSeconds >= 5.0)
                    {
                        var fps = _framesProcessed / (currentTime - _lastFpsReport).TotalSeconds;
                        _logger.LogInformation("Line following running at {FPS:F1} FPS", fps);
                        _framesProcessed = 0;
                        _lastFpsReport = currentTime;
                    }

                    // Process detection result
                    await ProcessLineDetection(detection);

                    var processingTime = (DateTime.UtcNow - loopStart).TotalMilliseconds;
                    var sleepTime = Math.Max(1, _frameDelayMs - (int)processingTime);

                    await Task.Delay(sleepTime, stoppingToken);
                }
                else
                {
                    // Not following line, just wait a bit
                    await Task.Delay(100, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Line following cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in line following loop");
        }
        finally
        {
            _motorService.Stop();
            _logger.LogInformation("Line following stopped");
        }
    }

    private async Task ProcessLineDetection(LineDetectionResult detection)
    {
        if (detection.LineDetected && detection.LinePosition.HasValue)
        {
            // Line detected - reset counter - matching Python
            _lineLostCounter = 0;

            if (!_lineDetected)
            {
                _logger.LogInformation("Line detected");
                _lineDetected = true;
            }

            // Calculate PID control - matching Python exactly
            var error = detection.Error;

            // PID computation - matching Python exactly
            _integral = Math.Max(-100, Math.Min(100, _integral + error)); // Prevent integral windup
            var derivative = error - _previousError;
            var pidOutput = _kp * error + _ki * _integral + _kd * derivative;
            _previousError = error;

            // Log PID values occasionally - matching Python
            if (_framesProcessed % 20 == 0)
            {
                _logger.LogInformation("Line error: {Error}, PID output: {Output:F2}", error, pidOutput);
            }

            // Track last direction for recovery - matching Python exactly
            if (error > 0)
                _lastDirection = "left";
            else if (error < 0)
                _lastDirection = "right";

            // Check for floor color detection (room arrival)
            if (StopAtColor != null && !_floorColorDetected)
            {
                if (_cameraService.DetectFloorColor(StopAtColor))
                {
                    _floorColorDetected = true;
                    _logger.LogWarning("FLOOR COLOR DETECTED - Arrived at room!");
                }
            }

            // Execute movement based on error magnitude - matching Python exactly
            await ExecuteMovement(error);
        }
        else
        {
            // No line detected - matching Python logic
            if (_lineDetected)
            {
                _logger.LogWarning("Line lost (counter: {Counter})", _lineLostCounter);
                _lineDetected = false;
            }
            else if (_lineLostCounter % 20 == 0) // Log every 20 frames when line is lost
            {
                _logger.LogInformation("Still searching for line (lost frames: {Counter})", _lineLostCounter);
            }

            _lineLostCounter++;
            await HandleLineLoss();
        }
    }

    private async Task ExecuteMovement(int error)
    {
        var absError = Math.Abs(error);

        // Movement logic - matching Python exactly
        if (absError < _smallErrorThreshold)
        {
            // Very small error - go straight
            _logger.LogInformation("Motor command: MOVE_FORWARD (error={Error}, threshold<{Threshold})", error,
                _smallErrorThreshold);
            _motorService.MoveForward();
        }
        else if (absError < _mediumErrorThreshold)
        {
            // Small-medium error - gentle correction
            if (error > 0)
            {
                _logger.LogInformation("Motor command: LEFT_FORWARD (error={Error}, left correction)", error);
                _motorService.LeftForward();
            }
            else
            {
                _logger.LogInformation("Motor command: RIGHT_FORWARD (error={Error}, right correction)", error);
                _motorService.RightForward();
            }
        }
        else if (absError < _extremeErrorThreshold)
        {
            // Large error - stronger correction
            if (error > 0)
            {
                _logger.LogInformation("Motor command: LEFT_FORWARD_STRONG (error={Error})", error);
                _motorService.LeftForward();
            }
            else
            {
                _logger.LogInformation("Motor command: RIGHT_FORWARD_STRONG (error={Error})", error);
                _motorService.RightForward();
            }
        }
        else
        {
            // Extreme error - full turn
            if (error > 0)
            {
                _logger.LogInformation("Motor command: TURN_LEFT (error={Error})", error);
                _motorService.TurnLeft();
            }
            else
            {
                _logger.LogInformation("Motor command: TURN_RIGHT (error={Error})", error);
                _motorService.TurnRight();
            }
        }

        await Task.CompletedTask;
    }

    private async Task HandleLineLoss()
    {
        // Line loss handling - matching Python logic exactly

        if (_lineLostCounter >= _maxLineLostFrames)
        {
            // Line lost for too many frames - implement search pattern
            _lineLostCounter = _maxLineLostFrames; // Cap the counter

            // Use alternating search pattern
            var searchCycle = _lineLostCounter % 40;

            if (searchCycle < 20)
            {
                // Search in opposite direction of last known direction
                if (_lastDirection == "left")
                {
                    _logger.LogInformation("Line search: sweeping right");
                    _motorService.SearchRight();
                }
                else
                {
                    _logger.LogInformation("Line search: sweeping left");
                    _motorService.SearchLeft();
                }
            }
            else
            {
                // Alternate search direction
                if (_lastDirection == "left")
                {
                    _logger.LogInformation("Line search: sweeping left");
                    _motorService.SearchLeft();
                }
                else
                {
                    _logger.LogInformation("Line search: sweeping right");
                    _motorService.SearchRight();
                }
            }
        }
        else
        {
            // Brief line loss - continue in last direction
            _logger.LogInformation("Brief line loss, continuing in last direction: {Direction}", _lastDirection);

            switch (_lastDirection)
            {
                case "left":
                    _motorService.LeftForward();
                    break;
                case "right":
                    _motorService.RightForward();
                    break;
                default:
                    _motorService.MoveForward();
                    break;
            }

            // Add extra delay for recovery
            await Task.Delay(50);
        }
    }

    /// <summary>
    /// Handle beacon detection events with 3-CHECK VERIFICATION ALGORITHM
    /// Algorithm:
    /// 1. Detect beacon in range → Stop and check RSSI
    /// 2. Wait 500ms, check again → If still in range, count++
    /// 3. Repeat 3 times total
    /// 4. If all 3 checks pass → Confirm arrival and stop completely
    /// 5. If any check fails → Reset counter and continue moving
    /// </summary>
    private async void OnBeaconDetected(object? sender, BeaconInfo beacon)
    {
        try
        {
            // If arrival already confirmed, ignore all further beacon detections
            if (_arrivalConfirmed)
            {
                return; // Stop processing beacons - we've already arrived!
            }

            var isLineFollowing = _motorService.IsLineFollowingActive;

            // Only care about beacons if we're line following
            if (!isLineFollowing)
            {
                _consecutiveBeaconDetections = 0; // Reset if not following
                return;
            }
            
            // COOLDOWN: Wait 3 seconds after starting navigation before checking beacons
            // This prevents false arrivals when already near the target beacon
            var timeSinceNavigationStart = (DateTime.UtcNow - _navigationStartTime).TotalMilliseconds;
            if (timeSinceNavigationStart < BEACON_COOLDOWN_MS)
            {
                return; // Still in cooldown period
            }

            // Get active beacons from server communication service
            var serverCommService = _serviceProvider.GetService<RobotServerCommunicationService>();
            if (serverCommService?.ActiveBeacons == null || !serverCommService.ActiveBeacons.Any())
            {
                return;
            }

            // Find if this beacon is an active target (navigation target OR base beacon)
            // IMPORTANT: We need to verify ALL beacons, including the base for return trips
            var targetBeaconConfig = serverCommService.ActiveBeacons
                .FirstOrDefault(b => string.Equals(b.MacAddress, beacon.MacAddress,
                                         StringComparison.OrdinalIgnoreCase));

            if (targetBeaconConfig == null)
            {
                return; // Not in active beacons list, ignore
            }

            // Skip if this is neither a navigation target nor a base beacon
            if (!targetBeaconConfig.IsNavigationTarget && !targetBeaconConfig.IsBase)
            {
                return; // Not a target we care about
            }

            // Only log every 20th detection to reduce spam
            _beaconDetectionCount++;
            var beaconType = targetBeaconConfig.IsBase ? "BASE" : "TARGET";
            if (_beaconDetectionCount % 20 == 0)
            {
                _logger.LogInformation(
                    "{Type} BEACON DETECTED: {BeaconMac} ({Name}) RSSI: {Rssi} dBm (threshold: {Threshold} dBm)",
                    beaconType, beacon.MacAddress, beacon.Name ?? "Unknown", beacon.Rssi, targetBeaconConfig.RssiThreshold);
            }

            // Check if we've reached the target (within RSSI threshold)
            if (beacon.Rssi >= targetBeaconConfig.RssiThreshold)
            {
                // 3-CHECK VERIFICATION SYSTEM
                var now = DateTime.UtcNow;

                // Only increment if enough time has passed since last check (avoid spam)
                if ((now - _lastBeaconVerificationCheck).TotalMilliseconds >= VERIFICATION_CHECK_INTERVAL_MS)
                {
                    _consecutiveBeaconDetections++;
                    _lastBeaconVerificationCheck = now;

                    _logger.LogWarning(
                        "{Type} BEACON CHECK [{Check}/3]: Beacon {BeaconMac} ({Name}) RSSI: {Rssi} dBm >= {Threshold} dBm - STOPPING TO VERIFY",
                        beaconType, _consecutiveBeaconDetections, beacon.MacAddress, beacon.Name ?? "Unknown", beacon.Rssi, targetBeaconConfig.RssiThreshold);

                    // PAUSE line following and stop the robot for verification
                    _verifyingBeacon = true;
                    _motorService.Stop();

                    // If we've verified 3 times consecutively, we've arrived!
                    if (_consecutiveBeaconDetections >= REQUIRED_CONSECUTIVE_DETECTIONS)
                    {
                        _logger.LogWarning(
                            "✓✓✓ {Type} BEACON CONFIRMED! 3 consecutive checks passed. Beacon {BeaconMac} ({Name}) - ARRIVAL CONFIRMED!",
                            beaconType, beacon.MacAddress, beacon.Name ?? "Unknown");

                        _arrivalConfirmed = true; // Set flag to ignore all future beacon detections
                        await _motorService.StopLineFollowingAsync();
                        _consecutiveBeaconDetections = 0; // Reset for next navigation

                        _logger.LogInformation("🛑 Arrival confirmed - beacon processing disabled until next navigation starts");
                    }
                    else
                    {
                        _logger.LogInformation(
                            "⏸ Verification in progress... Waiting for check {NextCheck}/3 (next check in {Ms}ms)",
                            _consecutiveBeaconDetections + 1, VERIFICATION_CHECK_INTERVAL_MS);
                    }
                }
            }
            else
            {
                // RSSI dropped below threshold during verification
                if (_consecutiveBeaconDetections > 0 && _verifyingBeacon)
                {
                    // Check if we should give up on verification (been trying for too long)
                    var timeSinceFirstCheck = (DateTime.UtcNow - _lastBeaconVerificationCheck).TotalMilliseconds;

                    // Only give up if we haven't gotten a good reading for 2 seconds
                    if (timeSinceFirstCheck > 2000)
                    {
                        _logger.LogWarning(
                            "✗ VERIFICATION TIMEOUT! RSSI: {Rssi} < {Threshold}. No good readings for 2s. Resetting counter and RESUMING line following.",
                            beacon.Rssi, targetBeaconConfig.RssiThreshold);

                        _consecutiveBeaconDetections = 0;
                        _lastBeaconVerificationCheck = DateTime.MinValue;
                        _verifyingBeacon = false; // Resume line following

                        _logger.LogInformation("▶ Line following resumed - verification timeout");
                    }
                    else
                    {
                        // Just a temporary dip - keep verifying
                        _logger.LogDebug("Temporary RSSI dip: {Rssi} < {Threshold} - continuing verification",
                            beacon.Rssi, targetBeaconConfig.RssiThreshold);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnBeaconDetected for beacon {BeaconMac}", beacon?.MacAddress ?? "Unknown");
        }
    }

    private void OnDistanceChanged(object? sender, double distance)
    {
        _obstacleDetected = distance < UltrasonicSensorService.STOP_DISTANCE;
        if (_obstacleDetected)
        {
            _logger.LogWarning("Obstacle detected at {Distance:F2}m - STOPPING", distance);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Line Follower Robot...");

        // Unsubscribe from events
        _beaconService.BeaconDetected -= OnBeaconDetected;
        _ultrasonicService.DistanceChanged -= OnDistanceChanged;
        _ultrasonicService.Stop();

        _motorService.Stop();

        await base.StopAsync(cancellationToken);

        _logger.LogInformation("Line Follower Robot stopped");
    }
}