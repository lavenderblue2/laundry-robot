namespace AdministratorWeb.Models.DTOs
{
    public class RequestsIndexDto
    {
        public IEnumerable<LaundryRequest> Requests { get; set; } = new List<LaundryRequest>();
        public IEnumerable<ConnectedRobot> AvailableRobots { get; set; } = new List<ConnectedRobot>();
        public IEnumerable<ApplicationUser> Customers { get; set; } = new List<ApplicationUser>();
    }
}