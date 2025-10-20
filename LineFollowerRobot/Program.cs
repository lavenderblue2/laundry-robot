using LineFollowerRobot.Services;
using LineFollowerRobot.Services.CameraMiddleware;
using LineFollowerRobot.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace LineFollowerRobot;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("🤖 Line Follower Robot Starting...");
        Console.WriteLine("=====================================");
        
        try
        {
            var builder = WebApplication.CreateBuilder(args);
            
            // Configure services
            builder.Services.AddControllers();
            
            // Camera services
            builder.Services.AddSingleton<CameraStreamService>();
            builder.Services.AddHostedService<CameraStreamService>(provider => provider.GetRequiredService<CameraStreamService>());
            builder.Services.AddSingleton<LineDetectionCameraService>();
            
            
            // Command polling service
            builder.Services.AddSingleton<CommandPollingService>();
            builder.Services.AddHostedService<CommandPollingService>(provider => provider.GetRequiredService<CommandPollingService>());
            
            // Bluetooth beacon services
            builder.Services.AddSingleton<BluetoothBeaconService>();
            builder.Services.AddHostedService<BluetoothBeaconHostedService>();
            
            // IMPORTANT: DO NOT add HttpClient here with AddHttpClient()
            // HttpClient must be configured per service with HttpClientHandler and FollowRedirects
            // to avoid JSON parsing errors in CommandPollingService and other services
            
            // Other services
            builder.Services.AddSingleton<UltrasonicSensorService>();
            builder.Services.AddSingleton<LineFollowerMotorService>();
            builder.Services.AddSingleton<LineFollowerService>();
            builder.Services.AddHostedService<LineFollowerService>(provider => provider.GetRequiredService<LineFollowerService>());
            builder.Services.AddSingleton<RobotRegistrationService>();
            builder.Services.AddHostedService<RobotRegistrationService>(provider => provider.GetRequiredService<RobotRegistrationService>());
            builder.Services.AddSingleton<RobotServerCommunicationService>();
            builder.Services.AddHostedService<RobotServerCommunicationService>(provider => provider.GetRequiredService<RobotServerCommunicationService>());
            builder.Services.AddSingleton<RobotImageUploadService>();
            builder.Services.AddHostedService<RobotImageUploadService>(provider => provider.GetRequiredService<RobotImageUploadService>());
            builder.Services.AddHostedService<EnsureStartupService>();
            builder.Services.AddHostedService<CrontabStartupService>();
            
            // Register HX711 service as singleton
            builder.Services.AddSingleton<Hx711Service>();
            builder.Services.AddHostedService<Hx711Service>(provider => provider.GetRequiredService<Hx711Service>());
            
            // Register HeadlightService for LED array control
            builder.Services.AddSingleton<HeadlightService>();
            builder.Services.AddHostedService<HeadlightService>(provider => provider.GetRequiredService<HeadlightService>());
            
            // Configure logging
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole();
            builder.Logging.SetMinimumLevel(LogLevel.Information);
            builder.Logging.AddFilter("System.Net.Http", LogLevel.None);
            
            // Configure to listen on all interfaces on port 8080
            builder.WebHost.UseUrls("http://*:8080");
            
            var app = builder.Build();
            
            // Configure middleware
            app.UseStaticFiles();
            app.UseRouting();
            app.MapControllers();
            
            // Redirect root to camera page
            app.MapGet("/", () => Results.Redirect("/camera.html"));
            
            // Handle Ctrl+C gracefully
            Console.CancelKeyPress += async (sender, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("\n🛑 Shutdown requested...");
                await app.StopAsync();
            };
            
            // Handle ESC key to close program
            var cancellationTokenSource = new CancellationTokenSource();
            var keyboardTask = Task.Run(async () =>
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    if (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true);
                        if (key.Key == ConsoleKey.Escape)
                        {
                            Console.WriteLine("\n🛑 ESC pressed - Shutdown requested...");
                            await app.StopAsync();
                            break;
                        }
                    }
                    await Task.Delay(100, cancellationTokenSource.Token);
                }
            });
            
            Console.WriteLine("✅ Line Follower Robot initialized");
            Console.WriteLine("📹 Camera-based line detection enabled");
            Console.WriteLine("📡 Ultrasonic obstacle detection enabled");
            Console.WriteLine("🌐 Web interface available at http://0.0.0.0:8080/camera.html");
            Console.WriteLine("⌨️  Press Ctrl+C or ESC to stop the robot");
            Console.WriteLine("=====================================");
            
            await app.RunAsync();
            cancellationTokenSource.Cancel();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Fatal error: {ex.Message}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            Environment.Exit(1);
        }
        finally
        {
            Console.WriteLine("🏁 Line Follower Robot shutdown complete");
        }
    }
}