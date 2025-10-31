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

            // Save changes first to generate payment.Id
            await _context.SaveChangesAsync();

            // Auto-generate receipt (for record keeping)
            Receipt? receipt = null;
            try
            {
                Console.WriteLine($"[RECEIPT DEBUG] Attempting to generate receipt for payment ID: {payment.Id}");
                receipt = await GenerateReceiptAsync(payment.Id);
                Console.WriteLine($"[RECEIPT DEBUG] Receipt generated successfully: {receipt?.ReceiptNumber} (ID: {receipt?.Id})");
            }
            catch (Exception ex)
            {
                // Log error but don't fail the payment
                Console.WriteLine($"[RECEIPT ERROR] Failed to generate receipt for payment {payment.Id}: {ex.Message}");
                Console.WriteLine($"[RECEIPT ERROR] Stack trace: {ex.StackTrace}");
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

            // Get settings for rate info
            var settings = await _context.LaundrySettings.FirstOrDefaultAsync();

            return Ok(new
            {
                requestId = laundryRequest.Id,
                customerName = laundryRequest.CustomerName,
                customerId = laundryRequest.CustomerId,
                isPaid = laundryRequest.IsPaid,
                totalCost = laundryRequest.TotalCost,
                weight = laundryRequest.Weight,
                completedAt = laundryRequest.CompletedAt,
                ratePerKg = settings?.RatePerKg ?? 0,
                payment = payment != null ? new
                {
                    paymentId = payment.Id,
                    amount = payment.Amount,
                    method = payment.Method.ToString(),
                    status = payment.Status.ToString(),
                    transactionId = payment.TransactionId,
                    paymentReference = payment.PaymentReference,
                    notes = payment.Notes,
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
            Console.WriteLine($"[RECEIPT DEBUG] GenerateReceiptAsync called for payment ID: {paymentId}");

            var payment = await _context.Payments
                .Include(p => p.LaundryRequest)
                .FirstOrDefaultAsync(p => p.Id == paymentId);

            if (payment == null)
            {
                Console.WriteLine($"[RECEIPT ERROR] Payment {paymentId} not found in database");
                throw new Exception("Payment not found");
            }

            Console.WriteLine($"[RECEIPT DEBUG] Payment found: ID={payment.Id}, RequestId={payment.LaundryRequestId}");

            // Check if receipt already exists
            var existingReceipt = await _context.Receipts
                .FirstOrDefaultAsync(r => r.PaymentId == paymentId);

            if (existingReceipt != null)
            {
                Console.WriteLine($"[RECEIPT DEBUG] Receipt already exists: {existingReceipt.ReceiptNumber}");
                return existingReceipt;
            }

            // Generate receipt number
            var receiptNumber = await GenerateReceiptNumberAsync();
            Console.WriteLine($"[RECEIPT DEBUG] Generated receipt number: {receiptNumber}");

            var receipt = new Receipt
            {
                PaymentId = paymentId,
                ReceiptNumber = receiptNumber,
                GeneratedAt = DateTime.UtcNow,
                SentToCustomer = false
            };

            _context.Receipts.Add(receipt);
            Console.WriteLine($"[RECEIPT DEBUG] Receipt added to context, saving to database...");
            await _context.SaveChangesAsync();
            Console.WriteLine($"[RECEIPT DEBUG] Receipt saved successfully with ID: {receipt.Id}");

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

        [HttpGet("debug/{requestId}")]
        public async Task<IActionResult> DebugPaymentReceipt(int requestId)
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

            var receipt = payment != null ? await _context.Receipts
                .FirstOrDefaultAsync(r => r.PaymentId == payment.Id) : null;

            return Ok(new
            {
                requestId = requestId,
                requestExists = laundryRequest != null,
                requestIsPaid = laundryRequest?.IsPaid,
                paymentExists = payment != null,
                paymentId = payment?.Id,
                receiptExists = receipt != null,
                receiptId = receipt?.Id,
                receiptNumber = receipt?.ReceiptNumber
            });
        }

        [HttpGet("receipt/{requestId}")]
        public async Task<IActionResult> GetReceiptByRequest(int requestId)
        {
            Console.WriteLine($"[RECEIPT API] Getting receipt for request ID: {requestId}");

            var customerId = User.FindFirst("customer_id")?.Value;
            Console.WriteLine($"[RECEIPT API] Customer ID from token: {customerId}");

            if (string.IsNullOrEmpty(customerId))
            {
                Console.WriteLine($"[RECEIPT API ERROR] Invalid customer token");
                return Unauthorized("Invalid customer token");
            }

            // Get the laundry request to verify ownership
            var laundryRequest = await _context.LaundryRequests
                .FirstOrDefaultAsync(r => r.Id == requestId && r.CustomerId == customerId);

            if (laundryRequest == null)
            {
                Console.WriteLine($"[RECEIPT API ERROR] Request {requestId} not found or not owned by customer {customerId}");
                return NotFound("Request not found or not authorized");
            }

            Console.WriteLine($"[RECEIPT API] Request found: ID={laundryRequest.Id}, IsPaid={laundryRequest.IsPaid}");

            // Get payment - THIS IS ALL WE NEED!
            var payment = await _context.Payments
                .Include(p => p.LaundryRequest)
                .FirstOrDefaultAsync(p => p.LaundryRequestId == requestId);

            if (payment == null)
            {
                Console.WriteLine($"[RECEIPT API ERROR] No payment found for request {requestId}");
                return NotFound(new { message = "No payment found for this request" });
            }

            Console.WriteLine($"[RECEIPT API] Payment found: ID={payment.Id}, Amount={payment.Amount}, Method={payment.Method}");

            // Try to get receipt record (if it exists), but don't require it
            var receipt = await _context.Receipts
                .FirstOrDefaultAsync(r => r.PaymentId == payment.Id);

            // Generate receipt number from payment data if no receipt record exists
            string receiptNumber;
            DateTime generatedAt;

            if (receipt != null)
            {
                receiptNumber = receipt.ReceiptNumber;
                generatedAt = receipt.GeneratedAt;
            }
            else
            {
                // Use payment ID and date to create a receipt number
                var paymentDate = payment.ProcessedAt ?? DateTime.UtcNow;
                receiptNumber = $"RCP-{paymentDate.Year}-{payment.Id:D6}";
                generatedAt = paymentDate;
            }

            // Get settings for rate information
            var settings = await _context.LaundrySettings.FirstOrDefaultAsync();

            Console.WriteLine($"[RECEIPT API] Returning receipt data: Number={receiptNumber}, Amount={payment.Amount}");

            // Return receipt data from payment - no separate Receipt table needed!
            return Ok(new
            {
                receiptNumber = receiptNumber,
                generatedAt = generatedAt,
                customerName = payment.CustomerName,
                customerId = payment.CustomerId,
                amount = payment.Amount,
                paymentMethod = payment.Method.ToString(),
                paidAt = payment.ProcessedAt,
                transactionId = payment.TransactionId,
                paymentReference = payment.PaymentReference,
                weight = laundryRequest.Weight,
                ratePerKg = settings?.RatePerKg ?? 0,
                requestId = laundryRequest.Id,
                scheduledAt = laundryRequest.ScheduledAt,
                completedAt = laundryRequest.CompletedAt,
                notes = payment.Notes
            });
        }

    }

    public class PaymentRequest
    {
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CreditCard;
        public string? PaymentReference { get; set; }
        public string? Notes { get; set; }
    }
}