using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace AdministratorWeb.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;
        
        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;
        
        [Display(Name = "Full Name")]
        public string FullName => $"{FirstName} {LastName}";
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? LastLoginAt { get; set; }
        
        public bool IsActive { get; set; } = true;
        
        // Beacon assignment for room identification using MAC address
        [Display(Name = "Assigned Beacon MAC")]
        [StringLength(17)]
        public string? AssignedBeaconMacAddress { get; set; }
        
        [Display(Name = "Assigned Room")]
        [StringLength(100)]
        public string? RoomName { get; set; }
        
        [Display(Name = "Room Description")]
        [StringLength(200)]
        public string? RoomDescription { get; set; }
        
        public ICollection<LaundryRequest> HandledRequests { get; set; } = new List<LaundryRequest>();
    }
}