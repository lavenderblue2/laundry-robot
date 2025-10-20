using System.ComponentModel.DataAnnotations;

namespace AdministratorWeb.Models
{
    public enum RequestStatus
    {
        // laundry request
        // - bot goes from base to member room, member puts laundry and bot goes back to base
        Pending,
        Accepted,
        InProgress,
        RobotEnRoute,
        ArrivedAtRoom,
        LaundryLoaded,
        ReturnedToBase,
        WeighingComplete,
        PaymentPending,
        Completed,
        Declined,
        Cancelled,
        Washing,
        
        // picking up
        // the laundry has been washed, now the robot will go to the user room to deliver the laundry
        FinishedWashingArrivedAtRoom,
        FinishedWashingReadyToDeliver, // customer chose delivery, admin needs to load laundry on robot
        FinishedWashingGoingToRoom,
        FinishedWashingGoingToBase, // going to base after user clicked deliver
        FinishedWashingAwaitingPickup,
        FinishedWashing,
        FinishedWashingAtBase
    }

    public enum RequestType
    {
        Pickup,
        Delivery,
        PickupAndDelivery
    }

    public class LaundryRequest
    {
        public int Id { get; set; }
        
        [Required]
        public string CustomerId { get; set; } = string.Empty;
        
        [Required]
        public string CustomerName { get; set; } = string.Empty;
        
        [Required]
        public string CustomerPhone { get; set; } = string.Empty;
        
        [Required]
        public string Address { get; set; } = string.Empty;
        
        public string? Instructions { get; set; }
        
        public RequestType Type { get; set; }
        
        public RequestStatus Status { get; set; } = RequestStatus.Pending;
        
        public decimal? Weight { get; set; }
        
        public decimal? TotalCost { get; set; }
        
        public bool IsPaid { get; set; } = false;
        
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? ScheduledAt { get; set; }
        
        public DateTime? CompletedAt { get; set; }
        
        public DateTime? ProcessedAt { get; set; }
        
        public string? AssignedRobotName { get; set; }
        
        public string? HandledById { get; set; }
        public ApplicationUser? HandledBy { get; set; }
        
        public string? DeclineReason { get; set; }
        
        // Workflow timestamps
        public DateTime? AcceptedAt { get; set; }
        public DateTime? RobotDispatchedAt { get; set; }
        public DateTime? ArrivedAtRoomAt { get; set; }
        public DateTime? LaundryLoadedAt { get; set; }
        public DateTime? ReturnedToBaseAt { get; set; }
        public DateTime? WeighingCompletedAt { get; set; }
        public DateTime? PaymentRequestedAt { get; set; }
        public DateTime? PaymentCompletedAt { get; set; }
        
        // Beacon and room information
        public string? AssignedBeaconMacAddress { get; set; }
        public string? RoomName { get; set; }
        
        // Payment information
        public string? PaymentMethod { get; set; }
        public string? PaymentReference { get; set; }
        public string? PaymentNotes { get; set; }
        
        // Pricing information
        public decimal PricePerKg { get; set; } = 25.00m;
        public decimal MinimumCharge { get; set; } = 50.00m;
    }
}