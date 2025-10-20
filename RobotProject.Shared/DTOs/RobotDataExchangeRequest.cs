using System;
using System.Collections.Generic;

namespace RobotProject.Shared.DTOs
{
    /// <summary>
    /// Request DTO for robot-server two-way metadata communication
    /// Contains data that the robot sends to the server during periodic data exchange
    /// This DTO is designed to be extensible for future robot metadata requirements
    /// </summary>
    public class RobotDataExchangeRequest
    {
        /// <summary>
        /// Name/identifier of the robot making the request
        /// </summary>
        public string RobotName { get; set; } = string.Empty;
        
        /// <summary>
        /// Timestamp when this data was collected by the robot
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Current battery level percentage (0-100)
        /// TODO: Add battery monitoring in future updates
        /// </summary>
        public int? BatteryLevel { get; set; }
        
        /// <summary>
        /// Current robot status/state
        /// TODO: Expand robot state tracking in future updates
        /// </summary>
        public string? CurrentStatus { get; set; }
        
        /// <summary>
        /// Current location/position information
        /// TODO: Add GPS/positioning data in future updates
        /// </summary>
        public string? CurrentLocation { get; set; }
        
        /// <summary>
        /// List of currently detected Bluetooth beacons with their RSSI values
        /// This is the primary data being exchanged in the initial implementation
        /// </summary>
        public List<BeaconInfo> DetectedBeacons { get; set; } = new();
        
        /// <summary>
        /// Indicates if the robot is currently within the navigation RSSI threshold of any target beacon
        /// Used to signal that the robot has successfully reached its navigation destination
        /// </summary>
        public bool IsInTarget { get; set; } = false;
        
        /// <summary>
        /// Current task or operation the robot is performing
        /// TODO: Add detailed task tracking in future updates
        /// </summary>
        public string? CurrentTask { get; set; }
        
        /// <summary>
        /// Any error messages or warnings from the robot
        /// TODO: Implement comprehensive error reporting in future updates
        /// </summary>
        public List<string> Errors { get; set; } = new();
        
        /// <summary>
        /// Additional sensor data (temperature, humidity, etc.)
        /// TODO: Add environmental sensor support in future updates
        /// </summary>
        public Dictionary<string, object>? SensorData { get; set; }
        
        /// <summary>
        /// Robot's current network connectivity information
        /// TODO: Add network diagnostics in future updates
        /// </summary>
        public string? NetworkStatus { get; set; }
        
        /// <summary>
        /// Version of the robot software/firmware
        /// TODO: Add version tracking for software updates
        /// </summary>
        public string? SoftwareVersion { get; set; }
        
        /// <summary>
        /// Current weight reading from HX711 sensor in kilograms
        /// Accurate to 4 decimal places (0.0001kg precision)
        /// </summary>
        public double WeightKg { get; set; } = 0.0;

        /// <summary>
        /// Ultrasonic sensor 1 obstacle distance in meters
        /// HC-SR04 sensor reading for obstacle detection
        /// </summary>
        public double USSensor1ObstacleDistance { get; set; } = 0.0;

    }
}