using System.Collections.Concurrent;
using RobotProject.Shared.DTOs;

namespace AdministratorWeb.Models
{
    public class ConnectedRobot
    {
        public string Name { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true; 
        public bool CanAcceptRequests { get; set; } = true;
        public RobotStatus Status { get; set; } = RobotStatus.Available;
        public string? CurrentTask { get; set; }
        public string? CurrentLocation { get; set; }
        public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastPing { get; set; } = DateTime.UtcNow;
        
        // Computed property - robot is offline if last ping was more than 5 seconds ago
        public bool IsOffline => DateTime.UtcNow - LastPing > TimeSpan.FromSeconds(5);
        
        // Command flags
        public bool IsFollowingLine { get; set; } = false;
        
        // Weight sensor data
        public double WeightKg { get; set; } = 0.0;

        // Ultrasonic sensor data
        public double USSensor1ObstacleDistance { get; set; } = 0.0;

        // Camera and detection data
        public RobotCameraData? CameraData { get; set; }
        
        // Beacon detection tracking - thread-safe dictionary keyed by MAC address
        public ConcurrentDictionary<string, RobotDetectedBeacon> DetectedBeacons { get; set; } = new();

        // Line following color settings (RGB values 0-255)
        public byte FollowColorR { get; set; } = 0; // Default to black
        public byte FollowColorG { get; set; } = 0; // Default to black
        public byte FollowColorB { get; set; } = 0; // Default to black

        // Helper property to get RGB as byte array for robot communication
        public byte[] FollowColorRgb => new[] { FollowColorR, FollowColorG, FollowColorB };
    }

    public class RobotCameraData
    {
        public bool LineDetected { get; set; }
        public int? LinePosition { get; set; }
        public int FrameWidth { get; set; }
        public int FrameCenter { get; set; }
        public int Error { get; set; }
        public string DetectionMethod { get; set; } = "";
        public bool UsingMemory { get; set; }
        public double TimeSinceLastLine { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
        public byte[]? ImageData { get; set; }
    }
}