using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LineFollowerRobot.Services;

public class EnsureStartupService : BackgroundService
{
    private readonly ILogger<EnsureStartupService> _logger;
    private readonly string _rcLocalPath = "/etc/rc.local";
    private readonly string _workingDirectory = "/home/user/linefollower";
    private readonly string _startupCommand = "dotnet LineFollowerRobot.dll";

    public EnsureStartupService(ILogger<EnsureStartupService> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔧 EnsureStartupService: Checking startup configuration...");
        
        try
        {
            await EnsureStartupConfiguration();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to configure startup service");
        }
        
        // This service only runs once at startup, then exits
        _logger.LogInformation("🔧 EnsureStartupService: Configuration check complete");
    }

    private async Task EnsureStartupConfiguration()
    {
        if (!File.Exists(_rcLocalPath))
        {
            _logger.LogInformation("📝 Creating /etc/rc.local file...");
            await CreateRcLocalFile();
        }
        else
        {
            _logger.LogInformation("📄 /etc/rc.local exists, checking configuration...");
        }

        var rcLocalContent = await File.ReadAllTextAsync(_rcLocalPath);
        var tmuxCommand = $"su - user -c 'cd {_workingDirectory} && tmux new-session -d -s linefollower \"{_startupCommand}\"'";

        if (rcLocalContent.Contains("tmux new-session -d -s linefollower"))
        {
            _logger.LogInformation("✅ Startup configuration already present in rc.local");
            return;
        }

        _logger.LogInformation("🔧 Adding LineFollowerRobot startup configuration to rc.local...");
        await AddStartupToRcLocal(rcLocalContent, tmuxCommand);
        
        // Make rc.local executable
        await MakeRcLocalExecutable();
        
        _logger.LogInformation("✅ LineFollowerRobot startup configuration added successfully");
        _logger.LogInformation("📍 Command: {Command}", tmuxCommand);
        _logger.LogInformation("📂 Working Directory: {WorkingDirectory}", _workingDirectory);
    }

    private async Task CreateRcLocalFile()
    {
        var rcLocalTemplate = @"#!/bin/bash
# /etc/rc.local
# This script is executed at the end of each multiuser runlevel.
# Make sure that the script will ""exit 0"" on success or any other
# value on error.

# LineFollowerRobot auto-start
su - user -c 'cd /home/user/linefollower && tmux new-session -d -s linefollower ""dotnet LineFollowerRobot.dll""'

exit 0
";
        
        await File.WriteAllTextAsync(_rcLocalPath, rcLocalTemplate);
        _logger.LogInformation("📝 Created new /etc/rc.local with LineFollowerRobot configuration");
    }

    private async Task AddStartupToRcLocal(string existingContent, string tmuxCommand)
    {
        var lines = existingContent.Split('\n').ToList();
        
        // Find the position to insert the command (before the last "exit 0" if it exists)
        var exitIndex = -1;
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            if (lines[i].Trim() == "exit 0")
            {
                exitIndex = i;
                break;
            }
        }

        // Add a comment and the tmux command
        var insertIndex = exitIndex >= 0 ? exitIndex : lines.Count;
        
        if (insertIndex > 0 && !string.IsNullOrWhiteSpace(lines[insertIndex - 1]))
        {
            lines.Insert(insertIndex, "");
        }
        
        lines.Insert(insertIndex, "# LineFollowerRobot auto-start");
        lines.Insert(insertIndex + 1, tmuxCommand);
        
        if (exitIndex < 0)
        {
            lines.Add("");
            lines.Add("exit 0");
        }

        var updatedContent = string.Join("\n", lines);
        await File.WriteAllTextAsync(_rcLocalPath, updatedContent);
    }

    private async Task MakeRcLocalExecutable()
    {
        try
        {
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = "+x /etc/rc.local",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            
            process.Start();
            await process.WaitForExitAsync();
            
            if (process.ExitCode == 0)
            {
                _logger.LogInformation("✅ Made /etc/rc.local executable");
            }
            else
            {
                _logger.LogWarning("⚠️ Failed to make /etc/rc.local executable (exit code: {ExitCode})", process.ExitCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error making /etc/rc.local executable");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔧 EnsureStartupService stopping");
        await base.StopAsync(cancellationToken);
    }
}