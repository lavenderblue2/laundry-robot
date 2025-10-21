using System.ComponentModel.DataAnnotations;

namespace AdministratorWeb.Models
{
    /// <summary>
    /// Room detection mode - determines how robots identify rooms
    /// </summary>
    public enum RoomDetectionMode
    {
        /// <summary>
        /// Use Bluetooth beacons for room identification
        /// </summary>
        Beacon = 0,

        /// <summary>
        /// Use floor color detection for room identification
        /// </summary>
        Color = 1
    }

    public class LaundrySettings
    {
        public int Id { get; set; }
        
        [Required]
        [Display(Name = "Rate per Kg")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Rate must be greater than 0")]
        public decimal RatePerKg { get; set; } = 10.00m;
        
        [Display(Name = "Company Name")]
        public string CompanyName { get; set; } = "Autonomous Laundry Service";
        
        [Display(Name = "Company Address")]
        public string? CompanyAddress { get; set; }
        
        [Display(Name = "Company Phone")]
        public string? CompanyPhone { get; set; }

        [Display(Name = "Company Email")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string? CompanyEmail { get; set; }

        [Display(Name = "Company Website")]
        public string? CompanyWebsite { get; set; }

        [Display(Name = "Company Description")]
        public string? CompanyDescription { get; set; }

        [Display(Name = "Facebook URL")]
        public string? FacebookUrl { get; set; }

        [Display(Name = "Twitter URL")]
        public string? TwitterUrl { get; set; }

        [Display(Name = "Instagram URL")]
        public string? InstagramUrl { get; set; }

        [Display(Name = "Operating Hours")]
        public string? OperatingHours { get; set; } = "8:00 AM - 6:00 PM";
        
        [Display(Name = "Maximum Weight per Request (kg)")]
        public decimal? MaxWeightPerRequest { get; set; } = 50.0m;
        
        [Display(Name = "Minimum Weight per Request (kg)")]
        public decimal? MinWeightPerRequest { get; set; } = 1.0m;

        [Display(Name = "Auto Accept Requests")]
        public bool AutoAcceptRequests { get; set; } = false;

        [Display(Name = "Room Detection Mode")]
        public RoomDetectionMode DetectionMode { get; set; } = RoomDetectionMode.Beacon;

        [Display(Name = "Line Follow Color (Red)")]
        [Range(0, 255)]
        public byte LineFollowColorR { get; set; } = 0;

        [Display(Name = "Line Follow Color (Green)")]
        [Range(0, 255)]
        public byte LineFollowColorG { get; set; } = 0;

        [Display(Name = "Line Follow Color (Blue)")]
        [Range(0, 255)]
        public byte LineFollowColorB { get; set; } = 0;

        [Display(Name = "Room Arrival Timeout (minutes)")]
        [Range(1, 60, ErrorMessage = "Timeout must be between 1 and 60 minutes")]
        public int RoomArrivalTimeoutMinutes { get; set; } = 5;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}