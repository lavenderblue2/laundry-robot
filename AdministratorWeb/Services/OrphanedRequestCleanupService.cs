using AdministratorWeb.Data;
using AdministratorWeb.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AdministratorWeb.Services
{
    /// <summary>
    /// Background service that periodically cleans up orphaned laundry requests
    /// that are stuck in limbo due to force stops, crashes, or other failures
    /// </summary>
    public class OrphanedRequestCleanupService : BackgroundService
    {
        private readonly ILogger<OrphanedRequestCleanupService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5); // Check every 5 minutes
        private readonly TimeSpan _orphanThreshold = TimeSpan.FromMinutes(30); // Requests older than 30 minutes
        private readonly TimeSpan _startupDelay = TimeSpan.FromSeconds(30); // Wait 30 seconds on startup (reduced from 2 minutes)

        public OrphanedRequestCleanupService(
            ILogger<OrphanedRequestCleanupService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🧹 Orphaned Request Cleanup Service started (checking every {Interval} minutes)",
                _checkInterval.TotalMinutes);

            // Wait on startup to give robots time to reconnect after server restart
            _logger.LogInformation("⏰ Waiting {Seconds} seconds before first cleanup check to allow robot reconnection...", _startupDelay.TotalSeconds);
            await Task.Delay(_startupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupOrphanedRequestsAsync();
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("🧹 Orphaned Request Cleanup Service cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Orphaned Request Cleanup Service");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Wait before retrying
                }
            }
        }

        private async Task CleanupOrphanedRequestsAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var robotService = scope.ServiceProvider.GetRequiredService<IRobotManagementService>();

                var cutoffTime = DateTime.UtcNow.Subtract(_orphanThreshold);

                // Find requests that:
                // 1. Are assigned to a robot
                // 2. Are NOT in a terminal state (Completed, Cancelled, Declined)
                // 3. Haven't been updated in 30+ minutes
                // 4. The robot is either offline or doesn't have this request as active

                var potentialOrphans = await context.LaundryRequests
                    .Where(r => r.AssignedRobotName != null &&
                               r.Status != RequestStatus.Completed &&
                               r.Status != RequestStatus.Cancelled &&
                               r.Status != RequestStatus.Declined &&
                               r.RequestedAt < cutoffTime)
                    .ToListAsync();

                if (!potentialOrphans.Any())
                {
                    _logger.LogDebug("No orphaned requests found");
                    return;
                }

                _logger.LogInformation("Found {Count} potential orphaned requests older than {Minutes} minutes",
                    potentialOrphans.Count, _orphanThreshold.TotalMinutes);

                var allRobots = await robotService.GetAllRobotsAsync();
                int cleanedCount = 0;

                foreach (var request in potentialOrphans)
                {
                    var assignedRobot = allRobots.FirstOrDefault(r =>
                        string.Equals(r.Name, request.AssignedRobotName, StringComparison.OrdinalIgnoreCase));

                    bool shouldCleanup = false;
                    string cleanupReason = "";

                    if (assignedRobot == null)
                    {
                        // Robot no longer exists in the system
                        shouldCleanup = true;
                        cleanupReason = $"Robot '{request.AssignedRobotName}' no longer exists in system";
                    }
                    else if (assignedRobot.IsOffline)
                    {
                        // Robot is offline
                        shouldCleanup = true;
                        cleanupReason = $"Robot '{request.AssignedRobotName}' is offline (last seen: {assignedRobot.LastPing})";
                    }
                    else if (assignedRobot.Status == RobotStatus.Available && assignedRobot.CurrentTask == null)
                    {
                        // Robot is available and idle, but request is still active
                        // HOWEVER: Don't cleanup if the request is in an active navigation state
                        // (robot might have just restarted and hasn't updated its task yet)
                        var activeNavigationStates = new[]
                        {
                            RequestStatus.RobotEnRoute,
                            RequestStatus.ArrivedAtRoom,
                            RequestStatus.LaundryLoaded,
                            RequestStatus.FinishedWashingGoingToRoom,
                            RequestStatus.FinishedWashingGoingToBase,
                            RequestStatus.FinishedWashingArrivedAtRoom
                        };

                        if (!activeNavigationStates.Contains(request.Status))
                        {
                            shouldCleanup = true;
                            cleanupReason = $"Robot '{request.AssignedRobotName}' is idle but request still active in non-navigation state ({request.Status})";
                        }
                    }

                    if (shouldCleanup)
                    {
                        request.Status = RequestStatus.Cancelled;
                        request.ProcessedAt = DateTime.UtcNow;
                        request.DeclineReason = $"[Auto-cleanup] {cleanupReason}. " +
                                               $"Request was orphaned since {request.RequestedAt:yyyy-MM-dd HH:mm:ss}";

                        _logger.LogWarning(
                            "Cleaned up orphaned request {RequestId} for customer {CustomerName}. Reason: {Reason}",
                            request.Id, request.CustomerName, cleanupReason);

                        cleanedCount++;
                    }
                }

                if (cleanedCount > 0)
                {
                    await context.SaveChangesAsync();
                    _logger.LogInformation("✅ Cleaned up {Count} orphaned requests", cleanedCount);
                }
                else
                {
                    _logger.LogDebug("No orphaned requests needed cleanup");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during orphaned request cleanup");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🧹 Orphaned Request Cleanup Service stopping");
            await base.StopAsync(cancellationToken);
        }
    }
}
