using RobotProject.Shared.DTOs;

namespace LineFollowerRobot.Interfaces
{
    /// <summary>
    /// Interface for Bluetooth beacon scanning and room tracking functionality
    /// Provides methods to detect and track Bluetooth beacons for robot navigation and location awareness
    /// </summary>
    public interface IBluetoothBeaconService
    {
        /// <summary>
        /// Initialize the Bluetooth beacon service
        /// Sets up the Bluetooth adapter and prepares for scanning
        /// </summary>
        /// <returns>Task representing the async initialization</returns>
        Task InitializeAsync();
        
        /// <summary>
        /// Start scanning for Bluetooth beacons
        /// Begins periodic scanning and detection of nearby beacons
        /// </summary>
        /// <returns>Task representing the async scanning start</returns>
        Task StartScanningAsync();
        
        /// <summary>
        /// Stop scanning for Bluetooth beacons
        /// Stops the scanning process and clears detected beacon data
        /// </summary>
        /// <returns>Task representing the async scanning stop</returns>
        Task StopScanningAsync();
        
        /// <summary>
        /// Get the currently detected beacons with their RSSI values
        /// Returns a list of all beacons detected in the current scan cycle
        /// </summary>
        /// <returns>List of detected beacon information</returns>
        List<BeaconInfo> GetDetectedBeacons();
        
        /// <summary>
        /// Get the primary/strongest beacon currently detected
        /// Returns the beacon with the strongest signal or null if none detected
        /// </summary>
        /// <returns>Primary beacon info or null</returns>
        BeaconInfo? GetPrimaryBeacon();
        
        /// <summary>
        /// Update the list of known beacons that should be tracked
        /// This is called when the server provides updated beacon configuration
        /// </summary>
        /// <param name="beaconConfigurations">List of beacon configurations from server</param>
        void UpdateKnownBeacons(List<BeaconConfigurationDto> beaconConfigurations);
        
        /// <summary>
        /// Check if the robot is currently within range of any beacon
        /// Uses RSSI threshold to determine proximity
        /// </summary>
        /// <returns>True if within range of at least one beacon</returns>
        bool IsInRange();
        
        /// <summary>
        /// Check if the robot is within range of a specific beacon
        /// </summary>
        /// <param name="macAddress">MAC address of the beacon to check</param>
        /// <returns>True if within range of the specified beacon</returns>
        bool IsInRangeOfBeacon(string macAddress);
        
        /// <summary>
        /// Get the name of the room the robot is currently in
        /// Based on the strongest beacon signal
        /// </summary>
        /// <returns>Room name or null if not in range of any beacon</returns>
        string? GetCurrentRoom();
        
        /// <summary>
        /// Event triggered when a new beacon is detected or when beacon data changes
        /// </summary>
        event EventHandler<BeaconInfo>? BeaconDetected;
        
        /// <summary>
        /// Event triggered when the robot enters or exits a room
        /// </summary>
        event EventHandler<string?>? RoomChanged;
    }
}