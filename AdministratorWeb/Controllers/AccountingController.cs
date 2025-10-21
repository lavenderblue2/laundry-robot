using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdministratorWeb.Data;
using AdministratorWeb.Models;
using AdministratorWeb.Models.DTOs;

namespace AdministratorWeb.Controllers
{
    /// <summary>
    /// MOBILE APP CONTROLLER - Used by mobile application
    /// MUST use [Authorize] for mobile app authentication
    /// </summary>
    [Authorize]
    public class AccountingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountingController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;
            var thisMonth = new DateTime(today.Year, today.Month, 1);
            var thisYear = new DateTime(today.Year, 1, 1);

            // Financial overview with adjustments
            var totalRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed)
                .SumAsync(p => p.Amount);

            var todayRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed && p.ProcessedAt!.Value.Date == today)
                .SumAsync(p => p.Amount);

            var monthRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed && p.ProcessedAt >= thisMonth)
                .SumAsync(p => p.Amount);

            var yearRevenue = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed && p.ProcessedAt >= thisYear)
                .SumAsync(p => p.Amount);

            // Apply adjustments
            var totalAdjustments = await _context.PaymentAdjustments
                .SumAsync(a => a.Type == AdjustmentType.AddRevenue || a.Type == AdjustmentType.Correction || a.Type == AdjustmentType.Other ? a.Amount : -a.Amount);

            var todayAdjustments = await _context.PaymentAdjustments
                .Where(a => a.EffectiveDate.Date == today)
                .SumAsync(a => a.Type == AdjustmentType.AddRevenue || a.Type == AdjustmentType.Correction || a.Type == AdjustmentType.Other ? a.Amount : -a.Amount);

            var monthAdjustments = await _context.PaymentAdjustments
                .Where(a => a.EffectiveDate >= thisMonth)
                .SumAsync(a => a.Type == AdjustmentType.AddRevenue || a.Type == AdjustmentType.Correction || a.Type == AdjustmentType.Other ? a.Amount : -a.Amount);

            totalRevenue += totalAdjustments;
            todayRevenue += todayAdjustments;
            monthRevenue += monthAdjustments;

            // Payment statistics
            var totalPayments = await _context.Payments.CountAsync();
            var completedPayments = await _context.Payments.CountAsync(p => p.Status == PaymentStatus.Completed);
            var pendingPayments = await _context.Payments.CountAsync(p => p.Status == PaymentStatus.Pending);
            var failedPayments = await _context.Payments.CountAsync(p => p.Status == PaymentStatus.Failed);

            // Outstanding payments (requests not paid)
            var outstandingAmount = await _context.LaundryRequests
                .Where(r => !r.IsPaid && r.TotalCost.HasValue && r.Status == RequestStatus.Completed)
                .SumAsync(r => r.TotalCost!.Value);

            var outstandingCount = await _context.LaundryRequests
                .CountAsync(r => !r.IsPaid && r.TotalCost.HasValue && r.Status == RequestStatus.Completed);

            // Recent payments
            var recentPayments = await _context.Payments
                .Include(p => p.LaundryRequest)
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .ToListAsync();

            var accountingDto = new AccountingIndexDto
            {
                TotalRevenue = totalRevenue,
                TodayRevenue = todayRevenue,
                MonthRevenue = monthRevenue,
                YearRevenue = yearRevenue,
                TotalPayments = totalPayments,
                CompletedPayments = completedPayments,
                PendingPayments = pendingPayments,
                FailedPayments = failedPayments,
                OutstandingAmount = outstandingAmount,
                OutstandingCount = outstandingCount
            };
            ViewData["AccountingData"] = accountingDto;

            return View(recentPayments);
        }

        public async Task<IActionResult> Payments(string? status, string? method, DateTime? from, DateTime? to, string? customerSearch, decimal? minAmount, decimal? maxAmount)
        {
            var query = _context.Payments.Include(p => p.LaundryRequest).AsQueryable();

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<PaymentStatus>(status, out var paymentStatus))
            {
                query = query.Where(p => p.Status == paymentStatus);
            }

            if (!string.IsNullOrEmpty(method) && Enum.TryParse<PaymentMethod>(method, out var paymentMethod))
            {
                query = query.Where(p => p.Method == paymentMethod);
            }

            if (from.HasValue)
            {
                query = query.Where(p => p.CreatedAt >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(p => p.CreatedAt <= to.Value.AddDays(1));
            }

            if (!string.IsNullOrEmpty(customerSearch))
            {
                query = query.Where(p => p.CustomerName.Contains(customerSearch) || p.CustomerId.Contains(customerSearch));
            }

            if (minAmount.HasValue)
            {
                query = query.Where(p => p.Amount >= minAmount.Value);
            }

            if (maxAmount.HasValue)
            {
                query = query.Where(p => p.Amount <= maxAmount.Value);
            }

            var payments = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();

            var filterDto = new PaymentsFilterDto
            {
                StatusFilter = status,
                MethodFilter = method,
                FromFilter = from?.ToString("yyyy-MM-dd"),
                ToFilter = to?.ToString("yyyy-MM-dd"),
                CustomerSearch = customerSearch,
                MinAmount = minAmount,
                MaxAmount = maxAmount,
                PaymentStatuses = Enum.GetNames<PaymentStatus>(),
                PaymentMethods = Enum.GetNames<PaymentMethod>()
            };
            ViewData["PaymentsFilterData"] = filterDto;

            return View(payments);
        }

        public async Task<IActionResult> Outstanding()
        {
            var outstandingRequests = await _context.LaundryRequests
                .Where(r => !r.IsPaid && r.TotalCost.HasValue && r.Status == RequestStatus.Completed)
                .OrderByDescending(r => r.CompletedAt)
                .ToListAsync();

            return View(outstandingRequests);
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsPaid(int requestId, PaymentMethod paymentMethod, string? notes)
        {
            var request = await _context.LaundryRequests.FindAsync(requestId);
            if (request == null || request.IsPaid)
            {
                return NotFound();
            }

            var payment = new Payment
            {
                LaundryRequestId = requestId,
                CustomerId = request.CustomerId,
                CustomerName = request.CustomerName,
                Amount = request.TotalCost!.Value,
                Method = paymentMethod,
                Status = PaymentStatus.Completed,
                TransactionId = $"ADMIN_{DateTime.UtcNow:yyyyMMdd}_{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                Notes = notes,
                ProcessedAt = DateTime.UtcNow,
                ProcessedByUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            };

            _context.Payments.Add(payment);
            request.IsPaid = true;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Payment recorded successfully.";

            return RedirectToAction(nameof(Outstanding));
        }

        public async Task<IActionResult> CustomerProfile(string customerId)
        {
            var requests = await _context.LaundryRequests
                .Where(r => r.CustomerId == customerId)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            var payments = await _context.Payments
                .Include(p => p.LaundryRequest)
                .Where(p => p.CustomerId == customerId)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            if (!requests.Any())
            {
                return NotFound("Customer not found");
            }

            var customerName = requests.First().CustomerName;
            var totalSpent = payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount);
            var totalRequests = requests.Count;
            var completedRequests = requests.Count(r => r.Status == RequestStatus.Completed);
            var outstandingAmount = requests.Where(r => !r.IsPaid && r.TotalCost.HasValue).Sum(r => r.TotalCost!.Value);

            var customerProfileDto = new CustomerProfileDto
            {
                CustomerId = customerId,
                CustomerName = customerName,
                TotalSpent = totalSpent,
                TotalRequests = totalRequests,
                CompletedRequests = completedRequests,
                OutstandingAmount = outstandingAmount,
                Requests = requests
            };
            ViewData["CustomerProfileData"] = customerProfileDto;

            return View(payments);
        }

        public async Task<IActionResult> Adjustments()
        {
            var adjustments = await _context.PaymentAdjustments
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            return View(adjustments);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAdjustment(AdjustmentType type, decimal amount, string description, string? referenceNumber, DateTime effectiveDate, string? notes)
        {
            if (amount <= 0)
            {
                TempData["Error"] = "Amount must be greater than 0.";
                return RedirectToAction(nameof(Adjustments));
            }

            if (string.IsNullOrWhiteSpace(description))
            {
                TempData["Error"] = "Description is required.";
                return RedirectToAction(nameof(Adjustments));
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var userName = User.Identity?.Name;

            var adjustment = new PaymentAdjustment
            {
                Type = type,
                Amount = amount,
                Description = description,
                ReferenceNumber = referenceNumber,
                EffectiveDate = effectiveDate,
                Notes = notes,
                CreatedByUserId = userId ?? "Unknown",
                CreatedByUserName = userName,
                CreatedAt = DateTime.UtcNow
            };

            _context.PaymentAdjustments.Add(adjustment);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Adjustment created successfully.";
            return RedirectToAction(nameof(Adjustments));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteAdjustment(int id)
        {
            var adjustment = await _context.PaymentAdjustments.FindAsync(id);
            if (adjustment == null)
            {
                TempData["Error"] = "Adjustment not found.";
                return RedirectToAction(nameof(Adjustments));
            }

            _context.PaymentAdjustments.Remove(adjustment);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Adjustment deleted successfully.";
            return RedirectToAction(nameof(Adjustments));
        }
    }
}