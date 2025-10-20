using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LineFollowerRobot.Services.CameraMiddleware;

/// <summary>
/// Interface for camera middleware that processes frames in a pipeline
/// </summary>
public interface ICameraMiddleware
{
    /// <summary>
    /// Process a camera frame. Return true to continue pipeline, false to stop processing.
    /// </summary>
    Task<bool> ProcessFrameAsync(Image<Rgb24> frame, CancellationToken cancellationToken);
    
    /// <summary>
    /// Priority of this middleware (lower numbers execute first)
    /// </summary>
    int Priority { get; }
    
    /// <summary>
    /// Whether this middleware is enabled
    /// </summary>
    bool IsEnabled { get; }
}

/// <summary>
/// Pipeline context that can be shared between middleware
/// </summary>
public class CameraPipelineContext
{
    public Dictionary<string, object> Data { get; } = new();
    public DateTime FrameTimestamp { get; set; } = DateTime.UtcNow;
    public bool ShouldStopPipeline { get; set; } = false;
}