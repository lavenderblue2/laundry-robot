using System;

namespace AdministratorWeb.Models
{
    public class Receipt
    {
        public int Id { get; set; }

        // Foreign key to payment
        public int PaymentId { get; set; }
        public Payment Payment { get; set; }

        // Receipt identification
        public string ReceiptNumber { get; set; } = string.Empty; // e.g., "RCP-2025-000001"

        // Timestamps
        public DateTime GeneratedAt { get; set; }

        // Notification tracking
        public bool SentToCustomer { get; set; } = false;
        public DateTime? SentAt { get; set; }
        public string? SentMethod { get; set; } // "Notification", "Email", etc.
    }
}
