using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdministratorWeb.Data;
using AdministratorWeb.Models;
using System.Security.Claims;

namespace AdministratorWeb.Controllers.Api
{
    /// <summary>
    /// MOBILE APP CONTROLLER - Used by mobile application for receipt access
    /// MUST use [Authorize] for mobile app authentication
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "ApiPolicy")]
    public class ReceiptController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReceiptController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Get receipt by request ID - users can only access their own receipts
        /// </summary>
        [HttpGet("by-request/{requestId}")]
        public async Task<IActionResult> GetReceiptByRequest(int requestId)
        {
            var customerId = User.FindFirst("customer_id")?.Value;
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized(new { error = "Invalid customer token" });
            }

            // Verify the request belongs to the customer
            var laundryRequest = await _context.LaundryRequests
                .FirstOrDefaultAsync(r => r.Id == requestId && r.CustomerId == customerId);

            if (laundryRequest == null)
            {
                return NotFound(new { error = "Request not found" });
            }

            // Find the payment for this request
            var payment = await _context.Payments
                .Include(p => p.LaundryRequest)
                .FirstOrDefaultAsync(p => p.LaundryRequestId == requestId);

            if (payment == null)
            {
                return NotFound(new { error = "No payment found for this request" });
            }

            // Find the receipt
            var receipt = await _context.Receipts
                .Include(r => r.Payment)
                    .ThenInclude(p => p.LaundryRequest)
                .FirstOrDefaultAsync(r => r.PaymentId == payment.Id);

            if (receipt == null)
            {
                return NotFound(new { error = "No receipt found for this payment" });
            }

            // Get business settings
            var settings = await _context.LaundrySettings.FirstOrDefaultAsync();

            return Ok(MapReceiptToDto(receipt, settings));
        }

        /// <summary>
        /// Get receipt by receipt ID - users can only access their own receipts
        /// </summary>
        [HttpGet("view/{id}")]
        public async Task<IActionResult> GetReceipt(int id)
        {
            var customerId = User.FindFirst("customer_id")?.Value;
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized(new { error = "Invalid customer token" });
            }

            var receipt = await _context.Receipts
                .Include(r => r.Payment)
                    .ThenInclude(p => p.LaundryRequest)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (receipt == null)
            {
                return NotFound(new { error = "Receipt not found" });
            }

            // Verify the receipt belongs to the customer
            if (receipt.Payment.CustomerId != customerId)
            {
                return Forbid();
            }

            // Get business settings
            var settings = await _context.LaundrySettings.FirstOrDefaultAsync();

            return Ok(MapReceiptToDto(receipt, settings));
        }

        /// <summary>
        /// Get web URL for receipt
        /// </summary>
        [HttpGet("{id}/web-url")]
        public async Task<IActionResult> GetReceiptWebUrl(int id)
        {
            var customerId = User.FindFirst("customer_id")?.Value;
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized(new { error = "Invalid customer token" });
            }

            var receipt = await _context.Receipts
                .Include(r => r.Payment)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (receipt == null)
            {
                return NotFound(new { error = "Receipt not found" });
            }

            // Verify the receipt belongs to the customer
            if (receipt.Payment.CustomerId != customerId)
            {
                return Forbid();
            }

            var url = $"{Request.Scheme}://{Request.Host}/Accounting/ViewReceiptPrint/{receipt.Id}";
            return Ok(new { url });
        }

        private object MapReceiptToDto(Receipt receipt, LaundrySettings? settings)
        {
            var payment = receipt.Payment;
            var request = payment.LaundryRequest;
            var isRefunded = payment.Status == PaymentStatus.Refunded;

            return new
            {
                id = receipt.Id,
                receiptNumber = receipt.ReceiptNumber,
                generatedAt = receipt.GeneratedAt,
                business = new
                {
                    businessName = settings?.CompanyName ?? "Autonomous Laundry Service",
                    businessAddress = settings?.CompanyAddress ?? "",
                    businessPhone = settings?.CompanyPhone ?? "",
                    businessEmail = settings?.CompanyEmail ?? "",
                    taxIdentificationNumber = (string?)null
                },
                customer = new
                {
                    customerName = payment.CustomerName,
                    customerPhone = request.CustomerPhone,
                    customerAddress = request.Address
                },
                lineItems = new[]
                {
                    new
                    {
                        description = $"Laundry Service (Request #{request.Id})\nDate: {request.RequestedAt:MMM dd, yyyy}",
                        quantity = request.Weight ?? 0,
                        unit = "kg",
                        unitPrice = request.PricePerKg,
                        amount = payment.Amount
                    }
                },
                subtotal = payment.Amount,
                taxAmount = 0,
                totalAmount = payment.Amount,
                payment = new
                {
                    paymentMethod = payment.Method.ToString(),
                    transactionId = payment.TransactionId,
                    paymentReference = payment.PaymentReference,
                    status = payment.Status.ToString(),
                    refundAmount = payment.RefundAmount,
                    refundedAt = payment.RefundedAt,
                    refundReason = payment.RefundReason
                }
            };
        }
    }
}
