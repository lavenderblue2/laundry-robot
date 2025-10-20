using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdministratorWeb.Data;
using AdministratorWeb.Models;

namespace AdministratorWeb.Controllers.Api
{
    /// <summary>
    /// API Controller for customer messaging (Mobile App)
    /// Allows customers to send and receive messages to/from admin
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "ApiPolicy")]
    public class MessagesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MessagesController> _logger;
        private readonly IWebHostEnvironment _env;

        public MessagesController(
            ApplicationDbContext context,
            ILogger<MessagesController> logger,
            IWebHostEnvironment env)
        {
            _context = context;
            _logger = logger;
            _env = env;
        }

        /// <summary>
        /// Get all messages for the authenticated customer
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMessages([FromQuery] int? lastMessageId = null, [FromQuery] int limit = 50)
        {
            var customerId = User.FindFirst("CustomerId")?.Value;
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized("Customer ID not found in token");
            }

            var query = _context.Messages
                .Where(m => m.CustomerId == customerId);

            // Pagination support - get messages after lastMessageId
            if (lastMessageId.HasValue)
            {
                query = query.Where(m => m.Id > lastMessageId.Value);
            }

            var messages = await query
                .OrderBy(m => m.SentAt)
                .Take(limit)
                .Select(m => new
                {
                    m.Id,
                    m.SenderId,
                    m.SenderName,
                    m.SenderType,
                    m.Content,
                    m.SentAt,
                    m.IsRead,
                    m.ReadAt,
                    m.ImageUrl,
                    m.RequestId
                })
                .ToListAsync();

            return Ok(messages);
        }

        /// <summary>
        /// Get unread message count for the authenticated customer
        /// </summary>
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var customerId = User.FindFirst("CustomerId")?.Value;
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized("Customer ID not found in token");
            }

            var count = await _context.Messages
                .Where(m => m.CustomerId == customerId && !m.IsRead && m.SenderType == "Admin")
                .CountAsync();

            return Ok(new { unreadCount = count });
        }

        /// <summary>
        /// Send a message from customer to admin
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SendMessage([FromForm] string? content, [FromForm] IFormFile? image = null, [FromForm] int? requestId = null)
        {
            var customerId = User.FindFirst("CustomerId")?.Value;
            var customerName = User.FindFirst("CustomerName")?.Value;

            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized("Customer ID not found in token");
            }

            if (string.IsNullOrWhiteSpace(content) && image == null)
            {
                return BadRequest("Message content or image is required");
            }

            string? imageUrl = null;

            // Handle image upload
            if (image != null)
            {
                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var extension = Path.GetExtension(image.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    return BadRequest("Only image files (JPG, PNG, GIF) are allowed");
                }

                // Validate file size (max 5MB)
                if (image.Length > 5 * 1024 * 1024)
                {
                    return BadRequest("Image file size must be less than 5MB");
                }

                // Create uploads directory if it doesn't exist
                var uploadsPath = Path.Combine(_env.WebRootPath, "uploads", "messages");
                Directory.CreateDirectory(uploadsPath);

                // Generate unique filename
                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadsPath, fileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await image.CopyToAsync(stream);
                }

                imageUrl = $"/uploads/messages/{fileName}";
            }

            var message = new Message
            {
                CustomerId = customerId,
                CustomerName = customerName ?? "Unknown",
                SenderId = customerId,
                SenderName = customerName ?? "Unknown",
                SenderType = "Customer",
                Content = content ?? string.Empty,
                ImageUrl = imageUrl,
                RequestId = requestId,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Customer {CustomerId} sent message #{MessageId}", customerId, message.Id);

            return Ok(new
            {
                message.Id,
                message.SenderId,
                message.SenderName,
                message.SenderType,
                message.Content,
                message.SentAt,
                message.IsRead,
                message.ImageUrl,
                message.RequestId
            });
        }

        /// <summary>
        /// Mark messages as read
        /// </summary>
        [HttpPost("mark-read")]
        public async Task<IActionResult> MarkAsRead([FromBody] List<int> messageIds)
        {
            var customerId = User.FindFirst("CustomerId")?.Value;
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized("Customer ID not found in token");
            }

            var messages = await _context.Messages
                .Where(m => messageIds.Contains(m.Id) && m.CustomerId == customerId && m.SenderType == "Admin")
                .ToListAsync();

            foreach (var message in messages)
            {
                message.IsRead = true;
                message.ReadAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new { markedCount = messages.Count });
        }

        /// <summary>
        /// Delete a message (customer can only delete their own messages)
        /// </summary>
        [HttpDelete("{messageId}")]
        public async Task<IActionResult> DeleteMessage(int messageId)
        {
            var customerId = User.FindFirst("CustomerId")?.Value;
            if (string.IsNullOrEmpty(customerId))
            {
                return Unauthorized("Customer ID not found in token");
            }

            var message = await _context.Messages
                .FirstOrDefaultAsync(m => m.Id == messageId && m.CustomerId == customerId && m.SenderId == customerId);

            if (message == null)
            {
                return NotFound("Message not found or you don't have permission to delete it");
            }

            // Delete image file if exists
            if (!string.IsNullOrEmpty(message.ImageUrl))
            {
                var imagePath = Path.Combine(_env.WebRootPath, message.ImageUrl.TrimStart('/'));
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Delete(imagePath);
                }
            }

            _context.Messages.Remove(message);
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }
    }
}
