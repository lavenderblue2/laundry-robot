using System.Device.Gpio;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RobotProject.Shared.Models;

namespace LineFollowerRobot.Services
{
    /// <summary>
    /// HX711 Weight Sensor Service - Singleton service for reading weight data
    /// Uses calibration values from your testing: offset=-37000, referenceUnit=205.5
    /// </summary>
    public class Hx711Service : BackgroundService, IDisposable
    {
        private readonly ILogger<Hx711Service> _logger;
        private GpioController? _gpio;

        // GPIO Pin mappings - matching your Program.cs exactly
        private readonly int _dataPin = 18;    // Red wire (DT/DOUT)
        private readonly int _clockPin = 23;   // Orange wire (SCK)

        // Calibration values from your testing - exact match
        private readonly long _offset = -37000;  // No load average from your calibration
        private readonly double _referenceUnit = 205.5; // (168500 - (-37000)) / 1000.0

        // Tare compensation for physical secure storage container
        private readonly double _containerWeightKg = 1.01; // Weight of secure storage container in kg
        private readonly double _containerWeightGrams = 1010.0; // Weight of secure storage container in grams

        // Reading configuration
        private readonly int _readingIntervalMs = 100; // 100ms as requested
        private readonly int _stabilityWindow = 5; // Number of readings to check for stability
        private readonly double _stabilityThreshold = 2.0; // Grams threshold for stability

        // Current weight properties - thread-safe
        private double _lastWeightReadInGrams = 0.0;
        private double _lastWeightReadInKg = 0.0;
        private readonly object _weightLock = new();

        // Recent readings for stability calculation
        private readonly Queue<double> _recentReadings = new();

        // Event for weight reading updates
        public event EventHandler<WeightReadingEventArgs>? WeightChanged;

        /// <summary>
        /// Last weight reading in grams (most accurate)
        /// </summary>
        public double LastWeightReadInGrams
        {
            get
            {
                lock (_weightLock)
                {
                    return _lastWeightReadInGrams;
                }
            }
        }

        /// <summary>
        /// Last weight reading in kilograms
        /// </summary>
        public double LastWeightReadInKg
        {
            get
            {
                lock (_weightLock)
                {
                    return _lastWeightReadInKg;
                }
            }
        }

        public Hx711Service(ILogger<Hx711Service> logger)
        {
            _logger = logger;
            _logger.LogInformation("HX711 Weight Sensor Service initialized");
            _logger.LogInformation("   Data Pin: {DataPin} (Red wire - DT/DOUT)", _dataPin);
            _logger.LogInformation("   Clock Pin: {ClockPin} (Orange wire - SCK)", _clockPin);
            _logger.LogInformation("   Offset: {Offset} (no load)", _offset);
            _logger.LogInformation("   Reference Unit: {ReferenceUnit:F2} units per gram", _referenceUnit);
            _logger.LogInformation("   Container Tare: {ContainerWeight:F2}kg ({ContainerWeightGrams:F0}g)", _containerWeightKg, _containerWeightGrams);
            _logger.LogInformation("   Reading Interval: {IntervalMs}ms", _readingIntervalMs);
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting HX711 Weight Sensor Service...");

            try
            {
                // Initialize GPIO
                _gpio = new GpioController();
                _gpio.OpenPin(_dataPin, PinMode.Input);
                _gpio.OpenPin(_clockPin, PinMode.Output);
                _gpio.Write(_clockPin, PinValue.Low);

                _logger.LogInformation("HX711 GPIO pins initialized successfully");
                
                // Test initial reading
                var testReading = await ReadRawValue();
                _logger.LogInformation("Initial test reading: {RawValue}", testReading);

                await base.StartAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize HX711 service");
                throw;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("HX711 weight reading service started - taking readings every {IntervalMs}ms", _readingIntervalMs);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var reading = await TakeWeightReading();
                    UpdateCurrentWeight(reading);
                    
                    // Emit weight changed event
                    WeightChanged?.Invoke(this, new WeightReadingEventArgs(reading));

                    // Log periodically (every 5 seconds)
                    if (DateTime.Now.Second % 5 == 0 && DateTime.Now.Millisecond < _readingIntervalMs)
                    {
                        _logger.LogInformation("Weight: {WeightGrams:F1}g ({WeightKg:F3}kg) | Raw: {RawValue} | Stable: {IsStable}", 
                            reading.WeightGrams, reading.WeightKg, reading.RawValue, reading.IsStable);
                    }

                    await Task.Delay(_readingIntervalMs, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading weight sensor");
                    await Task.Delay(1000, stoppingToken); // Wait longer on error
                }
            }

            _logger.LogInformation("HX711 weight reading service stopped");
        }

        /// <summary>
        /// Take a complete weight reading with stability calculation
        /// </summary>
        private async Task<WeightReading> TakeWeightReading()
        {
            var rawValue = await ReadRawValue();
            var weightGrams = (rawValue - _offset) / _referenceUnit;
            var weightKg = weightGrams / 1000.0;

            // Subtract container weight (tare compensation for secure storage)
            weightGrams -= _containerWeightGrams;
            weightKg -= _containerWeightKg;

            // Ensure weight doesn't go negative
            if (weightGrams < 0) weightGrams = 0;
            if (weightKg < 0) weightKg = 0;

            // Calculate stability
            bool isStable = CalculateStability(weightGrams);

            return new WeightReading
            {
                RawValue = rawValue,
                WeightGrams = weightGrams,
                WeightKg = weightKg,
                Timestamp = DateTime.UtcNow,
                IsStable = isStable
            };
        }

        /// <summary>
        /// Read raw value from HX711 - exact copy of your Program.cs logic
        /// </summary>
        private async Task<long> ReadRawValue()
        {
            if (_gpio == null)
                throw new InvalidOperationException("GPIO not initialized");

            // Wait for chip to be ready (data pin goes low)
            int timeout = 1000;
            while (_gpio.Read(_dataPin) == PinValue.High && timeout > 0)
            {
                await Task.Delay(1);
                timeout--;
            }

            if (timeout == 0)
            {
                throw new TimeoutException("HX711 not ready - check connections");
            }

            long value = 0;

            // Read 24 bits of data
            for (int i = 0; i < 24; i++)
            {
                _gpio.Write(_clockPin, PinValue.High);
                await Task.Delay(TimeSpan.FromMicroseconds(1));

                value = value << 1;
                if (_gpio.Read(_dataPin) == PinValue.High)
                {
                    value++;
                }

                _gpio.Write(_clockPin, PinValue.Low);
                await Task.Delay(TimeSpan.FromMicroseconds(1));
            }

            // 25th pulse for gain setting
            _gpio.Write(_clockPin, PinValue.High);
            await Task.Delay(TimeSpan.FromMicroseconds(1));
            _gpio.Write(_clockPin, PinValue.Low);
            await Task.Delay(TimeSpan.FromMicroseconds(1));

            // Convert to signed 24-bit value
            if ((value & 0x800000) != 0)
            {
                value -= 0x1000000;
            }

            return value;
        }

        /// <summary>
        /// Update current weight values thread-safely
        /// </summary>
        private void UpdateCurrentWeight(WeightReading reading)
        {
            lock (_weightLock)
            {
                _lastWeightReadInGrams = reading.WeightGrams;
                _lastWeightReadInKg = reading.WeightKg;
            }
        }

        /// <summary>
        /// Calculate if current reading is stable based on recent readings
        /// </summary>
        private bool CalculateStability(double currentWeightGrams)
        {
            lock (_recentReadings)
            {
                _recentReadings.Enqueue(currentWeightGrams);
                
                // Keep only recent readings within window
                while (_recentReadings.Count > _stabilityWindow)
                {
                    _recentReadings.Dequeue();
                }

                // Need at least the window size to determine stability
                if (_recentReadings.Count < _stabilityWindow)
                {
                    return false;
                }

                // Check if all readings are within threshold
                var min = _recentReadings.Min();
                var max = _recentReadings.Max();
                return (max - min) <= _stabilityThreshold;
            }
        }

        /// <summary>
        /// Get current weight reading
        /// </summary>
        public WeightReading GetCurrentReading()
        {
            lock (_weightLock)
            {
                return new WeightReading
                {
                    WeightGrams = _lastWeightReadInGrams,
                    WeightKg = _lastWeightReadInKg,
                    Timestamp = DateTime.UtcNow,
                    IsStable = true // Assume stable for current reading
                };
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping HX711 Weight Sensor Service...");
            await base.StopAsync(cancellationToken);
        }

        public override void Dispose()
        {
            try
            {
                _gpio?.Dispose();
                _logger.LogInformation("HX711 Weight Sensor Service disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing HX711 service");
            }
            finally
            {
                base.Dispose();
            }
        }
    }
}