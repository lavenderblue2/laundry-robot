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

            // Financial overview with adjustments - subtract refunds!
            var totalCompleted = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed)
                .SumAsync(p => p.Amount);
            var totalRefunded = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Refunded)
                .SumAsync(p => p.RefundAmount ?? p.Amount);
            var totalRevenue = totalCompleted - totalRefunded;

            var todayCompleted = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed && p.ProcessedAt!.Value.Date == today)
                .SumAsync(p => p.Amount);
            var todayRefunded = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Refunded && p.ProcessedAt!.Value.Date == today)
                .SumAsync(p => p.RefundAmount ?? p.Amount);
            var todayRevenue = todayCompleted - todayRefunded;

            var monthCompleted = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed && p.ProcessedAt >= thisMonth)
                .SumAsync(p => p.Amount);
            var monthRefunded = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Refunded && p.ProcessedAt >= thisMonth)
                .SumAsync(p => p.RefundAmount ?? p.Amount);
            var monthRevenue = monthCompleted - monthRefunded;

            var yearCompleted = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Completed && p.ProcessedAt >= thisYear)
                .SumAsync(p => p.Amount);
            var yearRefunded = await _context.Payments
                .Where(p => p.Status == PaymentStatus.Refunded && p.ProcessedAt >= thisYear)
                .SumAsync(p => p.RefundAmount ?? p.Amount);
            var yearRevenue = yearCompleted - yearRefunded;

            // Apply adjustments
            var totalAdjustments = await _context.PaymentAdjustments
                .SumAsync(a => a.Type == AdjustmentType.AddRevenue || a.Type == AdjustmentType.CompletePayment ? a.Amount :
                              (a.Type == AdjustmentType.SubtractRevenue ? -a.Amount : 0));

            var todayAdjustments = await _context.PaymentAdjustments
                .Where(a => a.EffectiveDate.Date == today)
                .SumAsync(a => a.Type == AdjustmentType.AddRevenue || a.Type == AdjustmentType.CompletePayment ? a.Amount :
                              (a.Type == AdjustmentType.SubtractRevenue ? -a.Amount : 0));

            var monthAdjustments = await _context.PaymentAdjustments
                .Where(a => a.EffectiveDate >= thisMonth)
                .SumAsync(a => a.Type == AdjustmentType.AddRevenue || a.Type == AdjustmentType.CompletePayment ? a.Amount :
                              (a.Type == AdjustmentType.SubtractRevenue ? -a.Amount : 0));

            var yearAdjustments = await _context.PaymentAdjustments
                .Where(a => a.EffectiveDate >= thisYear)
                .SumAsync(a => a.Type == AdjustmentType.AddRevenue || a.Type == AdjustmentType.CompletePayment ? a.Amount :
                              (a.Type == AdjustmentType.SubtractRevenue ? -a.Amount : 0));

            totalRevenue += totalAdjustments;
            todayRevenue += todayAdjustments;
            monthRevenue += monthAdjustments;
            yearRevenue += yearAdjustments;

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
            var query = _context.Payments
                .Include(p => p.LaundryRequest)
                .AsQueryable();

            // Load receipts separately to avoid Include issues
            var payments = await ApplyPaymentFilters(query, status, method, from, to, customerSearch, minAmount, maxAmount)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            // Load receipts for all payments
            var paymentIds = payments.Select(p => p.Id).ToList();
            var receipts = await _context.Receipts
                .Where(r => paymentIds.Contains(r.PaymentId))
                .ToListAsync();

            // Attach receipts to payments (manual join)
            ViewData["Receipts"] = receipts.ToDictionary(r => r.PaymentId);

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

        private IQueryable<Payment> ApplyPaymentFilters(
            IQueryable<Payment> query,
            string? status,
            string? method,
            DateTime? from,
            DateTime? to,
            string? customerSearch,
            decimal? minAmount,
            decimal? maxAmount)
        {
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

            return query;
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

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            Payment? paymentToReceipt = null;

            // Check if there's an existing pending payment for this request
            var existingPayment = await _context.Payments
                .FirstOrDefaultAsync(p => p.LaundryRequestId == requestId && p.Status == PaymentStatus.Pending);

            if (existingPayment != null)
            {
                // Update existing pending payment to completed
                existingPayment.Status = PaymentStatus.Completed;
                existingPayment.Method = paymentMethod;
                existingPayment.ProcessedAt = DateTime.UtcNow;
                existingPayment.ProcessedByUserId = userId;
                if (!string.IsNullOrEmpty(notes))
                {
                    existingPayment.Notes = (existingPayment.Notes ?? "") + $"\nPayment confirmed: {notes}";
                }
                paymentToReceipt = existingPayment;
            }
            else
            {
                // Create new completed payment (for older requests without pending payments)
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
                    ProcessedByUserId = userId
                };

                _context.Payments.Add(payment);
                paymentToReceipt = payment;
            }

            request.IsPaid = true;

            await _context.SaveChangesAsync();

            // Auto-generate receipt and send notification
            try
            {
                var receipt = await GenerateReceiptAsync(paymentToReceipt.Id);
                await SendReceiptNotificationAsync(receipt.Id, request.CustomerId);
                TempData["Success"] = $"Payment recorded and receipt #{receipt.ReceiptNumber} sent to customer.";
            }
            catch (Exception ex)
            {
                // Log error but don't fail the payment
                TempData["Warning"] = $"Payment recorded successfully, but failed to generate receipt: {ex.Message}";
            }

            return RedirectToAction(nameof(Outstanding));
        }

        [HttpPost]
        public async Task<IActionResult> IssueRefund(int paymentId, decimal refundAmount, string refundReason, string? notes)
        {
            var payment = await _context.Payments.Include(p => p.LaundryRequest).FirstOrDefaultAsync(p => p.Id == paymentId);
            if (payment == null || payment.Status != PaymentStatus.Completed)
            {
                TempData["Error"] = "Payment not found or not eligible for refund.";
                return RedirectToAction(nameof(Payments));
            }

            if (refundAmount <= 0 || refundAmount > payment.Amount)
            {
                TempData["Error"] = "Invalid refund amount.";
                return RedirectToAction(nameof(Payments));
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            // Update payment status
            payment.Status = PaymentStatus.Refunded;
            payment.RefundAmount = refundAmount;
            payment.RefundedAt = DateTime.UtcNow;
            payment.RefundedByUserId = userId;
            payment.RefundReason = refundReason;
            if (!string.IsNullOrEmpty(notes))
            {
                payment.Notes = (payment.Notes ?? "") + $"\nRefund: {notes}";
            }

            // If full refund, mark request as unpaid
            if (refundAmount >= payment.Amount)
            {
                payment.LaundryRequest.IsPaid = false;
            }

            // NOTE: We do NOT create a SubtractRevenue adjustment here because the refunded
            // payment itself already subtracts from revenue in the sales report calculations.
            // Creating an adjustment would double-count the refund.

            await _context.SaveChangesAsync();

            // Send refund notification to customer
            try
            {
                var receipt = await _context.Receipts
                    .FirstOrDefaultAsync(r => r.PaymentId == paymentId);

                if (receipt != null)
                {
                    var receiptUrl = $"{Request.Scheme}://{Request.Host}/Accounting/ViewReceipt/{receipt.Id}";
                    var refundMessage = new Message
                    {
                        SenderId = "System",
                        SenderName = "System",
                        SenderType = "Admin",
                        CustomerId = payment.CustomerId,
                        CustomerName = payment.CustomerName,
                        Content = $"REFUND ISSUED\n\nA refund of ₱{refundAmount:N2} has been issued for your payment.\n\nReason: {refundReason}\n\nView updated receipt: {receiptUrl}",
                        SentAt = DateTime.UtcNow,
                        IsRead = false
                    };
                    _context.Messages.Add(refundMessage);
                    await _context.SaveChangesAsync();
                }
            }
            catch
            {
                // Don't fail the refund if notification fails
            }

            TempData["Success"] = $"Refund of ₱{refundAmount:N2} issued successfully.";
            return RedirectToAction(nameof(Payments));
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsPending(int requestId, string? notes)
        {
            var request = await _context.LaundryRequests.FindAsync(requestId);
            if (request == null || request.IsPaid)
            {
                TempData["Error"] = "Request not found or already paid.";
                return RedirectToAction(nameof(Outstanding));
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var payment = new Payment
            {
                LaundryRequestId = requestId,
                CustomerId = request.CustomerId,
                CustomerName = request.CustomerName,
                Amount = request.TotalCost!.Value,
                Method = PaymentMethod.Cash,
                Status = PaymentStatus.Pending,
                TransactionId = $"PEND_{DateTime.UtcNow:yyyyMMdd}_{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                Notes = notes,
                ProcessedByUserId = userId
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Payment marked as pending.";
            return RedirectToAction(nameof(Outstanding));
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsFailed(int paymentId, string failureReason, string? notes)
        {
            var payment = await _context.Payments.FindAsync(paymentId);
            if (payment == null || payment.Status == PaymentStatus.Completed || payment.Status == PaymentStatus.Refunded)
            {
                TempData["Error"] = "Payment not found or cannot be marked as failed.";
                return RedirectToAction(nameof(Payments));
            }

            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = failureReason;
            if (!string.IsNullOrEmpty(notes))
            {
                payment.Notes = (payment.Notes ?? "") + $"\nFailed: {notes}";
            }

            await _context.SaveChangesAsync();

            TempData["Warning"] = "Payment marked as failed.";
            return RedirectToAction(nameof(Payments));
        }

        [HttpPost]
        public async Task<IActionResult> CancelPayment(int paymentId, string cancellationReason, string? notes)
        {
            var payment = await _context.Payments.FindAsync(paymentId);
            if (payment == null || payment.Status == PaymentStatus.Completed || payment.Status == PaymentStatus.Refunded)
            {
                TempData["Error"] = "Payment not found or cannot be cancelled.";
                return RedirectToAction(nameof(Payments));
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            payment.Status = PaymentStatus.Cancelled;
            payment.CancelledAt = DateTime.UtcNow;
            payment.CancelledByUserId = userId;
            payment.CancellationReason = cancellationReason;
            if (!string.IsNullOrEmpty(notes))
            {
                payment.Notes = (payment.Notes ?? "") + $"\nCancelled: {notes}";
            }

            await _context.SaveChangesAsync();

            TempData["Info"] = "Payment cancelled.";
            return RedirectToAction(nameof(Payments));
        }

        [HttpPost]
        public async Task<IActionResult> ConfirmPayment(int paymentId, string? notes)
        {
            var payment = await _context.Payments.Include(p => p.LaundryRequest).FirstOrDefaultAsync(p => p.Id == paymentId);
            if (payment == null || payment.Status != PaymentStatus.Pending)
            {
                TempData["Error"] = "Payment not found or not in pending status.";
                return RedirectToAction(nameof(Payments));
            }

            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            payment.Status = PaymentStatus.Completed;
            payment.ProcessedAt = DateTime.UtcNow;
            payment.ProcessedByUserId = userId;
            if (!string.IsNullOrEmpty(notes))
            {
                payment.Notes = (payment.Notes ?? "") + $"\nConfirmed: {notes}";
            }

            payment.LaundryRequest.IsPaid = true;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Payment confirmed successfully.";
            return RedirectToAction(nameof(Payments));
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

        public async Task<IActionResult> Adjustments(DateTime? from, DateTime? to, AdjustmentType? type)
        {
            // Set default date range (all time if not specified)
            var fromDate = from ?? DateTime.MinValue;
            var toDate = to ?? DateTime.MaxValue;

            var query = _context.PaymentAdjustments.AsQueryable();

            // Filter by date range
            if (from.HasValue || to.HasValue)
            {
                query = query.Where(a => a.EffectiveDate >= fromDate && a.EffectiveDate <= toDate.AddDays(1));
            }

            // Filter by adjustment type
            if (type.HasValue)
            {
                query = query.Where(a => a.Type == type.Value);
            }

            var adjustments = await query
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            ViewData["FromDate"] = from;
            ViewData["ToDate"] = to;
            ViewData["SelectedType"] = type;

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

        public async Task<IActionResult> SalesReport(DateTime? from, DateTime? to, string? period)
        {
            // Set default date range
            var today = DateTime.Today;
            var fromDate = from ?? new DateTime(today.Year, today.Month, 1); // Default: Start of month
            var toDate = to ?? today;

            // Apply quick period presets
            if (!string.IsNullOrEmpty(period))
            {
                switch (period.ToLower())
                {
                    case "today":
                        fromDate = today;
                        toDate = today;
                        break;
                    case "week":
                        fromDate = today.AddDays(-7);
                        toDate = today;
                        break;
                    case "month":
                        fromDate = new DateTime(today.Year, today.Month, 1);
                        toDate = today;
                        break;
                    case "year":
                        fromDate = new DateTime(today.Year, 1, 1);
                        toDate = today;
                        break;
                }
            }

            // Fetch payments for the period (by ProcessedAt - when payment was actually completed/refunded)
            var payments = await _context.Payments
                .Where(p => p.ProcessedAt != null && p.ProcessedAt >= fromDate && p.ProcessedAt <= toDate.AddDays(1))
                .OrderBy(p => p.ProcessedAt)
                .ToListAsync();

            var completedPayments = payments.Where(p => p.Status == PaymentStatus.Completed).ToList();
            var refundedPayments = payments.Where(p => p.Status == PaymentStatus.Refunded).ToList();

            // Calculate summary statistics - refunds subtract from revenue
            var completedRevenue = completedPayments.Sum(p => p.Amount);
            var refundedAmount = refundedPayments.Sum(p => p.RefundAmount ?? p.Amount);
            var totalRevenue = completedRevenue - refundedAmount;

            // All payments that count toward revenue (completed + refunded)
            var revenuePayments = completedPayments.Concat(refundedPayments).ToList();
            var totalTransactions = revenuePayments.Count;
            var avgTransactionValue = totalTransactions > 0 ? totalRevenue / totalTransactions : 0;

            // Payment status breakdown (removed Failed)
            var completedCount = payments.Count(p => p.Status == PaymentStatus.Completed);
            var pendingCount = payments.Count(p => p.Status == PaymentStatus.Pending);
            var refundedCount = payments.Count(p => p.Status == PaymentStatus.Refunded);

            // Top customers by revenue - account for refunds
            var topCustomers = revenuePayments
                .GroupBy(p => new { p.CustomerId, p.CustomerName })
                .Select(g => new CustomerRevenueDto
                {
                    CustomerId = g.Key.CustomerId,
                    CustomerName = g.Key.CustomerName,
                    TotalSpent = g.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount)
                               - g.Where(p => p.Status == PaymentStatus.Refunded).Sum(p => p.RefundAmount ?? p.Amount),
                    TransactionCount = g.Count()
                })
                .OrderByDescending(c => c.TotalSpent)
                .Take(10)
                .ToList();

            // Revenue by payment method - account for refunds
            var revenueByMethod = revenuePayments
                .GroupBy(p => p.Method.ToString())
                .ToDictionary(g => g.Key, g => g.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount)
                                                 - g.Where(p => p.Status == PaymentStatus.Refunded).Sum(p => p.RefundAmount ?? p.Amount));

            // Transactions by status (exclude Failed)
            var transactionsByStatus = payments
                .Where(p => p.Status != PaymentStatus.Failed)
                .GroupBy(p => p.Status.ToString())
                .ToDictionary(g => g.Key, g => g.Count());

            // Fetch adjustments for the period (by EffectiveDate)
            var adjustments = await _context.PaymentAdjustments
                .Where(a => a.EffectiveDate >= fromDate && a.EffectiveDate <= toDate.AddDays(1))
                .ToListAsync();

            // Recalculate hourly revenue to include adjustments (group by date + hour using ProcessedAt)
            var paymentHourlyRevenue = revenuePayments
                .Where(p => p.ProcessedAt.HasValue)
                .GroupBy(p => new DateTime(p.ProcessedAt!.Value.Year, p.ProcessedAt.Value.Month, p.ProcessedAt.Value.Day, p.ProcessedAt.Value.Hour, 0, 0))
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount)
                            - g.Where(p => p.Status == PaymentStatus.Refunded).Sum(p => p.RefundAmount ?? p.Amount),
                    TransactionCount = g.Count()
                })
                .ToList();

            // Group adjustments by effective date + hour
            var adjustmentHourlyRevenue = adjustments
                .GroupBy(a => new DateTime(a.EffectiveDate.Year, a.EffectiveDate.Month, a.EffectiveDate.Day, a.EffectiveDate.Hour, 0, 0))
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Where(a => a.Type == AdjustmentType.AddRevenue || a.Type == AdjustmentType.CompletePayment).Sum(a => a.Amount)
                            - g.Where(a => a.Type == AdjustmentType.SubtractRevenue).Sum(a => a.Amount),
                    TransactionCount = g.Count()
                })
                .ToList();

            // Combine payments and adjustments by date+hour
            var allDateHours = paymentHourlyRevenue.Select(p => p.Date)
                .Union(adjustmentHourlyRevenue.Select(a => a.Date))
                .Distinct()
                .OrderBy(d => d);

            var dailyRevenue = allDateHours.Select(dateHour => new DailyRevenueDto
            {
                Date = dateHour,
                Revenue = (paymentHourlyRevenue.FirstOrDefault(p => p.Date == dateHour)?.Revenue ?? 0)
                        + (adjustmentHourlyRevenue.FirstOrDefault(a => a.Date == dateHour)?.Revenue ?? 0),
                TransactionCount = (paymentHourlyRevenue.FirstOrDefault(p => p.Date == dateHour)?.TransactionCount ?? 0)
                                 + (adjustmentHourlyRevenue.FirstOrDefault(a => a.Date == dateHour)?.TransactionCount ?? 0)
            }).ToList();

            // Calculate adjustment impacts
            var addRevenueAmount = adjustments.Where(a => a.Type == AdjustmentType.AddRevenue).Sum(a => a.Amount);
            var subtractRevenueAmount = adjustments.Where(a => a.Type == AdjustmentType.SubtractRevenue).Sum(a => a.Amount);
            var completePaymentAmount = adjustments.Where(a => a.Type == AdjustmentType.CompletePayment).Sum(a => a.Amount);
            var supplyExpenseAmount = adjustments.Where(a => a.Type == AdjustmentType.SupplyExpense).Sum(a => a.Amount);

            // Calculate revenue components
            var paymentRevenue = totalRevenue; // This is already completedRevenue - refundedAmount
            var adjustmentRevenue = addRevenueAmount - subtractRevenueAmount + completePaymentAmount;
            var finalTotalRevenue = paymentRevenue + adjustmentRevenue;
            var netProfit = finalTotalRevenue - supplyExpenseAmount;

            var reportDto = new SalesReportDto
            {
                TotalRevenue = finalTotalRevenue,
                TotalTransactions = totalTransactions,
                AverageTransactionValue = avgTransactionValue,
                CompletedCount = completedCount,
                PendingCount = pendingCount,
                FailedCount = refundedCount, // Reusing FailedCount field for Refunded count

                // Revenue breakdown
                PaymentRevenue = paymentRevenue,
                AdjustmentRevenue = adjustmentRevenue,

                // Expenses & profit
                SupplyExpenses = supplyExpenseAmount,
                NetProfit = netProfit,

                // Adjustment breakdown
                AddRevenueCount = adjustments.Count(a => a.Type == AdjustmentType.AddRevenue),
                SubtractRevenueCount = adjustments.Count(a => a.Type == AdjustmentType.SubtractRevenue),
                CompletePaymentCount = adjustments.Count(a => a.Type == AdjustmentType.CompletePayment),
                SupplyExpenseCount = adjustments.Count(a => a.Type == AdjustmentType.SupplyExpense),

                AddRevenueAmount = addRevenueAmount,
                SubtractRevenueAmount = subtractRevenueAmount,
                CompletePaymentAmount = completePaymentAmount,
                SupplyExpenseAmount = supplyExpenseAmount,

                FromDate = fromDate,
                ToDate = toDate,
                PeriodLabel = $"{fromDate:MMM dd, yyyy} - {toDate:MMM dd, yyyy}",
                DailyRevenue = dailyRevenue,
                TopCustomers = topCustomers,
                RevenueByMethod = revenueByMethod,
                TransactionsByStatus = transactionsByStatus
            };

            ViewData["SalesReportData"] = reportDto;
            return View(reportDto);
        }

        public async Task<IActionResult> SalesReportPrint(DateTime? from, DateTime? to, string? period)
        {
            // Set default date range
            var today = DateTime.Today;
            var fromDate = from ?? new DateTime(today.Year, today.Month, 1);
            var toDate = to ?? today;

            // Apply quick period presets
            if (!string.IsNullOrEmpty(period))
            {
                switch (period.ToLower())
                {
                    case "today":
                        fromDate = today;
                        toDate = today;
                        break;
                    case "week":
                        fromDate = today.AddDays(-7);
                        toDate = today;
                        break;
                    case "month":
                        fromDate = new DateTime(today.Year, today.Month, 1);
                        toDate = today;
                        break;
                    case "year":
                        fromDate = new DateTime(today.Year, 1, 1);
                        toDate = today;
                        break;
                }
            }

            // Fetch payments for the period (by ProcessedAt - when payment was actually completed/refunded)
            var payments = await _context.Payments
                .Where(p => p.ProcessedAt != null && p.ProcessedAt >= fromDate && p.ProcessedAt <= toDate.AddDays(1))
                .OrderBy(p => p.ProcessedAt)
                .ToListAsync();

            var completedPayments = payments.Where(p => p.Status == PaymentStatus.Completed).ToList();
            var refundedPayments = payments.Where(p => p.Status == PaymentStatus.Refunded).ToList();

            // Calculate summary statistics - refunds subtract from revenue
            var completedRevenue = completedPayments.Sum(p => p.Amount);
            var refundedAmount = refundedPayments.Sum(p => p.RefundAmount ?? p.Amount);
            var totalRevenue = completedRevenue - refundedAmount;

            // All payments that count toward revenue (completed + refunded)
            var revenuePayments = completedPayments.Concat(refundedPayments).ToList();
            var totalTransactions = revenuePayments.Count;
            var avgTransactionValue = totalTransactions > 0 ? totalRevenue / totalTransactions : 0;

            // Payment status breakdown (removed Failed)
            var completedCount = payments.Count(p => p.Status == PaymentStatus.Completed);
            var pendingCount = payments.Count(p => p.Status == PaymentStatus.Pending);
            var refundedCount = payments.Count(p => p.Status == PaymentStatus.Refunded);

            // Top customers by revenue - account for refunds
            var topCustomers = revenuePayments
                .GroupBy(p => new { p.CustomerId, p.CustomerName })
                .Select(g => new CustomerRevenueDto
                {
                    CustomerId = g.Key.CustomerId,
                    CustomerName = g.Key.CustomerName,
                    TotalSpent = g.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount)
                               - g.Where(p => p.Status == PaymentStatus.Refunded).Sum(p => p.RefundAmount ?? p.Amount),
                    TransactionCount = g.Count()
                })
                .OrderByDescending(c => c.TotalSpent)
                .Take(10)
                .ToList();

            // Fetch adjustments for the period (by EffectiveDate)
            var adjustments = await _context.PaymentAdjustments
                .Where(a => a.EffectiveDate >= fromDate && a.EffectiveDate <= toDate.AddDays(1))
                .ToListAsync();

            // Recalculate hourly revenue to include adjustments (group by date + hour using ProcessedAt)
            var paymentHourlyRevenue = revenuePayments
                .Where(p => p.ProcessedAt.HasValue)
                .GroupBy(p => new DateTime(p.ProcessedAt!.Value.Year, p.ProcessedAt.Value.Month, p.ProcessedAt.Value.Day, p.ProcessedAt.Value.Hour, 0, 0))
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount)
                            - g.Where(p => p.Status == PaymentStatus.Refunded).Sum(p => p.RefundAmount ?? p.Amount),
                    TransactionCount = g.Count()
                })
                .ToList();

            // Group adjustments by effective date + hour
            var adjustmentHourlyRevenue = adjustments
                .GroupBy(a => new DateTime(a.EffectiveDate.Year, a.EffectiveDate.Month, a.EffectiveDate.Day, a.EffectiveDate.Hour, 0, 0))
                .Select(g => new
                {
                    Date = g.Key,
                    Revenue = g.Where(a => a.Type == AdjustmentType.AddRevenue || a.Type == AdjustmentType.CompletePayment).Sum(a => a.Amount)
                            - g.Where(a => a.Type == AdjustmentType.SubtractRevenue).Sum(a => a.Amount),
                    TransactionCount = g.Count()
                })
                .ToList();

            // Combine payments and adjustments by date+hour
            var allDateHours = paymentHourlyRevenue.Select(p => p.Date)
                .Union(adjustmentHourlyRevenue.Select(a => a.Date))
                .Distinct()
                .OrderBy(d => d);

            var dailyRevenue = allDateHours.Select(dateHour => new DailyRevenueDto
            {
                Date = dateHour,
                Revenue = (paymentHourlyRevenue.FirstOrDefault(p => p.Date == dateHour)?.Revenue ?? 0)
                        + (adjustmentHourlyRevenue.FirstOrDefault(a => a.Date == dateHour)?.Revenue ?? 0),
                TransactionCount = (paymentHourlyRevenue.FirstOrDefault(p => p.Date == dateHour)?.TransactionCount ?? 0)
                                 + (adjustmentHourlyRevenue.FirstOrDefault(a => a.Date == dateHour)?.TransactionCount ?? 0)
            }).ToList();

            // Calculate adjustment impacts
            var addRevenueAmount = adjustments.Where(a => a.Type == AdjustmentType.AddRevenue).Sum(a => a.Amount);
            var subtractRevenueAmount = adjustments.Where(a => a.Type == AdjustmentType.SubtractRevenue).Sum(a => a.Amount);
            var completePaymentAmount = adjustments.Where(a => a.Type == AdjustmentType.CompletePayment).Sum(a => a.Amount);
            var supplyExpenseAmount = adjustments.Where(a => a.Type == AdjustmentType.SupplyExpense).Sum(a => a.Amount);

            // Calculate revenue components
            var paymentRevenue = totalRevenue; // This is already completedRevenue - refundedAmount
            var adjustmentRevenue = addRevenueAmount - subtractRevenueAmount + completePaymentAmount;
            var finalTotalRevenue = paymentRevenue + adjustmentRevenue;
            var netProfit = finalTotalRevenue - supplyExpenseAmount;

            var reportDto = new SalesReportDto
            {
                TotalRevenue = finalTotalRevenue,
                TotalTransactions = totalTransactions,
                AverageTransactionValue = avgTransactionValue,
                CompletedCount = completedCount,
                PendingCount = pendingCount,
                FailedCount = refundedCount, // Reusing FailedCount field for Refunded count

                // Revenue breakdown
                PaymentRevenue = paymentRevenue,
                AdjustmentRevenue = adjustmentRevenue,

                // Expenses & profit
                SupplyExpenses = supplyExpenseAmount,
                NetProfit = netProfit,

                // Adjustment breakdown
                AddRevenueCount = adjustments.Count(a => a.Type == AdjustmentType.AddRevenue),
                SubtractRevenueCount = adjustments.Count(a => a.Type == AdjustmentType.SubtractRevenue),
                CompletePaymentCount = adjustments.Count(a => a.Type == AdjustmentType.CompletePayment),
                SupplyExpenseCount = adjustments.Count(a => a.Type == AdjustmentType.SupplyExpense),

                AddRevenueAmount = addRevenueAmount,
                SubtractRevenueAmount = subtractRevenueAmount,
                CompletePaymentAmount = completePaymentAmount,
                SupplyExpenseAmount = supplyExpenseAmount,

                FromDate = fromDate,
                ToDate = toDate,
                PeriodLabel = $"{fromDate:MMM dd, yyyy} - {toDate:MMM dd, yyyy}",
                DailyRevenue = dailyRevenue,
                TopCustomers = topCustomers,
                RevenueByMethod = new Dictionary<string, decimal>(),
                TransactionsByStatus = new Dictionary<string, int>()
            };

            return View(reportDto);
        }

        public async Task<IActionResult> ExportSalesReportExcel(DateTime? from, DateTime? to)
        {
            // Set default date range
            var today = DateTime.Today;
            var fromDate = from ?? new DateTime(today.Year, today.Month, 1);
            var toDate = to ?? today;

            // Fetch payments for the period
            var payments = await _context.Payments
                .Where(p => p.CreatedAt >= fromDate && p.CreatedAt <= toDate.AddDays(1))
                .OrderBy(p => p.CreatedAt)
                .ToListAsync();

            // Create CSV content
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Sales Report");
            csv.AppendLine($"Period: {fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}");
            csv.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            csv.AppendLine();
            csv.AppendLine("Transaction ID,Customer Name,Customer ID,Amount,Status,Payment Method,Date");

            foreach (var payment in payments)
            {
                csv.AppendLine($"\"{payment.TransactionId}\",\"{payment.CustomerName}\",\"{payment.CustomerId}\",{payment.Amount},\"{payment.Status}\",\"{payment.Method}\",\"{payment.CreatedAt:yyyy-MM-dd HH:mm}\"");
            }

            csv.AppendLine();
            csv.AppendLine("Summary");
            var totalRevenue = payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount);
            var totalTransactions = payments.Count;
            var completedCount = payments.Count(p => p.Status == PaymentStatus.Completed);
            var pendingCount = payments.Count(p => p.Status == PaymentStatus.Pending);
            var failedCount = payments.Count(p => p.Status == PaymentStatus.Failed);

            csv.AppendLine($"Total Revenue,{totalRevenue}");
            csv.AppendLine($"Total Transactions,{totalTransactions}");
            csv.AppendLine($"Completed,{completedCount}");
            csv.AppendLine($"Pending,{pendingCount}");
            csv.AppendLine($"Failed,{failedCount}");

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            var fileName = $"SalesReport_{fromDate:yyyyMMdd}_{toDate:yyyyMMdd}.csv";

            return File(bytes, "text/csv", fileName);
        }

        public async Task<IActionResult> ExportSalesReportPdf(DateTime? from, DateTime? to)
        {
            // PDF export uses browser's print-to-PDF functionality
            // Just redirect back to show the print-friendly view
            return RedirectToAction(nameof(SalesReport), new { from, to });
        }

        // ==================== RECEIPT MANAGEMENT ====================

        [HttpGet]
        public async Task<IActionResult> ViewReceipt(int id)
        {
            var receipt = await _context.Receipts
                .Include(r => r.Payment)
                    .ThenInclude(p => p.LaundryRequest)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (receipt == null)
            {
                return NotFound("Receipt not found");
            }

            var settings = await _context.LaundrySettings.FirstOrDefaultAsync();

            ViewData["Receipt"] = receipt;
            ViewData["Settings"] = settings;

            return View(receipt);
        }

        [HttpGet]
        public async Task<IActionResult> ViewReceiptPrint(int id)
        {
            var receipt = await _context.Receipts
                .Include(r => r.Payment)
                    .ThenInclude(p => p.LaundryRequest)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (receipt == null)
            {
                return NotFound("Receipt not found");
            }

            var settings = await _context.LaundrySettings.FirstOrDefaultAsync();

            ViewData["Settings"] = settings;

            return View(receipt);
        }

        [HttpPost]
        public async Task<IActionResult> ResendReceipt(int receiptId)
        {
            var receipt = await _context.Receipts
                .Include(r => r.Payment)
                .FirstOrDefaultAsync(r => r.Id == receiptId);

            if (receipt == null)
            {
                TempData["Error"] = "Receipt not found.";
                return RedirectToAction(nameof(Payments));
            }

            try
            {
                await SendReceiptNotificationAsync(receiptId, receipt.Payment.CustomerId, isResend: true);
                TempData["Success"] = $"Receipt #{receipt.ReceiptNumber} resent to customer.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to resend receipt: {ex.Message}";
            }

            return RedirectToAction(nameof(Payments));
        }

        // ==================== HELPER METHODS ====================

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

        private async Task SendReceiptNotificationAsync(int receiptId, string customerId, bool isResend = false)
        {
            var receipt = await _context.Receipts
                .Include(r => r.Payment)
                .FirstOrDefaultAsync(r => r.Id == receiptId);
            if (receipt == null) return;

            var receiptUrl = $"{Request.Scheme}://{Request.Host}/Accounting/ViewReceipt/{receiptId}";

            var message = new Message
            {
                SenderId = "System",
                SenderName = "System",
                SenderType = "Admin",
                CustomerId = customerId,
                CustomerName = receipt.Payment.CustomerName,
                Content = isResend
                    ? $"PAYMENT RECEIPT (RESENT)\n\nYour payment receipt #{receipt.ReceiptNumber} has been resent.\n\nView it here: {receiptUrl}"
                    : $"PAYMENT RECEIPT\n\nYour payment has been received! Receipt #{receipt.ReceiptNumber} is ready.\n\nView it here: {receiptUrl}",
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
}