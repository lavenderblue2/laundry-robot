using System.ComponentModel.DataAnnotations;

namespace AdministratorWeb.Models.DTOs
{
    /// <summary>
    /// DTO for creating manual laundry requests via admin web interface
    /// </summary>
    public class CreateManualRequestDto
    {
        /// <summary>
        /// ID of the customer for whom the request is being created
        /// </summary>
        [Required(ErrorMessage = "Customer is required")]
        public string CustomerId { get; set; } = string.Empty;

        /// <summary>
        /// Type of manual request: RobotDelivery or WalkIn
        /// </summary>
        [Required(ErrorMessage = "Request type is required")]
        public ManualRequestType RequestType { get; set; }

        /// <summary>
        /// Weight in kilograms (required for WalkIn, ignored for RobotDelivery)
        /// </summary>
        [Range(0.1, 50.0, ErrorMessage = "Weight must be between 0.1 and 50 kg")]
        public decimal? WeightKg { get; set; }

        /// <summary>
        /// Optional notes from admin about this request
        /// </summary>
        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters")]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Type of manual request being created
    /// </summary>
    public enum ManualRequestType
    {
        /// <summary>
        /// Normal robot delivery - robot picks up from customer's room
        /// </summary>
        RobotDelivery = 0,

        /// <summary>
        /// Walk-in service - customer brought laundry to shop, no robot pickup needed
        /// </summary>
        WalkIn = 1
    }
}
