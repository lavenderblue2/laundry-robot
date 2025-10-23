namespace AdministratorWeb.Models.DTOs;

public class NotificationSettingsDto
{
    public bool NotificationsEnabled { get; set; }
    public bool VibrationEnabled { get; set; }
    public bool RobotArrivalEnabled { get; set; }
    public bool RobotDeliveryEnabled { get; set; }
    public bool MessagesEnabled { get; set; }
    public bool StatusChangesEnabled { get; set; }
    public string RobotArrivalSound { get; set; } = "default";
    public string MessageSound { get; set; } = "default";
}
