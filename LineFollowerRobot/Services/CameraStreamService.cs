using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LineFollowerRobot.Services.CameraMiddleware;
using FFMpegCore;
using FFMpegCore.Pipes;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading.Channels;

namespace LineFollowerRobot.Services;

public static class ProcessExtensions
{
    public static string? GetCommandLine(this Process process)
    {
        try
        {
            // For Linux/Unix systems, read from /proc/[pid]/cmdline
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                var cmdlinePath = $"/proc/{process.Id}/cmdline";
                if (File.Exists(cmdlinePath))
                {
                    var cmdline = File.ReadAllText(cmdlinePath);
                    return cmdline.Replace('\0', ' ').Trim();
                }
            }
        }
        catch
        {
            // Ignore errors and fall back
        }
        
        try
        {
            return process.StartInfo.Arguments;
        }
        catch
        {
            return null;
        }
    }
}

public class CameraStreamService : BackgroundService
{
    private readonly ILogger<CameraStreamService> _logger;
    private readonly IConfiguration _config;
    private readonly IServiceProvider _serviceProvider;
    
    private Process? _ffmpegProcess;
    private readonly object _frameLock = new();
    private Image<Rgb24>? _currentFrame;
    private DateTime _lastFrameTime = DateTime.MinValue;
    private CancellationTokenSource? _ffmpegCancellation;
    
    // Camera configuration
    private readonly int _cameraWidth;
    private readonly int _cameraHeight;
    private readonly int _cameraFps;
    private readonly int _bufferSize;
    
    // Frame processing
    private readonly Channel<byte[]> _frameChannel;
    private readonly ChannelWriter<byte[]> _frameWriter;
    private readonly ChannelReader<byte[]> _frameReader;
    
    // Middleware pipeline
    private List<ICameraMiddleware> _middlewares = new();

    public CameraStreamService(
        ILogger<CameraStreamService> logger,
        IConfiguration config,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _config = config;
        _serviceProvider = serviceProvider;
        
        _cameraWidth = _config.GetValue("LineFollower:Camera:Width", 320);
        _cameraHeight = _config.GetValue("LineFollower:Camera:Height", 240);
        _cameraFps = _config.GetValue("LineFollower:Camera:FPS", 30);
        _bufferSize = _config.GetValue("LineFollower:Camera:BufferSize", 1);
        
        // Create channel for frame processing
        var channelOptions = new BoundedChannelOptions(capacity: _bufferSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = true
        };
        _frameChannel = Channel.CreateBounded<byte[]>(channelOptions);
        _frameWriter = _frameChannel.Writer;
        _frameReader = _frameChannel.Reader;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🎥 Starting Camera Stream Service");
        
        // Kill any existing ffmpeg processes from previous runs
        await KillExistingFfmpegProcessesAsync();
        
        // Initialize camera
        await InitializeCameraAsync();
        
        // Initialize middleware pipeline
        InitializeMiddleware();
        
        await base.StartAsync(cancellationToken);
    }

    private async Task InitializeCameraAsync()
    {
        try
        {
            _ffmpegCancellation = new CancellationTokenSource();
            
            // Configure FFmpeg to capture from camera and output raw RGB frames
            // Try simpler configuration to avoid grid artifacts
            var ffmpegArgs = $"-f v4l2 -video_size {_cameraWidth}x{_cameraHeight} -framerate {_cameraFps} -i /dev/video0 -f rawvideo -pix_fmt rgb24 -vsync 1 -";
            
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = ffmpegArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true, // Enable stderr for debugging
                CreateNoWindow = true
            };
            
            _ffmpegProcess = Process.Start(processStartInfo);
            
            if (_ffmpegProcess == null)
            {
                throw new InvalidOperationException("Could not start ffmpeg process");
            }
            
            _logger.LogInformation("✅ FFmpeg camera initialized: {Width}x{Height} @ {FPS}fps (PID: {ProcessId})", 
                _cameraWidth, _cameraHeight, _cameraFps, _ffmpegProcess.Id);
            _logger.LogInformation("🔧 FFmpeg command: ffmpeg {Args}", ffmpegArgs);
                
            // Start a task to read stderr for debugging
            _ = Task.Run(async () =>
            {
                try
                {
                    var reader = _ffmpegProcess.StandardError;
                    string? line;
                    while (!_ffmpegProcess.HasExited && (line = await reader.ReadLineAsync()) != null)
                    {
                        if (line.Contains("error", StringComparison.OrdinalIgnoreCase) || 
                            line.Contains("warning", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogWarning("📹 FFmpeg: {Message}", line);
                        }
                        else
                        {
                            _logger.LogDebug("📹 FFmpeg: {Message}", line);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Error reading FFmpeg stderr: {Error}", ex.Message);
                }
            });
                
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to initialize camera with FFmpeg");
            throw;
        }
    }

    private void InitializeMiddleware()
    {
        try
        {
            // Get all camera middleware services
            _middlewares = _serviceProvider.GetServices<ICameraMiddleware>()
                .Where(m => m.IsEnabled)
                .OrderBy(m => m.Priority)
                .ToList();
                
            _logger.LogInformation("🔧 Initialized {Count} camera middleware(s): {Middlewares}", 
                _middlewares.Count, 
                string.Join(", ", _middlewares.Select(m => m.GetType().Name)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to initialize camera middleware");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🎥 Camera stream loop started");
        
        var frameSize = _cameraWidth * _cameraHeight * 3; // RGB24 = 3 bytes per pixel
        var buffer = new byte[frameSize];
        
        // Start frame reading task
        var frameReadingTask = Task.Run(async () =>
        {
            try
            {
                if (_ffmpegProcess?.StandardOutput?.BaseStream == null)
                {
                    throw new InvalidOperationException("FFmpeg process or output stream is null");
                }
                
                var outputStream = _ffmpegProcess.StandardOutput.BaseStream;
                
                while (!stoppingToken.IsCancellationRequested && !_ffmpegProcess.HasExited)
                {
                    var bytesRead = 0;
                    var totalBytesRead = 0;
                    
                    // Read complete frame
                    while (totalBytesRead < frameSize && !stoppingToken.IsCancellationRequested)
                    {
                        bytesRead = await outputStream.ReadAsync(buffer.AsMemory(totalBytesRead, frameSize - totalBytesRead), stoppingToken);
                        if (bytesRead == 0) break; // End of stream
                        totalBytesRead += bytesRead;
                    }
                    
                    if (totalBytesRead == frameSize)
                    {
                        // Create a copy of the frame data
                        var frameData = new byte[frameSize];
                        Array.Copy(buffer, frameData, frameSize);
                        
                        // Try to write to channel (non-blocking)
                        if (!await _frameWriter.WaitToWriteAsync(stoppingToken))
                        {
                            break;
                        }
                        
                        await _frameWriter.WriteAsync(frameData, stoppingToken);
                    }
                    else if (totalBytesRead == 0)
                    {
                        _logger.LogWarning("⚠️ No data received from FFmpeg");
                        await Task.Delay(100, stoppingToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("🎥 Frame reading cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error reading frames from FFmpeg");
            }
            finally
            {
                _frameWriter.Complete();
            }
        }, stoppingToken);
        
        // Process frames
        try
        {
            await foreach (var frameData in _frameReader.ReadAllAsync(stoppingToken))
            {
                var loopStart = DateTime.UtcNow;
                
                try
                {
                    // Convert byte array to ImageSharp Image
                    using var image = Image.LoadPixelData<Rgb24>(frameData, _cameraWidth, _cameraHeight);
                    
                    // Process through middleware pipeline
                    await ProcessFrameThroughMiddleware(image, stoppingToken);
                    
                    // Store current frame for other services to read
                    lock (_frameLock)
                    {
                        _currentFrame?.Dispose();
                        _currentFrame = image.Clone();
                        _lastFrameTime = DateTime.UtcNow;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error processing frame");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("🎥 Camera stream cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in camera stream loop");
        }
        finally
        {
            await frameReadingTask;
            
            lock (_frameLock)
            {
                _currentFrame?.Dispose();
                _currentFrame = null;
            }
            _logger.LogInformation("🎥 Camera stream stopped");
        }
    }

    private async Task  ProcessFrameThroughMiddleware(Image<Rgb24> frame, CancellationToken cancellationToken)
    {
        try
        {
            var context = new CameraPipelineContext
            {
                FrameTimestamp = DateTime.UtcNow
            };
            
            // Process through each middleware in priority order
            foreach (var middleware in _middlewares)
            {
                if (context.ShouldStopPipeline)
                {
                    break;
                }
                
                try
                {
                    var shouldContinue = await middleware.ProcessFrameAsync(frame, cancellationToken);
                    if (!shouldContinue)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error in camera middleware {Middleware}", middleware.GetType().Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error processing frame through middleware pipeline");
        }
    }

    /// <summary>
    /// Get the current camera frame (thread-safe)
    /// </summary>
    public Image<Rgb24>? GetCurrentFrame()
    {
        lock (_frameLock)
        {
            return _currentFrame?.Clone();
        }
    }
    
    /// <summary>
    /// Get the timestamp of the last captured frame
    /// </summary>
    public DateTime GetLastFrameTime()
    {
        lock (_frameLock)
        {
            return _lastFrameTime;
        }
    }
    
    /// <summary>
    /// Check if camera is active and capturing frames
    /// </summary>
    public bool IsActive()
    {
        return _ffmpegProcess != null && !_ffmpegProcess.HasExited && (DateTime.UtcNow - GetLastFrameTime()).TotalSeconds < 5;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🎥 Stopping Camera Stream Service");
        
        // Cancel FFmpeg operations
        _ffmpegCancellation?.Cancel();
        
        await base.StopAsync(cancellationToken);
        
        // Stop and cleanup FFmpeg process
        await CleanupFfmpegProcessAsync();
        
        lock (_frameLock)
        {
            _currentFrame?.Dispose();
            _currentFrame = null;
        }
        
        _ffmpegCancellation?.Dispose();
        
        _logger.LogInformation("✅ Camera Stream Service stopped");
    }
    
    private async Task KillExistingFfmpegProcessesAsync()
    {
        try
        {
            var processes = Process.GetProcessesByName("ffmpeg");
            var ourProcesses = processes.Where(p => 
            {
                try
                {
                    return p.StartInfo.Arguments?.Contains("/dev/video0") == true ||
                           p.GetCommandLine()?.Contains("/dev/video0") == true;
                }
                catch
                {
                    // If we can't read process info, assume it might be ours
                    return true;
                }
            });
            
            foreach (var process in ourProcesses)
            {
                try
                {
                    _logger.LogInformation("🚫 Killing existing ffmpeg process (PID: {ProcessId})", process.Id);
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Failed to kill ffmpeg process {ProcessId}", process.Id);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error checking for existing ffmpeg processes");
        }
    }
    
    private async Task CleanupFfmpegProcessAsync()
    {
        if (_ffmpegProcess != null)
        {
            try
            {
                if (!_ffmpegProcess.HasExited)
                {
                    _logger.LogInformation("🚫 Terminating ffmpeg process (PID: {ProcessId})", _ffmpegProcess.Id);
                    _ffmpegProcess.Kill(entireProcessTree: true);
                    
                    var timeout = Task.Delay(5000);
                    var waitForExit = _ffmpegProcess.WaitForExitAsync();
                    
                    if (await Task.WhenAny(waitForExit, timeout) == timeout)
                    {
                        _logger.LogWarning("⚠️ FFmpeg process did not exit gracefully, force killing");
                        try
                        {
                            _ffmpegProcess.Kill(entireProcessTree: true);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "❌ Failed to force kill ffmpeg process");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error cleaning up ffmpeg process");
            }
            finally
            {
                _ffmpegProcess.Dispose();
                _ffmpegProcess = null;
            }
        }
    }
}