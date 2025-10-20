using RobotProject.Shared.DTOs;

namespace LineFollowerRobot.Services
{
    /// <summary>
    /// Hosted service that manages the lifecycle of the Bluetooth beacon scanning service
    /// Automatically starts beacon scanning when the robot starts up and stops it during shutdown
    /// </summary>
    public class BluetoothBeaconHostedService : BackgroundService
    {
        private readonly ILogger<BluetoothBeaconHostedService> _logger;
        private readonly BluetoothBeaconService _beaconService;
        private readonly IServiceProvider _serviceProvider;
        
        public BluetoothBeaconHostedService(
            ILogger<BluetoothBeaconHostedService> logger,
            BluetoothBeaconService beaconService,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _beaconService = beaconService;
            _serviceProvider = serviceProvider;
        }
        
        /// <summary>
        /// Initialize and start the Bluetooth beacon service when the robot starts
        /// </summary>
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🔵 Starting Bluetooth beacon hosted service...");
            
            try
            {
                // Initialize the beacon service
                await _beaconService.InitializeAsync();
                
                // Set up event handlers for room tracking
                _beaconService.RoomChanged += OnRoomChanged;
                _beaconService.BeaconDetected += OnBeaconDetected;
                
                // Start beacon scanning
                await _beaconService.StartScanningAsync();
                
                _logger.LogInformation("✅ Bluetooth beacon hosted service started successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to start Bluetooth beacon hosted service: {Error}", ex.Message);
                // Don't throw here - allow the robot to continue operating without beacon functionality
            }
            
            await base.StartAsync(cancellationToken);
        }
        
        /// <summary>
        /// Main execution loop - currently just maintains the service alive
        /// Future: Could implement periodic health checks or beacon management tasks
        /// </summary>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔵 Bluetooth beacon hosted service is running");
            
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    // Keep the service alive and allow beacon scanning to continue
                    // The actual beacon scanning happens in the BluetoothBeaconService timer
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    
                    // Periodic status logging
                    var detectedCount = _beaconService.GetDetectedBeacons().Count;
                    var currentRoom = _beaconService.GetCurrentRoom();
                    var primaryBeacon = _beaconService.GetPrimaryBeacon();
                    
                    if (detectedCount > 0)
                    {
                        _logger.LogDebug("📡 Beacon Status: {Count} detected, Room: {Room}, Primary: {Primary}", 
                            detectedCount, currentRoom ?? "None", primaryBeacon?.Name ?? "None");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                _logger.LogInformation("🔵 Bluetooth beacon hosted service execution cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error in Bluetooth beacon hosted service execution");
            }
        }
        
        /// <summary>
        /// Stop the beacon service when the robot shuts down
        /// </summary>
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🔵 Stopping Bluetooth beacon hosted service...");
            
            try
            {
                // Unsubscribe from events
                _beaconService.RoomChanged -= OnRoomChanged;
                _beaconService.BeaconDetected -= OnBeaconDetected;
                
                // Stop beacon scanning
                await _beaconService.StopScanningAsync();
                
                _logger.LogInformation("✅ Bluetooth beacon hosted service stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error stopping Bluetooth beacon hosted service");
            }
            
            await base.StopAsync(cancellationToken);
        }
        
        /// <summary>
        /// Handle room change events - log and potentially trigger navigation actions
        /// </summary>
        private void OnRoomChanged(object? sender, string? roomName)
        {
            if (roomName != null)
            {
                _logger.LogInformation("🏠 Robot entered room: {RoomName}", roomName);
            }
            else
            {
                _logger.LogInformation("🏠 Robot left all tracked rooms");
            }
            
            // TODO: Future enhancement - trigger room-based actions
            // - Update robot status in server
            // - Execute room-specific tasks
            // - Navigate to specific locations within the room
        }
        
        /// <summary>
        /// Handle individual beacon detection events
        /// </summary>
        private void OnBeaconDetected(object? sender, BeaconInfo beacon)
        {
            // Currently just used for debugging - could be enhanced for specific beacon actions
            _logger.LogDebug("📡 Beacon detected: {Name} in {Room} - RSSI: {Rssi}dBm", 
                beacon.Name, beacon.RoomName, beacon.Rssi);
            
            // TODO: Future enhancement - beacon-specific actions
            // - Navigate towards specific beacons
            // - Execute beacon-triggered tasks
            // - Update server with real-time beacon data
        }
        
        /// <summary>
        /// Dispose of resources when the hosted service is destroyed
        /// </summary>
        public override void Dispose()
        {
            try
            {
                // Ensure events are unsubscribed
                _beaconService.RoomChanged -= OnRoomChanged;
                _beaconService.BeaconDetected -= OnBeaconDetected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error disposing Bluetooth beacon hosted service");
            }
            
            base.Dispose();
        }
    }
}