using AdministratorWeb.Models;
using Microsoft.Extensions.Logging;

namespace AdministratorWeb.Services
{
    public interface INotificationService
    {
        Task NotifyCustomerRequestCancelledAsync(LaundryRequest request, string reason);
        Task NotifyCustomerRequestStatusChangeAsync(LaundryRequest request, string statusMessage);
    }

    public class NotificationService : INotificationService
    {
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(ILogger<NotificationService> logger)
        {
            _logger = logger;
        }

        public async Task NotifyCustomerRequestCancelledAsync(LaundryRequest request, string reason)
        {
            try
            {
                // TODO: Implement actual notification mechanism (SMS, Email, Push Notification, etc.)
                // For now, just log the notification
                _logger.LogWarning(
                    "NOTIFICATION: Request {RequestId} cancelled for customer {CustomerName} ({CustomerPhone}). " +
                    "Reason: {Reason}",
                    request.Id,
                    request.CustomerName,
                    request.CustomerPhone,
                    reason
                );

                // Example implementations you can add:
                // 1. SMS via Twilio/SMS Gateway
                // await SendSmsAsync(request.CustomerPhone, $"Your laundry request #{request.Id} has been cancelled. Reason: {reason}");

                // 2. Email notification
                // await SendEmailAsync(request.CustomerEmail, "Request Cancelled", $"Your request has been cancelled: {reason}");

                // 3. Push notification (if using mobile app)
                // await SendPushNotificationAsync(request.CustomerId, "Request Cancelled", reason);

                // 4. In-app notification stored in database
                // await CreateInAppNotificationAsync(request.CustomerId, $"Request #{request.Id} cancelled", reason);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send cancellation notification for request {RequestId}", request.Id);
            }
        }

        public async Task NotifyCustomerRequestStatusChangeAsync(LaundryRequest request, string statusMessage)
        {
            try
            {
                // Check if this is a critical status that triggers mobile notifications
                bool isCriticalStatus = IsCriticalStatusForNotification(request.Status);

                var logLevel = isCriticalStatus ? LogLevel.Warning : LogLevel.Information;

                _logger.Log(
                    logLevel,
                    "NOTIFICATION: Request {RequestId} status update for customer {CustomerName} ({CustomerPhone}). " +
                    "Status: {Status} ({StatusEnum}), Message: {Message}, Critical: {IsCritical}",
                    request.Id,
                    request.CustomerName,
                    request.CustomerPhone,
                    request.Status.ToString(),
                    (int)request.Status,
                    statusMessage,
                    isCriticalStatus
                );

                // Log additional context for critical statuses
                if (isCriticalStatus)
                {
                    _logger.LogWarning(
                        "📱 CRITICAL STATUS - Mobile app will send local notification to customer. " +
                        "Request #{RequestId}, Status: {Status}, Weight: {Weight}kg, Cost: ₱{Cost}",
                        request.Id,
                        request.Status,
                        request.Weight ?? 0,
                        request.TotalCost ?? 0
                    );
                }

                // TODO: Implement server-side push notification mechanism (Firebase FCM, etc.)
                // This would send push notifications even when the app is closed
                // For now, the mobile app handles notifications locally via status change detection

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send status change notification for request {RequestId}", request.Id);
            }
        }

        /// <summary>
        /// Determines if a status change should trigger a notification to the customer
        /// These statuses match the notification triggers in the mobile app (UserApp)
        /// </summary>
        /// <param name="status">The laundry request status</param>
        /// <returns>True if this is a critical status that should notify the customer</returns>
        private bool IsCriticalStatusForNotification(RequestStatus status)
        {
            // These 13 critical statuses trigger local notifications in the mobile app
            // See: UserApp/laundry-app/services/notificationService.ts - getNotificationContent()
            return status switch
            {
                RequestStatus.Accepted => true,                    // Request approved
                RequestStatus.RobotEnRoute => true,                // Robot is coming
                RequestStatus.ArrivedAtRoom => true,               // Robot arrived - action required
                RequestStatus.LaundryLoaded => true,               // Pickup successful
                RequestStatus.WeighingComplete => true,            // Weight confirmed
                RequestStatus.PaymentPending => true,              // Payment required - action needed
                RequestStatus.Washing => true,                     // Washing started
                RequestStatus.FinishedWashing => true,             // Washing done - choose delivery/pickup
                RequestStatus.FinishedWashingGoingToRoom => true,  // Delivery in progress
                RequestStatus.FinishedWashingArrivedAtRoom => true,// Delivery arrived - action required
                RequestStatus.Completed => true,                   // Service complete
                RequestStatus.Declined => true,                    // Request declined - important to know
                RequestStatus.Cancelled => true,                   // Request cancelled - important to know

                // Non-critical statuses (no notification needed):
                // - Pending: User just submitted, no need to notify
                // - InProgress: Internal processing
                // - ReturnedToBase: Internal milestone
                // - FinishedWashingGoingToBase: Internal robot movement
                // - FinishedWashingAwaitingPickup: Covered by FinishedWashing notification
                // - FinishedWashingAtBase: Internal status
                _ => false
            };
        }

        // Helper methods for future implementation

        // private async Task SendSmsAsync(string phoneNumber, string message)
        // {
        //     // Implement SMS sending using Twilio or other SMS gateway
        //     await Task.CompletedTask;
        // }

        // private async Task SendEmailAsync(string email, string subject, string body)
        // {
        //     // Implement email sending using SMTP or SendGrid
        //     await Task.CompletedTask;
        // }

        // private async Task SendPushNotificationAsync(string userId, string title, string message)
        // {
        //     // Implement push notification using FCM (Firebase Cloud Messaging) or similar
        //     await Task.CompletedTask;
        // }
    }
}
