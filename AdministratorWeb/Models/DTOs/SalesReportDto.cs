namespace AdministratorWeb.Models.DTOs;

public class SalesReportDto
{
    // Summary Statistics
    public decimal TotalRevenue { get; set; }
    public int TotalTransactions { get; set; }
    public decimal AverageTransactionValue { get; set; }
    public int CompletedCount { get; set; }
    public int PendingCount { get; set; }
    public int FailedCount { get; set; }

    // Period Information
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string PeriodLabel { get; set; } = string.Empty;

    // Daily Breakdown
    public List<DailyRevenueDto> DailyRevenue { get; set; } = new();

    // Customer Rankings
    public List<CustomerRevenueDto> TopCustomers { get; set; } = new();

    // Payment Method Breakdown
    public Dictionary<string, decimal> RevenueByMethod { get; set; } = new();

    // Status Breakdown
    public Dictionary<string, int> TransactionsByStatus { get; set; } = new();
}

public class DailyRevenueDto
{
    public DateTime Date { get; set; }
    public decimal Revenue { get; set; }
    public int TransactionCount { get; set; }
}

public class CustomerRevenueDto
{
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalSpent { get; set; }
    public int TransactionCount { get; set; }
}
