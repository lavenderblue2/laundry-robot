namespace AdministratorWeb.Models.DTOs
{
    public class DashboardIndexDto
    {
        public int PendingRequests { get; set; }
        public int ActiveRobots { get; set; }
        public int TodayRequests { get; set; }
        public decimal TotalRevenue { get; set; }
        public IEnumerable<object> Users { get; set; } = new List<object>();
    }
}