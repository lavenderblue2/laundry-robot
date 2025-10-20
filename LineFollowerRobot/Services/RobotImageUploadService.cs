using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;

namespace LineFollowerRobot.Services;

/// <summary>
/// Service responsible for uploading camera images to the administrator server
/// Sends current camera frame every 1 second for real-time monitoring
/// </summary>
public class RobotImageUploadService : BackgroundService
{
    private readonly ILogger<RobotImageUploadService> _logger;
    private readonly IConfiguration _configuration;
    private readonly LineDetectionCameraService _cameraService;
    private readonly HttpClient _httpClient;
    
    private readonly string _robotName;
    private readonly string _serverBaseUrl;
    private readonly int _uploadIntervalMs;
    private readonly bool _enabled;

    public RobotImageUploadService(
        ILogger<RobotImageUploadService> logger,
        IConfiguration configuration,
        LineDetectionCameraService cameraService)
    {
        _logger = logger;
        _configuration = configuration;
        _cameraService = cameraService;

        // Initialize HttpClient with proper settings
        var handler = new HttpClientHandler()
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        };
        _httpClient = new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(10); // Shorter timeout for image uploads
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "LineFollowerRobot-ImageUploader/1.0");

        // Load configuration
        _robotName = _configuration["Robot:Name"] ?? Environment.MachineName;
        _serverBaseUrl = _configuration["Robot:ServerBaseUrl"] ?? "http://localhost:5000";
        _uploadIntervalMs = _configuration.GetValue<int>("Robot:ImageUploadIntervalMs", 1000);
        _enabled = _configuration.GetValue<bool>("Robot:ImageUploadEnabled", true);

        if (_enabled)
        {
            _logger.LogInformation("🖼️ Robot Image Upload Service initialized - uploading to '{ServerUrl}' every {IntervalMs}ms",
                _serverBaseUrl, _uploadIntervalMs);
        }
        else
        {
            _logger.LogInformation("🖼️ Robot Image Upload Service disabled via configuration");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("Image upload service is disabled, exiting");
            return;
        }

        _logger.LogInformation("Starting robot image upload service");

        // Wait for camera service to be ready
        await Task.Delay(3000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UploadCameraImage(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading camera image to server");
            }

            try
            {
                await Task.Delay(_uploadIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Robot image upload service stopped");
    }

    /// <summary>
    /// Upload current camera frame to the server
    /// </summary>
    private async Task UploadCameraImage(CancellationToken cancellationToken)
    {
        try
        {
            // Get latest camera frame as JPEG
            var imageBytes = _cameraService.GetLatestFrameJpeg();
            if (imageBytes == null || imageBytes.Length == 0)
            {
                _logger.LogDebug("No camera frame available for upload");
                return;
            }

            // Ensure the image is JPEG with quality 88 using ImageSharp auto-detection
            byte[] finalImageBytes;
            try
            {
                using var inputStream = new MemoryStream(imageBytes);
                using var outputStream = new MemoryStream();
                using var image = await Image.LoadAsync(inputStream, cancellationToken);
                
                // Add timestamp to image
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                
                image.Mutate(ctx =>
                {
                    // Create font for timestamp
                    var font = SystemFonts.CreateFont("Arial", 16, FontStyle.Bold);
                   
                    // Draw black stroke (outline)
                    ctx.DrawText(timestamp, font, Color.Black, new PointF(12, 12));
                    ctx.DrawText(timestamp, font, Color.Black, new PointF(8, 8));
                    ctx.DrawText(timestamp, font, Color.Black, new PointF(12, 8));
                    ctx.DrawText(timestamp, font, Color.Black, new PointF(8, 12));
                    
                    // Draw white text on top
                    ctx.DrawText(timestamp, font, Color.White, new PointF(10, 10));
                });
                
                // Save as JPEG with quality 88
                await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = 88 }, cancellationToken: cancellationToken);
                finalImageBytes = outputStream.ToArray();
                 
                _logger.LogDebug("Processed image with timestamp: {OriginalSize} -> {FinalSize} bytes", 
                    imageBytes.Length, finalImageBytes.Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process image, using original bytes");
                finalImageBytes = imageBytes;
            }

            // Create form data for image upload
            using var content = new MultipartFormDataContent();
            
            // Add image file
            var imageContent = new ByteArrayContent(finalImageBytes);
            imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            content.Add(imageContent, "image", $"{_robotName}_camera_{DateTime.UtcNow:yyyyMMdd_HHmmss}.jpg");

            // Add metadata as JSON
            var metadata = new
            {
                captureTime = DateTime.UtcNow,
                robotName = _robotName,
                imageSize = finalImageBytes.Length,
                imageType = "camera_frame",
                processingInfo = new
                {
                    hasLineDetection = true,
                    uploadedAt = DateTime.UtcNow
                }
            };
            
            var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions 
            { 
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
            });
            content.Add(new StringContent(metadataJson, Encoding.UTF8, "application/json"), "metadata");

            // Send POST request to image upload endpoint
            var endpoint = $"{_serverBaseUrl}/api/Robot/{_robotName}/upload-image";
            var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Successfully uploaded camera image ({Size} bytes) to server", finalImageBytes.Length);
            }
            else
            {
                _logger.LogWarning("Image upload failed with status {StatusCode}: {ReasonPhrase}", 
                    response.StatusCode, response.ReasonPhrase);
                
                // Log response content for debugging
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Server response: {ResponseContent}", responseContent);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Network error during image upload - server may be unreachable");
        }
        catch (TaskCanceledException ex)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Image upload timed out");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during image upload");
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