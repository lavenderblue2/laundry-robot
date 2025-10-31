using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdministratorWeb.Data;
using AdministratorWeb.Models;
using System.Security.Claims;

namespace AdministratorWeb.Controllers.Api
{
    /// <summary>
    /// MOBILE APP CONTROLLER - Used by mobile application
    /// MUST use [Authorize] for mobile app authentication
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "ApiPolicy")]
    public class PaymentController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PaymentController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("{requestId}/pay")]
        public async Task<IActionResult> ProcessPayment(int requestId, [FromBody] PaymentRequest paymentRequest)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var customerId = User.FindFirst("customer_id")?.Value;
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized("Invalid customer token");
            }

            var laundryRequest = await _context.LaundryRequests
                .FirstOrDefaultAsync(r => r.Id == requestId && r.CustomerId == customerId);

            if (laundryRequest == null)
            {
                return NotFound("Request not found");
            }

            if (laundryRequest.IsPaid)
            {
                return BadRequest("This request has already been paid");
            }

            if (!laundryRequest.TotalCost.HasValue)
            {
                return BadRequest("Request total cost has not been calculated yet");
            }

            // Calculate cost if not set (based on weight and rate)
            if (laundryRequest.Weight.HasValue && !laundryRequest.TotalCost.HasValue)
            {
                var settings = await _context.LaundrySettings.FirstOrDefaultAsync();
                if (settings != null)
                {
                    laundryRequest.TotalCost = laundryRequest.Weight.Value * settings.RatePerKg;
                }
            }

            // Create payment record
            var payment = new Payment
            {
                LaundryRequestId = requestId,
                CustomerId = customerId,
                CustomerName = laundryRequest.CustomerName,
                Amount = laundryRequest.TotalCost.Value,
                Method = paymentRequest.PaymentMethod,
                TransactionId = GenerateTransactionId(),
                PaymentReference = paymentRequest.PaymentReference,
                Notes = paymentRequest.Notes,
                Status = PaymentStatus.Completed, // In a real scenario, this would be processed by payment gateway
                ProcessedAt = DateTime.UtcNow
            };

            _context.Payments.Add(payment);

            // Mark request as paid
            laundryRequest.IsPaid = true;

            await _context.SaveChangesAsync();

            // Auto-generate receipt and send notification
            Receipt? receipt = null;
            try
            {
                receipt = await GenerateReceiptAsync(payment.Id);
                await SendReceiptNotificationAsync(receipt.Id, customerId);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the payment
                Console.WriteLine($"Failed to generate receipt: {ex.Message}");
            }

            return Ok(new
            {
                paymentId = payment.Id,
                receiptId = receipt?.Id,
                receiptNumber = receipt?.ReceiptNumber,
                transactionId = payment.TransactionId,
                amount = payment.Amount,
                status = payment.Status.ToString(),
                processedAt = payment.ProcessedAt,
                message = "Payment processed successfully"
            });
        }

        [HttpGet("{requestId}/payment-status")]
        public async Task<IActionResult> GetPaymentStatus(int requestId)
        {
            var customerId = User.FindFirst("customer_id")?.Value;
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized("Invalid customer token");
            }

            var laundryRequest = await _context.LaundryRequests
                .FirstOrDefaultAsync(r => r.Id == requestId && r.CustomerId == customerId);

            if (laundryRequest == null)
            {
                return NotFound("Request not found");
            }

            var payment = await _context.Payments
                .FirstOrDefaultAsync(p => p.LaundryRequestId == requestId);

            return Ok(new
            {
                requestId = laundryRequest.Id,
                isPaid = laundryRequest.IsPaid,
                totalCost = laundryRequest.TotalCost,
                weight = laundryRequest.Weight,
                payment = payment != null ? new
                {
                    paymentId = payment.Id,
                    amount = payment.Amount,
                    method = payment.Method.ToString(),
                    status = payment.Status.ToString(),
                    transactionId = payment.TransactionId,
                    processedAt = payment.ProcessedAt
                } : null
            });
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetPaymentHistory()
        {
            var customerId = User.FindFirst("customer_id")?.Value;
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized("Invalid customer token");
            }

            var payments = await _context.Payments
                .Include(p => p.LaundryRequest)
                .Where(p => p.CustomerId == customerId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id,
                    p.Amount,
                    p.Method,
                    p.Status,
                    p.TransactionId,
                    p.CreatedAt,
                    p.ProcessedAt,
                    Request = new
                    {
                        p.LaundryRequest.Id,
                        p.LaundryRequest.Type,
                        p.LaundryRequest.Address,
                        p.LaundryRequest.Weight,
                        p.LaundryRequest.RequestedAt
                    }
                })
                .ToListAsync();

            return Ok(payments);
        }

        private string GenerateTransactionId()
        {
            return $"TXN_{DateTime.UtcNow:yyyyMMdd}_{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
        }

        // ==================== RECEIPT GENERATION METHODS ====================

        private async Task<Receipt> GenerateReceiptAsync(int paymentId)
        {
            var payment = await _context.Payments
                .Include(p => p.LaundryRequest)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
            {
                throw new Exception("Payment not found");
            }

            // Check if receipt already exists
            var existingReceipt = await _context.Receipts
                .FirstOrDefaultAsync(r => r.PaymentId == paymentId);

            if (existingReceipt != null)
            {
                return existingReceipt;
            }

            // Generate receipt number
            var receiptNumber = await GenerateReceiptNumberAsync();

            var receipt = new Receipt
            {
                PaymentId = paymentId,
                ReceiptNumber = receiptNumber,
                GeneratedAt = DateTime.UtcNow,
                SentToCustomer = false
            };

            _context.Receipts.Add(receipt);
            await _context.SaveChangesAsync();

            return receipt;
        }

        private async Task<string> GenerateReceiptNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"RCP-{year}";

            var lastReceipt = await _context.Receipts
                .Where(r => r.ReceiptNumber.StartsWith(prefix))
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync();

            var sequence = 1;
            if (lastReceipt != null)
            {
                var parts = lastReceipt.ReceiptNumber.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out int lastSeq))
                {
                    sequence = lastSeq + 1;
                }
            }

            return $"{prefix}-{sequence:D6}"; // RCP-2025-000001
        }

        private async Task SendReceiptNotificationAsync(int receiptId, string customerId)
        {
            var receipt = await _context.Receipts
                .Include(r => r.Payment)
                .FirstOrDefaultAsync(r => r.Id == receiptId);
            if (receipt == null) return;

            // Use request host from HttpContext for notification URL
            var receiptUrl = $"{Request.Scheme}://{Request.Host}/Accounting/ViewReceipt/{receiptId}";

            var message = new Message
            {
                SenderId = "System",
                SenderName = "System",
                SenderType = "Admin",
                CustomerId = customerId,
                CustomerName = receipt.Payment.CustomerName,
                Content = $"PAYMENT RECEIPT\n\nYour payment has been received! Receipt #{receipt.ReceiptNumber} is ready.\n\nView it here: {receiptUrl}",
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Messages.Add(message);

            receipt.SentToCustomer = true;
            receipt.SentAt = DateTime.UtcNow;
            receipt.SentMethod = "Notification";

            await _context.SaveChangesAsync();
        }
    }

    public class PaymentRequest
    {
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CreditCard;
        public string? PaymentReference { get; set; }
        public string? Notes { get; set; }
    }
}