using System.Device.Gpio;

namespace LineFollowerRobot.Services;

public class UltrasonicSensorService
{
    public const double STOP_DISTANCE = 0.25; // meters

    private readonly ILogger<UltrasonicSensorService> _logger;
    private readonly IConfiguration _config;
    private GpioController? _gpio;
    private Timer? _timer;
    private int _trigPin;
    private int _echoPin;
    private double _lastDistance;

    public event EventHandler<double>? DistanceChanged;

    public UltrasonicSensorService(ILogger<UltrasonicSensorService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
    }

    public Task InitializeAsync()
    {
        _trigPin = _config.GetValue<int>("LineFollower:GPIO:Ultrasonic:TrigPin");
        _echoPin = _config.GetValue<int>("LineFollower:GPIO:Ultrasonic:EchoPin");

        _gpio = new GpioController();
        _gpio.OpenPin(_trigPin, PinMode.Output);
        _gpio.OpenPin(_echoPin, PinMode.Input);
        _gpio.Write(_trigPin, PinValue.Low);

        _logger.LogInformation("Ultrasonic sensor initialized (Trig: GPIO{Trig}, Echo: GPIO{Echo})", _trigPin, _echoPin);
        return Task.CompletedTask;
    }

    public void Start()
    {
        _timer = new Timer(_ => MeasureDistance(), null, TimeSpan.Zero, TimeSpan.FromMilliseconds(500));
    }

    public void Stop()
    {
        _timer?.Dispose();
    }

    public double GetDistance() => _lastDistance;

    private void MeasureDistance()
    { 
        try
        {
            if (_gpio == null) return;

            _gpio.Write(_trigPin, PinValue.High);
            Thread.Sleep(TimeSpan.FromMicroseconds(10));
            _gpio.Write(_trigPin, PinValue.Low);

            var timeout = DateTime.UtcNow.AddMilliseconds(100);
            while (_gpio.Read(_echoPin) == PinValue.Low && DateTime.UtcNow < timeout) { }

            var start = DateTime.UtcNow;
            timeout = start.AddMilliseconds(100);
            while (_gpio.Read(_echoPin) == PinValue.High && DateTime.UtcNow < timeout) { }

            var duration = (DateTime.UtcNow - start).TotalSeconds;
            var distance = duration * 34300 / 2 / 100; // meters

            // Ignore invalid readings (sensor not connected or error)
            if (distance < 0.02 || distance > 2.0) return;

            _lastDistance = distance;
            DistanceChanged?.Invoke(this, distance);
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Ultrasonic measure error: {Error}", ex.Message);
        }
    }
}
