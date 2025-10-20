namespace AdministratorWeb.Models.DTOs
{
    public class AccountingIndexDto
    {
        public decimal TotalRevenue { get; set; }
        public decimal TodayRevenue { get; set; }
        public decimal MonthRevenue { get; set; }
        public decimal YearRevenue { get; set; }
        public int TotalPayments { get; set; }
        public int CompletedPayments { get; set; }
        public int PendingPayments { get; set; }
        public int FailedPayments { get; set; }
        public decimal OutstandingAmount { get; set; }
        public int OutstandingCount { get; set; }
    }
}