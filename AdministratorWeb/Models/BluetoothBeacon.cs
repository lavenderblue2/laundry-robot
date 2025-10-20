using System.ComponentModel.DataAnnotations;

namespace AdministratorWeb.Models
{
    /// <summary>
    /// Database model for Bluetooth beacons used in room tracking and robot navigation
    /// Stores beacon configuration and metadata for the autonomous laundry system
    /// </summary>
    public class BluetoothBeacon
    {
        /// <summary>
        /// Primary key for the beacon
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// MAC address of the beacon (unique identifier)
        /// Must be in format XX:XX:XX:XX:XX:XX
        /// </summary>
        [Required]
        [StringLength(17, MinimumLength = 17)]
        [RegularExpression(@"^([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}$", 
            ErrorMessage = "MAC address must be in format XX:XX:XX:XX:XX:XX")]
        public string MacAddress { get; set; } = string.Empty;
        
        /// <summary>
        /// Human-readable name for the beacon
        /// </summary>
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Name of the room where this beacon is located (optional)
        /// </summary>
        [StringLength(100)]
        public string? RoomName { get; set; }
        
        /// <summary>
        /// RSSI threshold for considering a robot "in range" of this beacon
        /// Typical values: -10 (very close) to -50 (far away)
        /// Default: -40 (medium-long range)
        /// </summary>
        [Required]
        [Range(-100, 0, ErrorMessage = "RSSI threshold must be between -100 and 0 dBm")]
        public int RssiThreshold { get; set; } = -40;
        
        /// <summary>
        /// Whether this beacon is currently active and should be tracked by robots
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Whether robots should navigate towards this beacon
        /// </summary>
        public bool IsNavigationTarget { get; set; } = false;
        
        /// <summary>
        /// Whether this beacon represents the base/laundry room location
        /// Only one beacon should be marked as base at a time
        /// </summary>
        public bool IsBase { get; set; } = false;
        
        /// <summary>
        /// Priority level for navigation (higher = more important)
        /// Used when multiple beacons are available for navigation
        /// </summary>
        [Range(1, 10, ErrorMessage = "Priority must be between 1 and 10")]
        public int Priority { get; set; } = 1;
        
        /// <summary>
        /// Optional description or notes about this beacon
        /// </summary>
        [StringLength(500)]
        public string? Description { get; set; }
        
        /// <summary>
        /// Timestamp when this beacon was added to the system
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Timestamp when this beacon configuration was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// User who created this beacon entry
        /// </summary>
        public string? CreatedBy { get; set; }
        
        /// <summary>
        /// User who last updated this beacon entry
        /// </summary>
        public string? UpdatedBy { get; set; }
        
        /// <summary>
        /// Last time this beacon was detected by any robot
        /// </summary>
        public DateTime? LastSeenAt { get; set; }
        
        /// <summary>
        /// Name of the robot that last detected this beacon
        /// </summary>
        public string? LastSeenByRobot { get; set; }
        
        /// <summary>
        /// Last recorded RSSI value from robot detection
        /// </summary>
        public int? LastRecordedRssi { get; set; }
        
    }
}