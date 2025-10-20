using System;

namespace RobotProject.Shared.DTOs
{
    /// <summary>
    /// Represents information about a detected Bluetooth beacon
    /// Used for room tracking and robot navigation
    /// </summary>
    public class BeaconInfo
    {
        /// <summary>
        /// MAC address of the beacon (unique identifier)
        /// </summary>
        public string MacAddress { get; set; } = string.Empty;
        
        /// <summary>
        /// Human-readable name/description of the beacon
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// RSSI (Received Signal Strength Indicator) in dBm
        /// Used to determine proximity to the beacon
        /// </summary>
        public short Rssi { get; set; }
        
        /// <summary>
        /// Calculated distance to the beacon in feet
        /// </summary>
        public double Distance { get; set; }
        
        /// <summary>
        /// Raw service data from the beacon (if available)
        /// </summary>
        public byte[]? ServiceData { get; set; }
        
        /// <summary>
        /// Timestamp when the beacon was last detected
        /// </summary>
        public DateTime LastSeen { get; set; }
        
        /// <summary>
        /// Room name associated with this beacon
        /// </summary>
        public string RoomName { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether this beacon is currently active and should be tracked
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// RSSI threshold for considering the robot "in range" of this beacon
        /// Default is -20 dBm (configurable)
        /// </summary>
        public int RssiThreshold { get; set; } = -20;
        
        /// <summary>
        /// Whether this beacon can be used as a navigation target
        /// </summary>
        public bool IsNavigationTarget { get; set; } = false;
        
        /// <summary>
        /// Navigation priority (higher = more important for pathfinding)
        /// </summary>
        public int Priority { get; set; } = 1;
    }
}