using System.Diagnostics;
using System.IO;

namespace LineFollowerRobot.Services;

/// <summary>
/// Service that ensures the LineFollowerRobot runs automatically on system startup
/// Creates and manages crontab configuration for auto-startup
/// </summary>
public class CrontabStartupService : BackgroundService
{
    private readonly ILogger<CrontabStartupService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _serviceName = "linefollower-robot";
    private readonly string _currentDirectory;
    private readonly string _dllPath; 

    public CrontabStartupService(ILogger<CrontabStartupService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _currentDirectory = Directory.GetCurrentDirectory();
        _dllPath = Path.Combine(_currentDirectory, "LineFollowerRobot.dll");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CrontabStartupService started - checking crontab configuration");

        try
        {
            // Check if running in Linux environment (crontab available)
            if (!IsLinuxEnvironment())
            {
                _logger.LogInformation("Not running on Linux - crontab startup service disabled");
                return;
            }

            _logger.LogInformation("Creating/updating crontab entry for auto-startup");
            await CreateCrontabEntry();
            
            _logger.LogInformation("✅ CrontabStartupService completed - robot will start automatically on boot");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure crontab startup service");
        }
    }

    private bool IsLinuxEnvironment()
    {
        return Environment.OSVersion.Platform == PlatformID.Unix;
    }

    private async Task CreateCrontabEntry()
    {
        try
        {
            // Get current crontab
            var getCurrentCrontab = await RunCommand("crontab", "-l");
            var currentCrontab = getCurrentCrontab.ExitCode == 0 ? getCurrentCrontab.Output : "";

            // Create startup script content
            var startupScript = $@"#!/bin/bash
# {_serviceName} startup script 
export PATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:/snap/bin:/opt/bin:/usr/local/bin:/home/linuxbrew/.linuxbrew/bin
export DOTNET_ENVIRONMENT=Production
export ASPNETCORE_ENVIRONMENT=Production

# Change to working directory
cd {_currentDirectory}

# Kill any existing session
tmux kill-session -t {_serviceName} 2>/dev/null || true

# Start new tmux session with dotnet application
tmux new-session -d -s {_serviceName} 'dotnet {_dllPath}' 2>&1

# Log startup attempt 
echo ""$(date): Started {_serviceName} tmux session"" >> /var/log/{_serviceName}.log
";

            var scriptPath = Path.Combine(_currentDirectory, $"{_serviceName}-startup.sh");
            // Ensure Unix line endings
            var unixScript = startupScript.Replace("\r\n", "\n").Replace("\r", "\n");
            await File.WriteAllTextAsync(scriptPath, unixScript);
             
            // Convert to Unix line endings
            await RunCommand("dos2unix", scriptPath);

            // Make script executable
            await RunCommand("chmod", $"+x {scriptPath}");

            // Create new crontab entry
            var crontabEntry = $"@reboot {scriptPath} # {_serviceName} auto-start";
            
            // Remove existing entry if it exists
            var lines = currentCrontab.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !line.Contains(_serviceName))
                .ToList();

            // Add new entry
            lines.Add(crontabEntry);

            var newCrontab = string.Join("\n", lines) + "\n";

            // Write new crontab
            var tempFile = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFile, newCrontab);

            try
            {
                var result = await RunCommand("crontab", tempFile);
                if (result.ExitCode != 0)
                {
                    throw new Exception($"Failed to install crontab: {result.Error}");
                }

                _logger.LogInformation("Created crontab entry and startup script: {ScriptPath}", scriptPath);
                _logger.LogInformation("Crontab entry: {Entry}", crontabEntry);
            }
            finally
            {
                File.Delete(tempFile); 
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create crontab entry");
            throw;
        }
    }


    private async Task<(int ExitCode, string Output, string Error)> RunCommand(string command, string arguments)
    {
        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();
            
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            
            await process.WaitForExitAsync();
            
            var output = await outputTask;
            var error = await errorTask;

            _logger.LogDebug("Command: {Command} {Arguments} - Exit code: {ExitCode}", 
                command, arguments, process.ExitCode);

            return (process.ExitCode, output, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute command: {Command} {Arguments}", command, arguments);
            return (-1, string.Empty, ex.Message);
        }
    }
}