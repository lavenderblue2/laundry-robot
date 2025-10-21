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

    // SLIDING WINDOW BEACON DETECTION SYSTEM
    private readonly object _beaconLock = new object(); // Thread safety
    private readonly Dictionary<string, Queue<int>> _rssiBuffers = new(); // MAC -> RSSI buffer (10 samples)
    private readonly Dictionary<string, int> _beaconScores = new(); // MAC -> current score
    private readonly Dictionary<string, int> _confirmationCounter = new(); // MAC -> confirmation counter
    private const int RSSI_BUFFER_SIZE = 10; // 10 samples = 1 second window @ 100ms/sample
    private const int SCORE_THRESHOLD_CONFIRM = 15; // Need +15 points to trigger confirmation
    private const int SCORE_THRESHOLD_MAINTAIN = 10; // Stay above +10 during confirmation
    private const int CONFIRMATION_READINGS = 3; // Need 3 more readings above threshold after trigger
    private bool _arrivalConfirmed = false; // Flag to prevent further beacon processing after arrival

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
                    // Reset sliding window beacon detection
                    lock (_beaconLock)
                    {
                        _rssiBuffers.Clear();
                        _beaconScores.Clear();
                        _confirmationCounter.Clear();
                        _arrivalConfirmed = false; // Reset arrival flag when starting new navigation
                    }
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
                    // Check for obstacle
                    if (_obstacleDetected)
                    {
                        _logger.LogWarning("⚡ OBSTACLE DETECTED - stopping motors");
                        _motorService.Stop();
                        await Task.Delay(100, stoppingToken);
                        continue;
                    }

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
    /// Handle beacon detection events with SLIDING WINDOW + WEIGHTED SCORING ALGORITHM
    /// Algorithm:
    /// 1. Maintain 10-sample RSSI buffer per beacon (1 second window @ 100ms/sample)
    /// 2. Calculate weighted score based on signal quality:
    ///    - Strong signal (RSSI >= threshold): +2 points
    ///    - Acceptable signal (RSSI >= threshold - 5): +1 point
    ///    - Weak signal (RSSI < threshold - 5): -1 point
    ///    - Missing sample: -2 points
    /// 3. When score reaches +15: Trigger confirmation mode
    /// 4. During confirmation: Need 3 more readings with score staying above +10
    /// 5. Robot keeps moving until arrival is CONFIRMED (no premature stopping)
    /// 6. Thread-safe with lock to prevent race conditions
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
                lock (_beaconLock)
                {
                    _rssiBuffers.Clear();
                    _beaconScores.Clear();
                    _confirmationCounter.Clear();
                }
                return;
            }

            // Get active beacons from server communication service
            var serverCommService = _serviceProvider.GetService<RobotServerCommunicationService>();
            if (serverCommService?.ActiveBeacons == null || !serverCommService.ActiveBeacons.Any())
            {
                return;
            }

            // Find if this beacon is an active target (navigation target OR base beacon)
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

            var beaconType = targetBeaconConfig.IsBase ? "BASE" : "TARGET";
            var mac = beacon.MacAddress;

            // Variables to capture inside lock
            bool shouldStopRobot = false;
            int currentScore = 0;
            double avgRssi = 0;

            // THREAD-SAFE SLIDING WINDOW UPDATE
            lock (_beaconLock)
            {
                // Initialize buffer for this beacon if needed
                if (!_rssiBuffers.ContainsKey(mac))
                {
                    _rssiBuffers[mac] = new Queue<int>();
                    _beaconScores[mac] = 0;
                    _confirmationCounter[mac] = 0;
                }

                var buffer = _rssiBuffers[mac];

                // Add new RSSI sample to buffer
                buffer.Enqueue(beacon.Rssi);

                // Remove old samples (keep only last 10)
                while (buffer.Count > RSSI_BUFFER_SIZE)
                {
                    buffer.Dequeue();
                }

                // Calculate weighted score based on signal quality
                int newScore = CalculateWeightedScore(buffer, targetBeaconConfig.RssiThreshold);
                int oldScore = _beaconScores[mac];
                _beaconScores[mac] = newScore;
                currentScore = newScore;

                // Calculate moving average for logging
                avgRssi = buffer.Average();

                // Only log every 20th detection to reduce spam
                _beaconDetectionCount++;
                if (_beaconDetectionCount % 20 == 0)
                {
                    _logger.LogInformation(
                        "{Type} BEACON: {BeaconMac} ({Name}) | RSSI: {Rssi} dBm (avg: {Avg:F1}) | Score: {Score} | Samples: {Count}/{Max}",
                        beaconType, beacon.MacAddress, beacon.Name ?? "Unknown", beacon.Rssi, avgRssi,
                        newScore, buffer.Count, RSSI_BUFFER_SIZE);
                }

                // Check if we've triggered confirmation mode
                if (newScore >= SCORE_THRESHOLD_CONFIRM)
                {
                    // Increment confirmation counter
                    _confirmationCounter[mac]++;

                    _logger.LogWarning(
                        "{Type} BEACON CONFIRMATION [{Count}/{Required}]: {BeaconMac} ({Name}) | Score: {Score} | Avg RSSI: {Avg:F1} dBm",
                        beaconType, _confirmationCounter[mac], CONFIRMATION_READINGS, beacon.MacAddress,
                        beacon.Name ?? "Unknown", newScore, avgRssi);

                    // Check if we've confirmed arrival (score stayed high for 3 readings)
                    if (_confirmationCounter[mac] >= CONFIRMATION_READINGS && newScore >= SCORE_THRESHOLD_MAINTAIN)
                    {
                        _logger.LogWarning(
                            "✓✓✓ {Type} BEACON ARRIVAL CONFIRMED! Beacon {BeaconMac} ({Name}) | Final Score: {Score} | Avg RSSI: {Avg:F1} dBm - STOPPING NOW!",
                            beaconType, beacon.MacAddress, beacon.Name ?? "Unknown", newScore, avgRssi);

                        _arrivalConfirmed = true; // Set flag to ignore all future beacon detections
                        shouldStopRobot = true; // Signal to stop outside lock

                        // Clear all buffers for next navigation
                        _rssiBuffers.Clear();
                        _beaconScores.Clear();
                        _confirmationCounter.Clear();

                        _logger.LogInformation("🛑 Arrival confirmed - beacon processing disabled until next navigation starts");
                    }
                }
                else if (newScore < SCORE_THRESHOLD_MAINTAIN)
                {
                    // Score dropped - reset confirmation counter but keep buffer
                    if (_confirmationCounter[mac] > 0)
                    {
                        _logger.LogInformation(
                            "⚠ {Type} BEACON score dropped to {Score} - resetting confirmation counter (was {OldCount})",
                            beaconType, newScore, _confirmationCounter[mac]);
                        _confirmationCounter[mac] = 0;
                    }
                }
            }

            // STOP ROBOT OUTSIDE LOCK (can't await inside lock)
            if (shouldStopRobot)
            {
                await _motorService.StopLineFollowingAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnBeaconDetected for beacon {BeaconMac}", beacon?.MacAddress ?? "Unknown");
        }
    }

    /// <summary>
    /// Calculate weighted score for beacon proximity based on RSSI buffer
    /// </summary>
    private int CalculateWeightedScore(Queue<int> rssiBuffer, int threshold)
    {
        int score = 0;

        foreach (var rssi in rssiBuffer)
        {
            if (rssi >= threshold)
            {
                // Strong signal - definitely at beacon
                score += 2;
            }
            else if (rssi >= threshold - 5)
            {
                // Acceptable signal - probably at beacon (allows for small fluctuations)
                score += 1;
            }
            else if (rssi >= threshold - 10)
            {
                // Weak but present - neutral (0 points)
                score += 0;
            }
            else
            {
                // Too weak - probably not at beacon yet
                score -= 1;
            }
        }

        return score;
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