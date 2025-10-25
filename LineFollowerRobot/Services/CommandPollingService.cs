using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace LineFollowerRobot.Services;

public class CommandPollingService : BackgroundService
{
    private readonly ILogger<CommandPollingService> _logger;
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly string _robotName;
    private readonly string _apiServer;

    // Command flags
    public bool IsFollowingLine { get; private set; } = false;

    public CommandPollingService(
        ILogger<CommandPollingService> logger,
        IConfiguration config)
    {
        _logger = logger;
        _config = config;
        
        var handler = new HttpClientHandler()
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 3
        };
        _httpClient = new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(5); // Prevent hanging on network issues

        _robotName = _config.GetValue<string>("Robot:Name") ?? "Unknown";
        _apiServer = _config.GetValue<string>("Robot:ApiServer") ?? "";

        if (string.IsNullOrEmpty(_apiServer))
        {
            _logger.LogWarning("⚠️ Robot:ApiServer not configured, command polling disabled");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrEmpty(_apiServer))
        {
            _logger.LogWarning("⚠️ Command polling service disabled - no API server configured");
            return;
        }

        _logger.LogInformation("🔄 Command polling service started (checking every 0.1s for faster response)");


        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await PollForCommands();
                await Task.Delay(100, stoppingToken); // Poll every 0.1 seconds (100ms) for faster response
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("🔄 Command polling service cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in command polling service");
        }
    }

    private async Task PollForCommands()
    {
        try
        {
            var url = $"{_apiServer.TrimEnd('/')}/api/robot/{Uri.EscapeDataString(_robotName)}/status";
            //_logger.LogInformation($"sending {url}");
            
            var response = await _httpClient.GetAsync(url);
             
            var json = await response.Content.ReadAsStringAsync();
         //   _logger.LogInformation($"received {json}, status: {(int)response.StatusCode}, to {response.Headers.Location}");
            var robotStatus = JObject.Parse(json);
            if (robotStatus["isFollowingLine"] != null)
            {
                var newFollowingLineStatus = robotStatus["isFollowingLine"].Value<bool>();

                if (newFollowingLineStatus != IsFollowingLine)
                {
                    IsFollowingLine = newFollowingLineStatus;
                    _logger.LogInformation("🤖 Command received: IsFollowingLine = {Status}", IsFollowingLine);
                }
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError("Network error polling commands: {Error}", ex.Message);
        }
        catch (TaskCanceledException)
        {
            // Timeout, ignore
        }
        catch (Exception ex)
        {
            _logger.LogError("Error polling commands: {Error}", ex.Message);
        }
    }
}