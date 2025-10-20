namespace AdministratorWeb.Models.DTOs
{
    public class BeaconCreateDto
    {
        public IEnumerable<RoomOption> AvailableRooms { get; set; } = new List<RoomOption>();
    }

    public class BeaconEditDto
    {
        public BluetoothBeacon Beacon { get; set; } = null!;
        public IEnumerable<RoomOption> AvailableRooms { get; set; } = new List<RoomOption>();
    }
}