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

            return Ok(new
            {
                paymentId = payment.Id,
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
    }

    public class PaymentRequest
    {
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CreditCard;
        public string? PaymentReference { get; set; }
        public string? Notes { get; set; }
    }
}