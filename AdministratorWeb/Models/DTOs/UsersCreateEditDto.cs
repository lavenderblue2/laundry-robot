namespace AdministratorWeb.Models.DTOs
{
    public class UsersCreateDto
    {
        public IEnumerable<string> AvailableRoles { get; set; } = new List<string>();
        public IEnumerable<BeaconOption> AvailableBeacons { get; set; } = new List<BeaconOption>();
        public IEnumerable<RoomOption> AvailableRooms { get; set; } = new List<RoomOption>();
    }

    public class UsersEditDto
    {
        public ApplicationUser User { get; set; } = null!;
        public IEnumerable<string> AvailableRoles { get; set; } = new List<string>();
        public IList<string> UserRoles { get; set; } = new List<string>();
        public IEnumerable<BeaconOption> AvailableBeacons { get; set; } = new List<BeaconOption>();
        public IEnumerable<RoomOption> AvailableRooms { get; set; } = new List<RoomOption>();
    }

    public class BeaconOption
    {
        public string MacAddress { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
    
    public class RoomOption
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }
}