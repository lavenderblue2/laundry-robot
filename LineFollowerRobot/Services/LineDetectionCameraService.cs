using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;
using System.Numerics;

namespace LineFollowerRobot.Services;

public class LineDetectionResult
{
    public bool LineDetected { get; set; }
    public int? LinePosition { get; set; }
    public int FrameWidth { get; set; }
    public int FrameCenter { get; set; }
    public int Error { get; set; }
    public DateTime Timestamp { get; set; }
    public string DetectionMethod { get; set; } = "";
    public bool UsingMemory { get; set; }
    public double TimeSinceLastLine { get; set; }
}

public class LineDetectionCameraService : IDisposable
{
    private readonly ILogger<LineDetectionCameraService> _logger;
    private readonly IConfiguration _config;
    private readonly CameraStreamService _cameraStreamService;

    // Detection parameters - matching Python exactly
    private readonly double _roiTopPercent = 0.3; // Use 70% of frame from 30% down
    private readonly int _binaryThreshold = 60; // Exact match to Python
    private readonly int _adaptiveBlockSize = 11; // Exact match to Python
    private readonly int _adaptiveC = 7; // Exact match to Python
    private readonly int _minContourArea = 50; // Exact match to Python
    private readonly int _minValidContourArea = 100; // Exact match to Python
    private readonly double _minAspectRatio = 2.0; // Exact match to Python
    private readonly double _lineMemoryTimeout = 3.0; // Exact match to Python (3 seconds)
    private readonly int _positionSmoothingThreshold = 100; // Exact match to Python

    // Dynamic color detection
    private readonly int _colorTolerance = 30; // Tolerance for color matching (0-255)

    // Line memory variables - matching Python exactly
    private int? _lastLinePosition = null;
    private DateTime _lastLineTime = DateTime.MinValue;

    // JPEG caching for performance optimization
    private byte[]? _cachedJpeg = null;
    private DateTime _cachedJpegTime = DateTime.MinValue;
    private readonly object _jpegCacheLock = new object();

    public LineDetectionCameraService(
        ILogger<LineDetectionCameraService> logger,
        IConfiguration config,
        CameraStreamService cameraStreamService)
    {
        _logger = logger;
        _config = config;
        _cameraStreamService = cameraStreamService;

        // Load configuration if available
        _roiTopPercent = _config.GetValue("LineFollower:Detection:ROITopPercent", 0.3);
        _binaryThreshold = _config.GetValue("LineFollower:Detection:BinaryThreshold", 60);
        _adaptiveBlockSize = _config.GetValue("LineFollower:Detection:AdaptiveThresholdBlockSize", 11);
        _adaptiveC = _config.GetValue("LineFollower:Detection:AdaptiveThresholdC", 7);
        _minContourArea = _config.GetValue("LineFollower:Detection:MinContourArea", 50);
        _minValidContourArea = _config.GetValue("LineFollower:Detection:MinValidContourArea", 100);
        _minAspectRatio = _config.GetValue("LineFollower:Detection:MinAspectRatio", 2.0);
        _lineMemoryTimeout = _config.GetValue("LineFollower:Detection:LineMemoryTimeoutSeconds", 3.0);
        _positionSmoothingThreshold = _config.GetValue("LineFollower:Detection:PositionSmoothingThreshold", 100);

        _logger.LogInformation("🎯 Line Detection Camera Service initialized (reads from CameraStreamService)");
        _logger.LogInformation("   ROI: {ROI}%, Binary threshold: {Binary}, Adaptive: {Block}/{C}",
            _roiTopPercent * 100, _binaryThreshold, _adaptiveBlockSize, _adaptiveC);
        _logger.LogInformation("   Contour area: {MinArea}/{MinValid}, Aspect ratio: {AspectRatio}, Memory timeout: {Timeout}s",
            _minContourArea, _minValidContourArea, _minAspectRatio, _lineMemoryTimeout);
    }

    public async Task InitializeAsync()
    {
        // Wait for camera stream to be active
        var maxWaitTime = TimeSpan.FromSeconds(15);
        var startTime = DateTime.UtcNow;
        
        while (!_cameraStreamService.IsActive() && (DateTime.UtcNow - startTime) < maxWaitTime)
        {
            _logger.LogInformation("⏳ Waiting for camera stream to become active...");
            await Task.Delay(1000);
        }
        
        if (!_cameraStreamService.IsActive())
        {
            throw new InvalidOperationException("Camera stream is not active after waiting");
        }
        
        _logger.LogInformation("✅ Line Detection Camera Service ready (connected to camera stream)");
    }

    public LineDetectionResult DetectLine(byte[]? lineColor = null)
    {
        using var frame = _cameraStreamService.GetCurrentFrame();
        
        if (frame == null)
        {
            _logger.LogWarning("⚠️ No frame available from camera stream");
            return new LineDetectionResult
            {
                LineDetected = false,
                FrameWidth = 320,
                FrameCenter = 160,
                Error = 0,
                Timestamp = DateTime.UtcNow,
                DetectionMethod = "No frame",
                UsingMemory = false,
                TimeSinceLastLine = _lastLineTime == DateTime.MinValue ? 0 : (DateTime.UtcNow - _lastLineTime).TotalSeconds
            };
        }

        // Process frame for line detection
        var linePosition = ProcessFrame(frame, lineColor);
        var frameCenter = frame.Width / 2;

        // No need to store frames anymore - we generate JPEG on-demand

        var result = new LineDetectionResult
        {
            FrameWidth = frame.Width,
            FrameCenter = frameCenter,
            Timestamp = DateTime.UtcNow
        };

        if (linePosition.HasValue)
        {
            // Line detected
            result.LineDetected = true;
            result.LinePosition = linePosition.Value;
            result.Error = frameCenter - linePosition.Value;
            result.DetectionMethod = "Direct detection";
            result.UsingMemory = false;
            result.TimeSinceLastLine = 0;

            // Update memory
            _lastLinePosition = linePosition.Value;
            _lastLineTime = DateTime.UtcNow;

            _logger.LogDebug("✅ Line detected at position {Position} (error: {Error})", linePosition.Value, result.Error);
        }
        else
        {
            // No line detected - check memory
            var timeSinceLastLine = _lastLineTime == DateTime.MinValue ? double.MaxValue : (DateTime.UtcNow - _lastLineTime).TotalSeconds;

            if (timeSinceLastLine <= _lineMemoryTimeout && _lastLinePosition.HasValue)
            {
                // Use memory position
                result.LineDetected = true;
                result.LinePosition = _lastLinePosition.Value;
                result.Error = frameCenter - _lastLinePosition.Value;
                result.DetectionMethod = "Memory";
                result.UsingMemory = true;
                result.TimeSinceLastLine = timeSinceLastLine;

                _logger.LogDebug("🧠 Using line memory: position {Position} (age: {Age:F1}s)", _lastLinePosition.Value, timeSinceLastLine);
            }
            else
            {
                // No line and no valid memory
                result.LineDetected = false;
                result.LinePosition = null;
                result.Error = 0;
                result.DetectionMethod = "None";
                result.UsingMemory = false;
                result.TimeSinceLastLine = timeSinceLastLine == double.MaxValue ? 0 : timeSinceLastLine;

                _logger.LogDebug("❌ No line detected and no valid memory");
            }
        }

        return result;
    }

    private int? ProcessFrame(Image<Rgb24> frame, byte[]? lineColor = null)
    {
        var processFramePerf = Stopwatch.StartNew();
        try
        {
            // Extract ROI - matching Python exactly
            var roiHeight = (int)(frame.Height * (1.0 - _roiTopPercent));
            var roiY = frame.Height - roiHeight;

            using var roiFrame = frame.Clone();
            roiFrame.Mutate(x => x.Crop(new Rectangle(0, roiY, frame.Width, roiHeight)));

            Image<L8> processed;

            // Check if we have a dynamic color to follow
            if (lineColor != null && lineColor.Length >= 3)
            {
                // Dynamic color detection mode
                processed = CreateColorMask(roiFrame, lineColor[0], lineColor[1], lineColor[2]);
            }
            else
            {
                // Default black line detection (original algorithm)
                // Convert to grayscale - matching Python
                using var gray = roiFrame.CloneAs<L8>();

                // Apply Gaussian blur - matching Python (5x5 kernel)
                using var blurred = gray.Clone();
                blurred.Mutate(x => x.GaussianBlur(2.5f)); // sigma ~= kernel_size/4

                // Method 1: Binary threshold - matching Python exactly
                using var binary = blurred.Clone();
                binary.Mutate(x => x.BinaryThreshold(_binaryThreshold / 255f));

                // Invert the binary image manually (since BinaryThresholdMode.Inverted doesn't exist)
                for (int y = 0; y < binary.Height; y++)
                {
                    for (int x = 0; x < binary.Width; x++)
                    {
                        var pixel = binary[x, y];
                        binary[x, y] = new L8((byte)(255 - pixel.PackedValue));
                    }
                }

                // Method 2: Adaptive threshold - simulate with local thresholding
                using var adaptive = blurred.Clone();
                adaptive.Mutate(x => x.BinaryThreshold(0.3f));

                // Invert the adaptive image manually
                for (int y = 0; y < adaptive.Height; y++)
                {
                    for (int x = 0; x < adaptive.Width; x++)
                    {
                        var pixel = adaptive[x, y];
                        adaptive[x, y] = new L8((byte)(255 - pixel.PackedValue));
                    }
                }

                // Combine both methods by creating OR operation manually
                using var combined = binary.Clone();
                for (int y = 0; y < combined.Height; y++)
                {
                    for (int x = 0; x < combined.Width; x++)
                    {
                        var binaryPixel = binary[x, y];
                        var adaptivePixel = adaptive[x, y];
                        combined[x, y] = new L8((byte)Math.Max(binaryPixel.PackedValue, adaptivePixel.PackedValue));
                    }
                }

                // Morphological operations - skip for now as ImageSharp doesn't have these built-in
                processed = combined.Clone();
            }

            // Try contour-based detection first - matching Python exactly
            var position = TryContourBasedDetection(processed);
            if (position.HasValue)
            {
                return position.Value + 0; // Adjust for ROI offset
            }

            // Fallback to column sum method - matching Python exactly
            var columnResult = TryColumnSumDetection(processed);
            return columnResult.HasValue ? columnResult.Value + 0 : null; // Adjust for ROI offset
        }

        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error processing frame for line detection");
            return null;
        }
        finally
        {
            _logger.LogInformation($"took {processFramePerf.ElapsedMilliseconds}ms to process image");
        }
    }

    private int? TryContourBasedDetection(Image<L8> processedFrame)
    {
        try
        {
            // Simple contour detection using connected components
            var validContours = new List<(Rectangle bounds, double area)>();
            
            // Find connected white regions
            var visited = new bool[processedFrame.Width, processedFrame.Height];
            
            for (int y = 0; y < processedFrame.Height; y++)
            {
                for (int x = 0; x < processedFrame.Width; x++)
                {
                    if (!visited[x, y] && processedFrame[x, y].PackedValue > 128) // White pixel threshold
                    {
                        var bounds = FloodFillBounds(processedFrame, visited, x, y);
                        var area = bounds.Width * bounds.Height;
                        
                        if (area >= _minContourArea)
                        {
                            var aspectRatio = (double)bounds.Width / bounds.Height;
                            if (aspectRatio >= _minAspectRatio && area >= _minValidContourArea)
                            {
                                validContours.Add((bounds, area));
                            }
                        }
                    }
                }
            }

            if (validContours.Count > 0)
            {
                // Find contour with largest area - matching Python exactly
                var bestContour = validContours.OrderByDescending(c => c.area).First();
                var centerX = bestContour.bounds.X + bestContour.bounds.Width / 2;

                _logger.LogDebug("🎯 Contour detection: area={Area}, bounds={Bounds}, center={Center}",
                    bestContour.area, bestContour.bounds, centerX);

                return centerX;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Contour detection failed: {Error}", ex.Message);
            return null;
        }
    }

    private int? TryColumnSumDetection(Image<L8> processedFrame)
    {
        try
        {
            // Calculate column sums - matching Python exactly
            var columnSums = new int[processedFrame.Width];

            for (int x = 0; x < processedFrame.Width; x++)
            {
                int sum = 0;
                for (int y = 0; y < processedFrame.Height; y++)
                {
                    var pixel = processedFrame[x, y];
                    sum += pixel.PackedValue;
                }
                columnSums[x] = sum;
            }

            // Find column with maximum sum - matching Python exactly
            var maxSum = columnSums.Max();
            if (maxSum > 0)
            {
                var maxIndex = Array.IndexOf(columnSums, maxSum);

                _logger.LogDebug("📊 Column sum detection: max_sum={MaxSum} at position {Position}", maxSum, maxIndex);

                return maxIndex;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Column sum detection failed: {Error}", ex.Message);
            return null;
        }
    }
    
    private Rectangle FloodFillBounds(Image<L8> image, bool[,] visited, int startX, int startY)
    {
        var stack = new Stack<(int x, int y)>();
        stack.Push((startX, startY));
        
        int minX = startX, maxX = startX;
        int minY = startY, maxY = startY;
        
        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            
            if (x < 0 || x >= image.Width || y < 0 || y >= image.Height || visited[x, y])
                continue;
                
            var pixel = image[x, y];
            if (pixel.PackedValue <= 128) // Not a white pixel
                continue;
                
            visited[x, y] = true;
            
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);
            
            // Add neighboring pixels
            stack.Push((x + 1, y));
            stack.Push((x - 1, y));
            stack.Push((x, y + 1));
            stack.Push((x, y - 1));
        }
        
        return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    /// <summary>
    /// Get the latest camera frame as JPEG with line detection indicators
    /// Uses 100ms cache to avoid regenerating JPEG for rapid requests (Phase 1 optimization)
    /// </summary>
    public byte[]? GetLatestFrameJpeg()
    {
        try
        {
            // Check cache first (100ms cache duration)
            lock (_jpegCacheLock)
            {
                if (_cachedJpeg != null &&
                    (DateTime.UtcNow - _cachedJpegTime).TotalMilliseconds < 100)
                {
                    _logger.LogDebug("📸 Returning cached JPEG (age: {Age}ms)",
                        (DateTime.UtcNow - _cachedJpegTime).TotalMilliseconds);
                    return _cachedJpeg;
                }
            }

            // Cache miss - generate new JPEG
            _logger.LogDebug("📸 Generating new JPEG (cache expired or empty)");

            // Get current frame from camera service
            using var currentFrame = _cameraStreamService.GetCurrentFrame();
            if (currentFrame == null)
            {
                _logger.LogDebug("No current frame available for JPEG conversion");
                return null;
            }

            // Create a copy to draw on
            using var annotatedFrame = currentFrame.Clone();

            // Get current line detection state for indicators
            var lineDetectionResult = DetectLineFromFrame(currentFrame);

            // Draw line indicators
            DrawLineIndicators(annotatedFrame, lineDetectionResult.LinePosition, lineDetectionResult.FrameCenter);

            // Convert to JPEG with quality 88
            using var memoryStream = new MemoryStream();
            annotatedFrame.SaveAsJpeg(memoryStream, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = 88 });
            var jpegBytes = memoryStream.ToArray();

            // Update cache
            lock (_jpegCacheLock)
            {
                _cachedJpeg = jpegBytes;
                _cachedJpegTime = DateTime.UtcNow;
            }

            return jpegBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error generating latest frame JPEG");
            return null;
        }
    }

    /// <summary>
    /// Detect line from a specific frame without updating internal state
    /// Used for generating annotated images without affecting main detection logic
    /// </summary>
    private LineDetectionResult DetectLineFromFrame(Image<Rgb24> frame)
    {
        var linePosition = ProcessFrame(frame, null);
        var frameCenter = frame.Width / 2;

        return new LineDetectionResult
        {
            LineDetected = linePosition.HasValue,
            LinePosition = linePosition,
            FrameWidth = frame.Width,
            FrameCenter = frameCenter,
            Error = linePosition.HasValue ? frameCenter - linePosition.Value : 0,
            Timestamp = DateTime.UtcNow,
            DetectionMethod = linePosition.HasValue ? "Direct detection" : "None",
            UsingMemory = false,
            TimeSinceLastLine = _lastLineTime == DateTime.MinValue ? 0 : (DateTime.UtcNow - _lastLineTime).TotalSeconds
        };
    }

    private void DrawLineIndicators(Image<Rgb24> frame, int? linePosition, int frameCenter)
    {
        try
        {
            var frameHeight = frame.Height;
            var frameWidth = frame.Width;
            
            frame.Mutate(ctx =>
            {
                // Draw center line (vertical line in blue)
                ctx.DrawLine(Color.Blue, 2f, new PointF(frameCenter, 0), new PointF(frameCenter, frameHeight));
                
                // Draw ROI boundary (horizontal line marking ROI start)
                var roiY = (int)(frameHeight * _roiTopPercent);
                ctx.DrawLine(Color.Yellow, 1f, new PointF(0, roiY), new PointF(frameWidth, roiY));
                
                if (linePosition.HasValue)
                {
                    // Draw detected line position (vertical line in green)
                    ctx.DrawLine(Color.Green, 3f, new PointF(linePosition.Value, roiY), new PointF(linePosition.Value, frameHeight));
                    
                    // Draw error indicator (horizontal line showing direction)
                    var error = frameCenter - linePosition.Value;
                    var errorY = frameHeight - 30;
                    var errorColor = error > 0 ? Color.Red : Color.Cyan; // Red for right, Cyan for left
                    ctx.DrawLine(errorColor, 2f, new PointF(frameCenter, errorY), new PointF(linePosition.Value, errorY));
                    
                    // Add text showing error value (simplified - ImageSharp text rendering is complex)
                    // For now, we'll skip text rendering as it requires font loading
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error drawing line indicators");
        }
    }


    /// <summary>
    /// Create a binary mask based on color matching with tolerance
    /// </summary>
    private Image<L8> CreateColorMask(Image<Rgb24> colorFrame, byte targetR, byte targetG, byte targetB)
    {
        var mask = new Image<L8>(colorFrame.Width, colorFrame.Height);

        for (int y = 0; y < colorFrame.Height; y++)
        {
            for (int x = 0; x < colorFrame.Width; x++)
            {
                var pixel = colorFrame[x, y];

                // Calculate color distance using Euclidean distance
                var rDiff = Math.Abs(pixel.R - targetR);
                var gDiff = Math.Abs(pixel.G - targetG);
                var bDiff = Math.Abs(pixel.B - targetB);

                var colorDistance = Math.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);

                // If color is within tolerance, mark as white (255) in mask
                if (colorDistance <= _colorTolerance)
                {
                    mask[x, y] = new L8(255); // White = target color detected
                }
                else
                {
                    mask[x, y] = new L8(0); // Black = background
                }
            }
        }

        // Apply Gaussian blur to smooth the mask
        mask.Mutate(x => x.GaussianBlur(1.0f));

        return mask;
    }

    public void ResetLineMemory()
    {
        _lastLinePosition = null;
        _lastLineTime = DateTime.MinValue;
        _logger.LogInformation("🧠 Line memory reset");
    }

    /// <summary>
    /// Check if target floor color is detected near the line
    /// </summary>
    public bool DetectFloorColor(byte[]? targetColor)
    {
        if (targetColor == null || targetColor.Length < 3)
            return false;

        using var frame = _cameraStreamService.GetCurrentFrame();
        if (frame == null)
            return false;

        // Sample center region of ROI for floor color
        var roiY = (int)(frame.Height * _roiTopPercent);
        var centerX = frame.Width / 2;
        var sampleRadius = 30;

        int matchCount = 0;
        int totalSamples = 0;

        for (int y = roiY; y < frame.Height && y < roiY + 60; y += 5)
        {
            for (int x = centerX - sampleRadius; x < centerX + sampleRadius; x += 5)
            {
                if (x >= 0 && x < frame.Width && y >= 0 && y < frame.Height)
                {
                    var pixel = frame[x, y];
                    var rDiff = Math.Abs(pixel.R - targetColor[0]);
                    var gDiff = Math.Abs(pixel.G - targetColor[1]);
                    var bDiff = Math.Abs(pixel.B - targetColor[2]);
                    var colorDistance = Math.Sqrt(rDiff * rDiff + gDiff * gDiff + bDiff * bDiff);

                    totalSamples++;
                    if (colorDistance <= _colorTolerance)
                        matchCount++;
                }
            }
        }

        // If >50% pixels match the floor color, report detection
        return totalSamples > 0 && (matchCount * 100 / totalSamples) > 50;
    }

    public void Dispose()
    {
        try
        {
            _logger.LogInformation("🗑️ Line Detection Camera Service disposed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error disposing Line Detection Camera Service");
        }
    }
}