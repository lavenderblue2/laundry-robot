using AdministratorWeb.Data;
using AdministratorWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace AdministratorWeb.Services
{
    /// <summary>
    /// Background service that handles request timeouts for ArrivedAtRoom status
    /// </summary>
    public class RequestTimeoutService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RequestTimeoutService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(10); // Check every 10 seconds for fast timeout response
        private readonly TimeSpan _startupDelay = TimeSpan.FromSeconds(30); // Wait 30 seconds on startup

        public RequestTimeoutService(IServiceProvider serviceProvider, ILogger<RequestTimeoutService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("RequestTimeoutService started");

            // Wait on startup to avoid canceling requests immediately after server restart
            _logger.LogInformation("⏰ Waiting {Seconds} seconds before first timeout check...", _startupDelay.TotalSeconds);
            await Task.Delay(_startupDelay, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckForTimedOutRequests();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking for timed out requests");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }

        private async Task CheckForTimedOutRequests()
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Get timeout duration from settings (default to 5 minutes if not set)
            var settings = await context.LaundrySettings.FirstOrDefaultAsync();
            var timeoutMinutes = settings?.RoomArrivalTimeoutMinutes ?? 5;
            var arrivalTimeout = TimeSpan.FromMinutes(timeoutMinutes);

            var cutoffTime = DateTime.UtcNow.Subtract(arrivalTimeout);

            // Check for requests that timed out at customer room (initial pickup)
            var timedOutPickupRequests = await context.LaundryRequests
                .Where(r => r.Status == RequestStatus.ArrivedAtRoom &&
                           r.ArrivedAtRoomAt.HasValue &&
                           r.ArrivedAtRoomAt.Value < cutoffTime)
                .ToListAsync();

            // Check for requests that timed out at customer room (delivery after washing)
            var timedOutDeliveryRequests = await context.LaundryRequests
                .Where(r => r.Status == RequestStatus.FinishedWashingArrivedAtRoom &&
                           r.ArrivedAtRoomAt.HasValue &&
                           r.ArrivedAtRoomAt.Value < cutoffTime)
                .ToListAsync();

            var timedOutRequests = timedOutPickupRequests.Concat(timedOutDeliveryRequests).ToList();

            foreach (var request in timedOutRequests)
            {
                if (request.Status == RequestStatus.ArrivedAtRoom)
                {
                    // Initial pickup timeout - cancel request but keep robot assigned so it returns to base
                    _logger.LogWarning("Request {RequestId} timed out - customer did not load laundry within {TimeoutMinutes} minutes - CANCELLING REQUEST", request.Id, timeoutMinutes);
                    request.Status = RequestStatus.Cancelled;
                    request.ProcessedAt = DateTime.UtcNow;
                    // Keep AssignedRobotName so robot will return to base (will be cleared when robot arrives at base)
                    _logger.LogInformation("Request {RequestId} marked as Cancelled - robot {RobotName} will return to base",
                        request.Id, request.AssignedRobotName ?? "Unknown");
                }
                else if (request.Status == RequestStatus.FinishedWashingArrivedAtRoom)
                {
                    // Delivery timeout - cancel delivery attempt, keep robot assigned to return to base
                    _logger.LogWarning("Request {RequestId} delivery timed out - customer did not pick up laundry within {TimeoutMinutes} minutes - CANCELLING DELIVERY", request.Id, timeoutMinutes);
                    request.Status = RequestStatus.FinishedWashingGoingToBase;
                    request.ProcessedAt = DateTime.UtcNow;
                    // Keep AssignedRobotName so robot will return to base with laundry
                    _logger.LogInformation("Request {RequestId} marked as FinishedWashingGoingToBase - robot {RobotName} will return to base with laundry",
                        request.Id, request.AssignedRobotName ?? "Unknown");
                }

                await context.SaveChangesAsync();
            }
        }
    }
}