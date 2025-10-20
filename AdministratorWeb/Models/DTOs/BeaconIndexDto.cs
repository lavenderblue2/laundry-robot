namespace AdministratorWeb.Models.DTOs
{
    /// <summary>
    /// DTO for the beacon management index page
    /// Contains summary data and beacon list for the admin interface
    /// </summary>
    public class BeaconIndexDto
    {
        /// <summary>
        /// Total number of beacons configured in the system
        /// </summary>
        public int TotalBeacons { get; set; }
        
        /// <summary>
        /// Number of active beacons
        /// </summary>
        public int ActiveBeacons { get; set; }
        
        /// <summary>
        /// Number of inactive/disabled beacons
        /// </summary>
        public int InactiveBeacons { get; set; }
        
        /// <summary>
        /// Number of beacons set as navigation targets
        /// </summary>
        public int NavigationTargets { get; set; }
        
        /// <summary>
        /// Number of beacons detected recently (within last hour)
        /// </summary>
        public int RecentlyDetected { get; set; }
        
        /// <summary>
        /// Number of unique rooms covered by beacons
        /// </summary>
        public int TotalRooms { get; set; }
        
        /// <summary>
        /// The base beacon (laundry room) if configured
        /// </summary>
        public BeaconWithStatusDto? BaseBeacon { get; set; }
        
        /// <summary>
        /// Number of users assigned to beacons
        /// </summary>
        public int AssignedUsers { get; set; }
        
        /// <summary>
        /// List of all beacons with their current status
        /// </summary>
        public List<BeaconWithStatusDto> Beacons { get; set; } = new();
        
        /// <summary>
        /// List of room names for filtering
        /// </summary>
        public List<string> AvailableRooms { get; set; } = new();
    }
    
    /// <summary>
    /// Extended beacon information with detection status
    /// </summary>
    public class BeaconWithStatusDto
    {
        /// <summary>
        /// Beacon database ID
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// MAC address of the beacon
        /// </summary>
        public string MacAddress { get; set; } = string.Empty;
        
        /// <summary>
        /// Beacon name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Room name
        /// </summary>
        public string RoomName { get; set; } = string.Empty;
        
        /// <summary>
        /// RSSI threshold
        /// </summary>
        public int RssiThreshold { get; set; }
        
        /// <summary>
        /// Whether beacon is active
        /// </summary>
        public bool IsActive { get; set; }
        
        /// <summary>
        /// Whether beacon is a navigation target
        /// </summary>
        public bool IsNavigationTarget { get; set; }
        
        /// <summary>
        /// Whether this beacon is the base/laundry room
        /// </summary>
        public bool IsBase { get; set; }
        
        /// <summary>
        /// Navigation priority
        /// </summary>
        public int Priority { get; set; }
        
        /// <summary>
        /// When the beacon was created
        /// </summary>
        public DateTime CreatedAt { get; set; }
        
        /// <summary>
        /// Last time this beacon was detected by a robot
        /// </summary>
        public DateTime? LastSeenAt { get; set; }
        
        /// <summary>
        /// Robot that last detected this beacon
        /// </summary>
        public string? LastSeenByRobot { get; set; }
        
        /// <summary>
        /// Last recorded RSSI value
        /// </summary>
        public int? LastRecordedRssi { get; set; }
        
        /// <summary>
        /// Number of users assigned to this beacon
        /// </summary>
        public int AssignedUserCount { get; set; }
        
        /// <summary>
        /// Whether the beacon has been detected recently (within 1 hour)
        /// </summary>
        public bool IsRecentlyDetected => LastSeenAt.HasValue && 
                                         (DateTime.UtcNow - LastSeenAt.Value).TotalHours <= 1;
                                         
        /// <summary>
        /// Status description for display
        /// </summary>
        public string StatusDescription => IsActive ? 
            (IsRecentlyDetected ? "Active & Detected" : "Active") : 
            "Inactive";
    }
}