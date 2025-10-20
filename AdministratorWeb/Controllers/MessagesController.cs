using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdministratorWeb.Data;
using AdministratorWeb.Models;

namespace AdministratorWeb.Controllers
{
    /// <summary>
    /// Admin web dashboard controller for customer messaging
    /// Allows admins to view and respond to customer messages
    /// </summary>
    [Authorize]
    public class MessagesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MessagesController> _logger;
        private readonly IWebHostEnvironment _env;
        private readonly UserManager<ApplicationUser> _userManager;

        public MessagesController(
            ApplicationDbContext context,
            ILogger<MessagesController> logger,
            IWebHostEnvironment env,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _logger = logger;
            _env = env;
            _userManager = userManager;
        }

        /// <summary>
        /// Main messages dashboard - shows list of conversations
        /// </summary>
        public async Task<IActionResult> Index()
        {
            // Get all customers who have sent messages, with their latest message and unread count
            var conversations = await _context.Messages
                .GroupBy(m => m.CustomerId)
                .Select(g => new
                {
                    CustomerId = g.Key,
                    CustomerName = g.OrderByDescending(m => m.SentAt).First().CustomerName,
                    LastMessage = g.OrderByDescending(m => m.SentAt).First().Content,
                    LastMessageTime = g.OrderByDescending(m => m.SentAt).First().SentAt,
                    LastSenderType = g.OrderByDescending(m => m.SentAt).First().SenderType,
                    UnreadCount = g.Count(m => !m.IsRead && m.SenderType == "Customer"),
                    TotalMessages = g.Count()
                })
                .OrderByDescending(c => c.LastMessageTime)
                .ToListAsync();

            return View(conversations);
        }

        /// <summary>
        /// Show all users to select for messaging
        /// Uses the EXACT same logic as Users/Index - just shows ALL users
        /// </summary>
        public async Task<IActionResult> SelectUser()
        {
            // EXACT SAME AS USERS CONTROLLER - Get ALL users, no filtering
            var allUsers = await _userManager.Users.ToListAsync();
            var userViewModels = new List<object>();

            _logger.LogInformation("Total users in database: {Count}", allUsers.Count);

            // Get roles for each user (same as Users controller)
            foreach (var user in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userViewModels.Add(new
                {
                    User = user,
                    Roles = roles
                });
            }

            _logger.LogInformation("Total users loaded: {Count}", userViewModels.Count);

            return View(userViewModels);
        }

        /// <summary>
        /// View conversation with a specific customer
        /// </summary>
        public async Task<IActionResult> Conversation(string customerId)
        {
            if (string.IsNullOrEmpty(customerId))
            {
                return BadRequest("Customer ID is required");
            }

            // Get customer info
            var customer = await _context.Users.FindAsync(customerId);
            if (customer == null)
            {
                return NotFound("Customer not found");
            }

            ViewBag.CustomerId = customerId;
            ViewBag.CustomerName = $"{customer.FirstName} {customer.LastName}";

            // Get all messages for this customer
            var messages = await _context.Messages
                .Where(m => m.CustomerId == customerId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            // Mark all admin messages from this customer as read
            var unreadMessages = messages.Where(m => !m.IsRead && m.SenderType == "Customer").ToList();
            foreach (var msg in unreadMessages)
            {
                msg.IsRead = true;
                msg.ReadAt = DateTime.UtcNow;
            }
            if (unreadMessages.Any())
            {
                await _context.SaveChangesAsync();
            }

            return View(messages);
        }

        /// <summary>
        /// Send a message from admin to customer (AJAX)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SendMessage(string customerId, string content, IFormFile? image = null)
        {
            _logger.LogInformation("SendMessage called - CustomerId: {CustomerId}, Content: {Content}, HasImage: {HasImage}",
                customerId, content?.Length ?? 0, image != null);

            if (string.IsNullOrEmpty(customerId))
            {
                _logger.LogWarning("SendMessage failed - Customer ID is empty");
                return Json(new { success = false, error = "Customer ID is required" });
            }

            if (string.IsNullOrWhiteSpace(content) && image == null)
            {
                return Json(new { success = false, error = "Message content or image is required" });
            }

            // Get customer info
            var customer = await _context.Users.FindAsync(customerId);
            if (customer == null)
            {
                return Json(new { success = false, error = "Customer not found" });
            }

            // Get admin info
            var adminId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var adminUser = await _context.Users.FindAsync(adminId);
            var adminName = adminUser != null ? $"{adminUser.FirstName} {adminUser.LastName}" : "Admin";

            string? imageUrl = null;

            // Handle image upload
            if (image != null)
            {
                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var extension = Path.GetExtension(image.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(extension))
                {
                    return Json(new { success = false, error = "Only image files (JPG, PNG, GIF) are allowed" });
                }

                // Validate file size (max 5MB)
                if (image.Length > 5 * 1024 * 1024)
                {
                    return Json(new { success = false, error = "Image file size must be less than 5MB" });
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
                CustomerName = $"{customer.FirstName} {customer.LastName}",
                SenderId = adminId ?? "admin",
                SenderName = adminName,
                SenderType = "Admin",
                Content = content ?? string.Empty,
                ImageUrl = imageUrl,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Admin {AdminName} sent message to customer {CustomerId}", adminName, customerId);

            return Json(new
            {
                success = true,
                message = new
                {
                    message.Id,
                    message.SenderId,
                    message.SenderName,
                    message.SenderType,
                    message.Content,
                    sentAt = message.SentAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    message.ImageUrl
                }
            });
        }

        /// <summary>
        /// Get new messages for a conversation (AJAX polling)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetNewMessages(string customerId, int lastMessageId)
        {
            if (string.IsNullOrEmpty(customerId))
            {
                return Json(new { success = false, error = "Customer ID is required" });
            }

            var newMessages = await _context.Messages
                .Where(m => m.CustomerId == customerId && m.Id > lastMessageId)
                .OrderBy(m => m.SentAt)
                .Select(m => new
                {
                    m.Id,
                    m.SenderId,
                    m.SenderName,
                    m.SenderType,
                    m.Content,
                    sentAt = m.SentAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    m.ImageUrl
                })
                .ToListAsync();

            // Mark customer messages as read
            var unreadMessages = await _context.Messages
                .Where(m => m.CustomerId == customerId && m.Id > lastMessageId && m.SenderType == "Customer" && !m.IsRead)
                .ToListAsync();

            foreach (var msg in unreadMessages)
            {
                msg.IsRead = true;
                msg.ReadAt = DateTime.UtcNow;
            }
            if (unreadMessages.Any())
            {
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true, messages = newMessages });
        }

        /// <summary>
        /// Get total unread message count (for navbar badge)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var count = await _context.Messages
                .Where(m => !m.IsRead && m.SenderType == "Customer")
                .CountAsync();

            return Json(new { unreadCount = count });
        }

        /// <summary>
        /// Delete a message
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> DeleteMessage(int messageId)
        {
            var message = await _context.Messages.FindAsync(messageId);
            if (message == null)
            {
                return Json(new { success = false, error = "Message not found" });
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

            return Json(new { success = true });
        }
    }
}
