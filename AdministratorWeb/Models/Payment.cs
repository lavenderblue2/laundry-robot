using System.ComponentModel.DataAnnotations;

namespace AdministratorWeb.Models
{
    public enum PaymentMethod
    {
        CreditCard,
        DebitCard,
        PayPal,
        Cash,
        BankTransfer,
        DigitalWallet
    }

    public enum PaymentStatus
    {
        Pending,
        Completed,
        Failed,
        Refunded,
        Cancelled
    }

    public class Payment
    {
        public int Id { get; set; }
        
        [Required]
        public int LaundryRequestId { get; set; }
        public LaundryRequest LaundryRequest { get; set; } = null!;
        
        [Required]
        public string CustomerId { get; set; } = string.Empty;
        
        [Required]
        public string CustomerName { get; set; } = string.Empty;
        
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }
        
        public PaymentMethod Method { get; set; } = PaymentMethod.CreditCard;
        
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
        
        public string? TransactionId { get; set; }
        
        public string? PaymentReference { get; set; }
        
        public string? Notes { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? ProcessedAt { get; set; }
        
        public string? ProcessedByUserId { get; set; }
        public ApplicationUser? ProcessedByUser { get; set; }
        
        public string? FailureReason { get; set; }
    }
}