using System;

namespace RobotProject.Shared.DTOs
{
    /// <summary>
    /// Represents a beacon detection record for a specific robot
    /// Tracks when and how strongly a robot detects a beacon
    /// </summary>
    public class RobotDetectedBeacon
    {
        /// <summary>
        /// MAC address of the detected beacon
        /// </summary>
        public string MacAddress { get; set; } = string.Empty;
        
        /// <summary>
        /// Name of the beacon (from beacon registry)
        /// </summary>
        public string BeaconName { get; set; } = string.Empty;
        
        /// <summary>
        /// Room name associated with this beacon
        /// </summary>
        public string RoomName { get; set; } = string.Empty;
        
        /// <summary>
        /// Current RSSI value in dBm
        /// </summary>
        public short CurrentRssi { get; set; }
        
        /// <summary>
        /// Calculated distance to the beacon in meters
        /// </summary>
        public double DistanceMeters { get; set; }
        
        /// <summary>
        /// Whether the robot is currently "in range" based on RSSI threshold
        /// </summary>
        public bool IsInRange { get; set; }
        
        /// <summary>
        /// When this beacon was first detected by this robot
        /// </summary>
        public DateTime FirstDetected { get; set; }
        
        /// <summary>
        /// When this beacon was last detected by this robot
        /// </summary>
        public DateTime LastDetected { get; set; }
        
        /// <summary>
        /// Number of consecutive detection cycles
        /// </summary>
        public int DetectionCount { get; set; }
        
        /// <summary>
        /// Average RSSI over recent detections
        /// </summary>
        public double AverageRssi { get; set; }
        
        /// <summary>
        /// Signal strength category for UI display
        /// </summary>
        public BeaconSignalStrength SignalStrength => CurrentRssi switch
        {
            >= -30 => BeaconSignalStrength.Excellent,
            >= -50 => BeaconSignalStrength.Good,
            >= -70 => BeaconSignalStrength.Fair,
            >= -85 => BeaconSignalStrength.Weak,
            _ => BeaconSignalStrength.VeryWeak
        };
        
        /// <summary>
        /// Status of this beacon detection
        /// </summary>
        public BeaconDetectionStatus Status { get; set; } = BeaconDetectionStatus.Active;
    }
    
    /// <summary>
    /// Signal strength categories for beacon detection
    /// </summary>
    public enum BeaconSignalStrength
    {
        VeryWeak,
        Weak,
        Fair,
        Good,
        Excellent
    }
    
    /// <summary>
    /// Status of beacon detection
    /// </summary>
    public enum BeaconDetectionStatus
    {
        Active,      // Currently being detected
        Lost,        // No longer detected but recently was
        Timeout      // Not detected for extended period
    }
}