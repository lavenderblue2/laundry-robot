using System.ComponentModel.DataAnnotations;

namespace AdministratorWeb.Models
{
    /// <summary>
    /// Room model to serve as templates and dropdown menu items
    /// Used for standardizing room names across the system
    /// </summary>
    public class Room
    {
        /// <summary>
        /// Primary key for the room
        /// </summary>
        public int Id { get; set; }
        
        /// <summary>
        /// Name of the room (unique identifier)
        /// </summary>
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Optional description for the room
        /// </summary>
        [StringLength(200)]
        public string? Description { get; set; }
        
        /// <summary>
        /// Whether this room is active and available for selection
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// Timestamp when this room was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Timestamp when this room was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// User who created this room entry
        /// </summary>
        public string? CreatedBy { get; set; }
        
        /// <summary>
        /// User who last updated this room entry
        /// </summary>
        public string? UpdatedBy { get; set; }

        /// <summary>
        /// Floor color R component (0-255) for room arrival detection
        /// </summary>
        public byte? FloorColorR { get; set; }

        /// <summary>
        /// Floor color G component (0-255) for room arrival detection
        /// </summary>
        public byte? FloorColorG { get; set; }

        /// <summary>
        /// Floor color B component (0-255) for room arrival detection
        /// </summary>
        public byte? FloorColorB { get; set; }

        /// <summary>
        /// Floor color as RGB byte array, null if any component is null
        /// </summary>
        public byte[]? FloorColorRgb =>
            FloorColorR.HasValue && FloorColorG.HasValue && FloorColorB.HasValue
                ? new[] { FloorColorR.Value, FloorColorG.Value, FloorColorB.Value }
                : null;
    }
}