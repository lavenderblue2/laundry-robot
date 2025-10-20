using AdministratorWeb.Models;

namespace AdministratorWeb.Models.DTOs;

public class CustomerProfileDto
{
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalSpent { get; set; }
    public int TotalRequests { get; set; }
    public int CompletedRequests { get; set; }
    public decimal OutstandingAmount { get; set; }
    public IEnumerable<LaundryRequest> Requests { get; set; } = new List<LaundryRequest>();
}