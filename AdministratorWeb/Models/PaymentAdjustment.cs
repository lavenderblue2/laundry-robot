using System;
using System.ComponentModel.DataAnnotations;

namespace AdministratorWeb.Models
{
    /// <summary>
    /// Manual adjustments to accounting totals for corrections or special entries
    /// </summary>
    public class PaymentAdjustment
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Adjustment Type")]
        public AdjustmentType Type { get; set; }

        [Required]
        [Display(Name = "Amount")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "Description")]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Reference Number")]
        [StringLength(100)]
        public string? ReferenceNumber { get; set; }

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        [Required]
        [Display(Name = "Created By")]
        public string CreatedByUserId { get; set; } = string.Empty;

        [Display(Name = "Created By Name")]
        public string? CreatedByUserName { get; set; }

        [Display(Name = "Created At")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Effective Date")]
        public DateTime EffectiveDate { get; set; } = DateTime.Today;
    }

    public enum AdjustmentType
    {
        [Display(Name = "Add Revenue")]
        AddRevenue = 0,

        [Display(Name = "Subtract Revenue")]
        SubtractRevenue = 1,

        [Display(Name = "Complete Payment")]
        CompletePayment = 2,

        [Display(Name = "Supply Expense")]
        SupplyExpense = 3
    }
}
