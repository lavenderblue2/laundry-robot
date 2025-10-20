using System.ComponentModel.DataAnnotations;
using RobotProject.Shared.DTOs;

namespace AdministratorWeb.Models
{
    /// <summary>
    /// Persisted robot state in database to survive service restarts
    /// </summary>
    public class RobotState
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string RobotName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        public bool IsActive { get; set; } = true;

        public bool CanAcceptRequests { get; set; } = true;

        public RobotStatus Status { get; set; } = RobotStatus.Available;

        [MaxLength(500)]
        public string? CurrentTask { get; set; }

        [MaxLength(200)]
        public string? CurrentLocation { get; set; }

        public bool IsFollowingLine { get; set; } = false;

        // Line following color settings
        public byte FollowColorR { get; set; } = 0;
        public byte FollowColorG { get; set; } = 0;
        public byte FollowColorB { get; set; } = 0;

        // Timestamps
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public DateTime? LastSeen { get; set; }

        // Additional state for recovery
        [MaxLength(100)]
        public string? LastKnownBeaconMac { get; set; }

        [MaxLength(200)]
        public string? LastKnownRoom { get; set; }

        public int? LastLinePosition { get; set; }
    }
}
