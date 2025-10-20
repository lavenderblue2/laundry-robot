namespace AdministratorWeb.Models.DTOs;

public class PaymentsFilterDto
{
    public string? StatusFilter { get; set; }
    public string? MethodFilter { get; set; }
    public string? FromFilter { get; set; }
    public string? ToFilter { get; set; }
    public string[] PaymentStatuses { get; set; } = Array.Empty<string>();
    public string[] PaymentMethods { get; set; } = Array.Empty<string>();
}