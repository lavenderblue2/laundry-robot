using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AdministratorWeb.Models
{
    public class Message
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Customer ID who is part of this conversation
        /// </summary>
        [Required]
        public string CustomerId { get; set; } = string.Empty;

        /// <summary>
        /// Customer name for display
        /// </summary>
        public string CustomerName { get; set; } = string.Empty;

        /// <summary>
        /// ID of the sender (CustomerId for customer, AdminId for admin)
        /// </summary>
        [Required]
        public string SenderId { get; set; } = string.Empty;

        /// <summary>
        /// Name of the sender for display
        /// </summary>
        public string SenderName { get; set; } = string.Empty;

        /// <summary>
        /// Type of sender: "Customer" or "Admin"
        /// </summary>
        [Required]
        public string SenderType { get; set; } = string.Empty;

        /// <summary>
        /// Message content/text
        /// </summary>
        [Required]
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// When the message was sent
        /// </summary>
        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the message has been read by the recipient
        /// </summary>
        public bool IsRead { get; set; } = false;

        /// <summary>
        /// When the message was read (if read)
        /// </summary>
        public DateTime? ReadAt { get; set; }

        /// <summary>
        /// Optional: Associated request ID for context
        /// </summary>
        public int? RequestId { get; set; }

        /// <summary>
        /// Optional: Image attachment URL
        /// </summary>
        public string? ImageUrl { get; set; }
    }
}
