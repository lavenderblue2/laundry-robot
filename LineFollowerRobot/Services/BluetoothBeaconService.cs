using Linux.Bluetooth;
using Linux.Bluetooth.Extensions;
using LineFollowerRobot.Interfaces;
using RobotProject.Shared.DTOs;

namespace LineFollowerRobot.Services
{
    /// <summary>
    /// Service for scanning and tracking Bluetooth beacons for room navigation and location awareness
    /// Implements real-time beacon detection using Linux.Bluetooth library
    /// </summary>
    public class BluetoothBeaconService : IBluetoothBeaconService
    {
        private readonly ILogger<BluetoothBeaconService> _logger;
        private readonly IConfiguration _config;
        private IAdapter1? _adapter;
        private Timer? _scanTimer;
        private bool _isScanning = false;
        private bool _isInitialized = false;
        private int _scanCount = 0;
        private DateTime _lastBeaconLog = DateTime.UtcNow;
        
        // Thread-safe beacon tracking
        private readonly object _beaconLock = new();
        private readonly Dictionary<string, BeaconInfo> _detectedBeacons = new();
        private BeaconInfo? _primaryBeacon = null;
        private string? _currentRoom = null;
        
        // Server-provided beacon configurations
        private readonly Dictionary<string, BeaconConfigurationDto> _knownBeacons = new();
        
        // Default RSSI threshold from appsettings.json (configurable)
        private int _defaultRssiThreshold = -40;
        
        public event EventHandler<BeaconInfo>? BeaconDetected;
        public event EventHandler<string?>? RoomChanged;
        
        public BluetoothBeaconService(ILogger<BluetoothBeaconService> logger, IConfiguration config)
        {
            _logger = logger;
            _config = config;
            
            // Load default RSSI threshold from configuration
            _defaultRssiThreshold = _config.GetValue<int>("BluetoothBeacon:DefaultRssiThreshold", -40);
        }
        
        /// <summary>
        /// Initialize the Bluetooth adapter and prepare for beacon scanning
        /// </summary>
        public async Task InitializeAsync()
        {
            try
            {
                _logger.LogInformation("🔵 Initializing Bluetooth beacon service...");
                
                // Get the first available Bluetooth adapter
                var adapters = await BlueZManager.GetAdaptersAsync();
                if (!adapters.Any())
                {
                    throw new Exception("No Bluetooth adapters found on this system");
                }
                
                _adapter = adapters.First();
                _logger.LogInformation("🔵 Using Bluetooth adapter: {Name}", await _adapter.GetNameAsync());
                
                // Power on and configure the adapter
                await _adapter.SetPoweredAsync(true);
                
                // Set discovery filter for Low Energy devices only (faster scanning for beacons)
                try
                {
                    await _adapter.SetDiscoveryFilterAsync(new Dictionary<string, object>
                    {
                        ["Transport"] = "le" // Low Energy only for faster beacon scanning
                    }); 
                    _logger.LogInformation("🔵 Set discovery filter to LE devices only");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("⚠️ Could not set discovery filter: {Error}", ex.Message);
                    // Filter not supported, continue without it
                }
                
                _isInitialized = true;
                _logger.LogInformation("✅ Bluetooth beacon service initialized successfully with RSSI threshold: {Threshold}dBm", _defaultRssiThreshold);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to initialize Bluetooth beacon service");
                throw;
            }
        }
        
        /// <summary>
        /// Start periodic scanning for Bluetooth beacons
        /// </summary>
        public async Task StartScanningAsync()
        {
            if (!_isInitialized || _isScanning || _adapter == null)
            {
                return;
            }
            
            try
            {
                _logger.LogInformation("🔍 Starting Bluetooth beacon scan...");
                
                // Start Bluetooth discovery
                await _adapter.StartDiscoveryAsync();
                
                // Set up high-frequency periodic scan for real-time beacon tracking (100ms intervals)
                _scanTimer = new Timer(async _ => await ScanForBeaconsAsync(), null, 
                    TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(250));
                
                _isScanning = true;
                _logger.LogInformation("✅ Bluetooth beacon scanning started with 250ms intervals");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to start Bluetooth beacon scanning");
                throw;
            }
        }
        
        /// <summary>
        /// Stop beacon scanning and clean up resources
        /// </summary>
        public async Task StopScanningAsync()
        {
            if (!_isScanning)
            {
                return;
            }
            
            try
            {
                _logger.LogInformation("🛑 Stopping Bluetooth beacon scan...");
                
                // Stop the scan timer
                _scanTimer?.Dispose();
                _scanTimer = null;
                
                // Stop Bluetooth discovery
                if (_adapter != null)
                {
                    await _adapter.StopDiscoveryAsync();
                }
                
                _isScanning = false;
                
                // Clear detected beacon data
                lock (_beaconLock)
                {
                    _detectedBeacons.Clear();
                    _primaryBeacon = null;
                    _currentRoom = null;
                }
                
                _logger.LogInformation("✅ Bluetooth beacon scanning stopped and data cleared");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to stop Bluetooth beacon scanning");
            }
        }
        
        /// <summary>
        /// Main scanning loop - detects and processes beacon devices
        /// </summary>
        private async Task ScanForBeaconsAsync()
        {
            if (!_isInitialized || _adapter == null)
            {
                return;
            }
            
            try
            {
                // Periodically restart discovery to catch fresh beacon advertisements
                if (_scanCount % 20 == 0 && _adapter != null)
                {
                    try
                    {
                        await _adapter.StopDiscoveryAsync();
                        await Task.Delay(100);
                        await _adapter.StartDiscoveryAsync();
                    }
                    catch (Exception restartEx)
                    {
                        _logger.LogDebug("⚠️ Discovery restart failed: {Error}", restartEx.Message);
                    }
                }
                
                // Get all discovered devices
                var devices  = await _adapter.GetDevicesAsync();
                _scanCount++; 
                
                foreach (var device in devices)
                {
                    try
                    {
                        var address = await device.GetAddressAsync();
                        var name = await device.GetNameAsync();
                        
                        // Check if this device is a known beacon that we should track
                        if (IsKnownBeacon(address))
                        {
                            await ProcessBeaconDeviceAsync(device, address, name);
                        }
                        await Task.Delay(70);
                    }
                    catch (Exception deviceEx)
                    {
                        // Skip devices that can't be accessed (common in Bluetooth scanning)
                        _logger.LogDebug("⚠️ Could not access device: {Error}", deviceEx.Message);
                    }
                }
                
                // Update room tracking
                UpdateCurrentRoom();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error during beacon scan cycle");
            }
        }
        
        /// <summary>
        /// Check if a MAC address belongs to a known beacon we should track
        /// </summary>
        private bool IsKnownBeacon(string address)
        {
            lock (_beaconLock)
            {
                return _knownBeacons.ContainsKey(address.ToUpperInvariant()) ||
                       _knownBeacons.Keys.Any(k => string.Equals(k, address, StringComparison.OrdinalIgnoreCase));
            }
        }
        
        /// <summary>
        /// Process a detected beacon device and update tracking data
        /// </summary>
        private async Task ProcessBeaconDeviceAsync(Device device, string macAddress, string? deviceName)
        {
            try
            {
                // Get RSSI (signal strength) - this is crucial for proximity detection
                short rssi = 0;
                try
                {
                    rssi = await device.GetRSSIAsync();
                }
                catch (Exception)
                {
                    return; // Skip if RSSI not available
                }
                
                // Filter out clearly invalid RSSI readings
                if (rssi <= -100 || rssi >= 0)
                {
                    return;
                }
                
                // Try to get beacon service data (may not be available)
                byte[]? serviceData = null;
                try
                {
                    var serviceDataDict = await device.GetServiceDataAsync();
                    if (serviceDataDict != null && serviceDataDict.Any())
                    {
                        serviceData = serviceDataDict.Values.FirstOrDefault() as byte[];
                    }
                }
                catch (Exception)
                {
                    // Service data not available, continue without it
                }
                
                // Get beacon configuration (room name, RSSI threshold, etc.)
                var beaconConfig = GetBeaconConfiguration(macAddress);
                var rssiThreshold = beaconConfig?.RssiThreshold ?? _defaultRssiThreshold;
                var roomName = beaconConfig?.RoomName ?? "Unknown Room";
                var beaconName = beaconConfig?.Name ?? deviceName ?? $"Beacon-{macAddress.Substring(macAddress.Length - 5).Replace(":", "")}";
                
                // Calculate distance from RSSI
                var distance = CalculateDistance(rssi);
                
                // Create beacon info object
                var beacon = new BeaconInfo
                {
                    MacAddress = macAddress.ToUpperInvariant(),
                    Name = beaconName,
                    Rssi = rssi,
                    Distance = distance,
                    ServiceData = serviceData,
                    LastSeen = DateTime.UtcNow,
                    RoomName = roomName,
                    IsActive = beaconConfig?.IsActive ?? true,
                    RssiThreshold = rssiThreshold
                };
                
                // Update beacon tracking data
                lock (_beaconLock)
                {
                    _detectedBeacons[macAddress.ToUpperInvariant()] = beacon;
                    UpdatePrimaryBeacon();
                }
                
                // Trigger beacon detected event
                BeaconDetected?.Invoke(this, beacon);
                
                // Log beacon status periodically (every 1 second for real-time monitoring)
                var now = DateTime.UtcNow;
                if ((now - _lastBeaconLog).TotalSeconds >= 1)
                {
                    var status = beacon.Rssi >= rssiThreshold ? "✅ IN-RANGE" : "🔍 OUT-OF-RANGE";
                    _logger.LogDebug("📡 {Status} {Room}: {Name} - RSSI: {Rssi}dBm (threshold: {Threshold}dBm), Distance: {Distance:F1}ft", 
                        status, roomName, beacon.Name, beacon.Rssi, rssiThreshold, beacon.Distance);
                    _lastBeaconLog = now;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("⚠️ Could not process beacon device {MacAddress}: {Error}", macAddress, ex.Message);
            }
        }
        
        /// <summary>
        /// Calculate approximate distance from RSSI value (in feet)
        /// Based on real-world beacon testing data
        /// </summary>
        private double CalculateDistance(short rssi)
        {
            if (rssi <= -100 || rssi >= 0)
            {
                return -1.0; // Invalid reading
            }
            
            // Calibrated distance calculation based on actual beacon testing
            if (rssi >= -40) return 0.5;   // Very close - signal saturation
            if (rssi >= -50) return 3.0;   // 1-5 feet range
            if (rssi >= -65) return 8.0;   // 6-10 feet range  
            if (rssi >= -75) return 15.0;  // 10-20 feet range
            if (rssi >= -85) return 30.0;  // 20-40 feet range
            if (rssi >= -90) return 50.0;  // 40-60 feet range
            if (rssi >= -95) return 80.0;  // 60-100 feet range
            if (rssi >= -100) return 130.0; // 100-160 feet range
            
            return 160.0; // Maximum detection range
        }
        
        /// <summary>
        /// Update the primary beacon (strongest/closest signal)
        /// </summary>
        private void UpdatePrimaryBeacon()
        {
            var activeBeacons = _detectedBeacons.Values.Where(b => b.IsActive).ToList();
            
            if (!activeBeacons.Any())
            {
                _primaryBeacon = null;
                return;
            }
            
            // Find beacon with strongest signal (highest RSSI)
            _primaryBeacon = activeBeacons
                .Where(b => b.Rssi >= b.RssiThreshold) // Only consider beacons within range
                .OrderByDescending(b => b.Rssi)
                .FirstOrDefault();
        }
        
        /// <summary>
        /// Update current room tracking and trigger room change events
        /// </summary>
        private void UpdateCurrentRoom()
        {
            string? newRoom = null;
            
            lock (_beaconLock)
            {
                if (_primaryBeacon != null && _primaryBeacon.Rssi >= _primaryBeacon.RssiThreshold)
                {
                    newRoom = _primaryBeacon.RoomName;
                }
            }
            
            // Trigger room change event if room has changed
            if (newRoom != _currentRoom)
            {
                _currentRoom = newRoom;
                _logger.LogInformation("🏠 Room changed: {Room}", _currentRoom ?? "None");
                RoomChanged?.Invoke(this, _currentRoom);
            }
        }
        
        /// <summary>
        /// Get beacon configuration for a specific MAC address
        /// </summary>
        private BeaconConfigurationDto? GetBeaconConfiguration(string macAddress)
        {
            lock (_beaconLock)
            {
                var key = macAddress.ToUpperInvariant();
                return _knownBeacons.TryGetValue(key, out var config) ? config : null;
            }
        }
        
        #region Public Interface Implementation
        
        public List<BeaconInfo> GetDetectedBeacons()
        {
            lock (_beaconLock)
            {
                return _detectedBeacons.Values.ToList();
            }
        }
        
        public BeaconInfo? GetPrimaryBeacon()
        {
            lock (_beaconLock)
            {
                return _primaryBeacon;
            }
        }
        
        public void UpdateKnownBeacons(List<BeaconConfigurationDto> beaconConfigurations)
        {
            lock (_beaconLock)
            {
                _knownBeacons.Clear();
                
                foreach (var config in beaconConfigurations)
                {
                    var key = config.MacAddress.ToUpperInvariant();
                    _knownBeacons[key] = config;
                }
                
                _logger.LogInformation("📋 Updated known beacons list: {Count} beacons configured", beaconConfigurations.Count);
            }
        }
        
        public bool IsInRange()
        {
            lock (_beaconLock)
            {
                return _detectedBeacons.Values.Any(b => b.IsActive && b.Rssi >= b.RssiThreshold);
            }
        }
        
        public bool IsInRangeOfBeacon(string macAddress)
        {
            lock (_beaconLock)
            {
                var key = macAddress.ToUpperInvariant();
                if (_detectedBeacons.TryGetValue(key, out var beacon))
                {
                    return beacon.IsActive && beacon.Rssi >= beacon.RssiThreshold;
                }
                return false;
            }
        }
        
        public string? GetCurrentRoom()
        {
            return _currentRoom;
        }
        
        /// <summary>
        /// Get all tracked beacons with their current status
        /// </summary>
        public Dictionary<string, BeaconInfo> GetTrackedBeacons()
        {
            lock (_beaconLock)
            {
                return new Dictionary<string, BeaconInfo>(_detectedBeacons);
            }
        }
        
        /// <summary>
        /// Update beacon configurations from server
        /// </summary>
        public async Task UpdateBeaconConfigurations(Dictionary<string, (string Name, string RoomName, int RssiThreshold, bool IsNavigationTarget, int Priority)> beaconConfigs)
        {
            await Task.Run(() =>
            {
                lock (_beaconLock)
                {
                    _knownBeacons.Clear();
                    
                    foreach (var kvp in beaconConfigs)
                    {
                        var macAddress = kvp.Key.ToUpperInvariant();
                        var config = kvp.Value;
                        
                        _knownBeacons[macAddress] = new BeaconConfigurationDto
                        {
                            MacAddress = macAddress,
                            Name = config.Name,
                            RoomName = config.RoomName,
                            RssiThreshold = config.RssiThreshold,
                            IsNavigationTarget = config.IsNavigationTarget,
                            Priority = config.Priority,
                            IsActive = true
                        };
                    }
                    
                    _logger.LogDebug("📋 Updated beacon configurations from server: {Count} beacons", beaconConfigs.Count);
                }
            });
        }
        
        #endregion
        
        /// <summary>
        /// Dispose of resources when service is destroyed
        /// </summary>
        public void Dispose()
        {
            _scanTimer?.Dispose();
        }
    }
}