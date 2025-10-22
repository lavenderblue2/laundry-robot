using RobotProject.Shared.DTOs;

namespace AdministratorWeb.Models.DTOs
{
    /// <summary>
    /// DTO for the Robots/Details page
    /// Contains all robot information including detected beacons
    /// </summary>
    public class RobotDetailsDto
    {
        /// <summary>
        /// Basic robot information
        /// </summary> 
        public string Name { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool CanAcceptRequests { get; set; }
        public RobotStatus Status { get; set; }
        public string? CurrentTask { get; set; }
        public string? CurrentLocation { get; set; }
        public DateTime ConnectedAt { get; set; }
        public DateTime LastPing { get; set; }
        public bool IsOffline { get; set; }
        public bool IsFollowingLine { get; set; }

        /// <summary>
        /// Actual line following state - true if robot is following line due to manual control OR active request
        /// </summary>
        public bool IsActuallyLineFollowing { get; set; }

        /// <summary>
        /// True if robot is line following due to an active request (not manual control)
        /// </summary>
        public bool IsFollowingDueToRequest { get; set; }

        /// <summary>
        /// Line following color settings
        /// </summary>
        public byte FollowColorR { get; set; }
        public byte FollowColorG { get; set; }
        public byte FollowColorB { get; set; }

        /// <summary>
        /// Helper property to get follow color as hex string for color picker
        /// </summary>
        public string FollowColorHex => $"#{FollowColorR:X2}{FollowColorG:X2}{FollowColorB:X2}";

        /// <summary>
        /// Camera data if available
        /// </summary>
        public RobotCameraDataDto? CameraData { get; set; }
        
        /// <summary>
        /// List of beacons currently being detected by this robot
        /// </summary>
        public List<RobotDetectedBeaconDto> DetectedBeacons { get; set; } = new();
        
        /// <summary>
        /// Connection statistics
        /// </summary>
        public TimeSpan ConnectedDuration => DateTime.UtcNow - ConnectedAt;
        public TimeSpan TimeSinceLastPing => DateTime.UtcNow - LastPing;
        
        /// <summary>
        /// Beacon detection summary
        /// </summary>
        public int TotalBeaconsDetected => DetectedBeacons.Count;
        public int ActiveBeacons => DetectedBeacons.Count(b => b.Status == BeaconDetectionStatus.Active);
        public int LostBeacons => DetectedBeacons.Count(b => b.Status == BeaconDetectionStatus.Lost);
        public int TimeoutBeacons => DetectedBeacons.Count(b => b.Status == BeaconDetectionStatus.Timeout);
    }
    
    /// <summary>
    /// DTO for camera data in robot details
    /// </summary>
    public class RobotCameraDataDto
    {
        public bool LineDetected { get; set; }
        public int? LinePosition { get; set; }
        public int FrameWidth { get; set; }
        public int FrameCenter { get; set; }
        public int Error { get; set; }
        public string DetectionMethod { get; set; } = string.Empty;
        public bool UsingMemory { get; set; }
        public double TimeSinceLastLine { get; set; }
        public DateTime LastUpdate { get; set; }
        public bool HasImage => ImageData != null && ImageData.Length > 0;
        public byte[]? ImageData { get; set; }
    }
    
    /// <summary>
    /// DTO for detected beacon information in robot details
    /// </summary>
    public class RobotDetectedBeaconDto
    {
        public string MacAddress { get; set; } = string.Empty;
        public string BeaconName { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public short CurrentRssi { get; set; }
        public double DistanceMeters { get; set; }
        public bool IsInRange { get; set; }
        public DateTime FirstDetected { get; set; }
        public DateTime LastDetected { get; set; }
        public int DetectionCount { get; set; }
        public double AverageRssi { get; set; }
        public BeaconSignalStrength SignalStrength { get; set; }
        public BeaconDetectionStatus Status { get; set; }
        
        /// <summary>
        /// Time since last detection
        /// </summary>
        public TimeSpan TimeSinceLastDetection => DateTime.UtcNow - LastDetected;
        
        /// <summary>
        /// Detection duration
        /// </summary>
        public TimeSpan DetectionDuration => LastDetected - FirstDetected;
        
        /// <summary>
        /// Signal strength as percentage for UI
        /// </summary>
        public int SignalPercentage => Math.Max(0, Math.Min(100, (int)((CurrentRssi + 100) * 1.25)));
        
        /// <summary>
        /// CSS class for signal strength indicator
        /// </summary>
        public string SignalStrengthCssClass => SignalStrength switch
        {
            BeaconSignalStrength.Excellent => "text-emerald-400",
            BeaconSignalStrength.Good => "text-green-400", 
            BeaconSignalStrength.Fair => "text-yellow-400",
            BeaconSignalStrength.Weak => "text-orange-400",
            BeaconSignalStrength.VeryWeak => "text-red-400",
            _ => "text-slate-400"
        };
        
        /// <summary>
        /// CSS class for status indicator
        /// </summary>
        public string StatusCssClass => Status switch
        {
            BeaconDetectionStatus.Active => "bg-emerald-900/50 text-emerald-300 border-emerald-700/50",
            BeaconDetectionStatus.Lost => "bg-orange-900/50 text-orange-300 border-orange-700/50",
            BeaconDetectionStatus.Timeout => "bg-red-900/50 text-red-300 border-red-700/50",
            _ => "bg-slate-900/50 text-slate-300 border-slate-700/50"
        };
    }
}