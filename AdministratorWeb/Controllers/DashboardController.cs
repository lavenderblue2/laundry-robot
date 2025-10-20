using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdministratorWeb.Data;
using AdministratorWeb.Models;
using AdministratorWeb.Models.DTOs;
using AdministratorWeb.Services;

namespace AdministratorWeb.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IRobotManagementService _robotService;

        public DashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IRobotManagementService robotService)
        {
            _context = context;
            _userManager = userManager;
            _robotService = robotService;
        }

        public async Task<IActionResult> Index()
        {
            var pendingRequests = await _context.LaundryRequests.CountAsync(r => r.Status == RequestStatus.Pending);
            var robots = await _robotService.GetAllRobotsAsync();
            var activeRobots = robots.Count(r => r.IsActive && r.CanAcceptRequests && !r.IsOffline);
            var todayRequests = await _context.LaundryRequests.CountAsync(r => r.RequestedAt.Date == DateTime.Today);
            var totalRevenue = await _context.LaundryRequests
                .Where(r => r.Status == RequestStatus.Completed && r.TotalCost.HasValue)
                .SumAsync(r => r.TotalCost!.Value);

            var dashboardDto = new DashboardIndexDto
            {
                PendingRequests = pendingRequests,
                ActiveRobots = activeRobots,
                TodayRequests = todayRequests,
                TotalRevenue = totalRevenue,
                Users = new List<object>()
            };
            ViewData["DashboardData"] = dashboardDto;

            var recentRequests = await _context.LaundryRequests
                .OrderByDescending(r => r.RequestedAt)
                .Take(5)
                .ToListAsync();

            return View(recentRequests);
        }

        public async Task<IActionResult> Requests()
        {
            var requests = await _context.LaundryRequests
                .Include(r => r.HandledBy)
                .OrderByDescending(r => r.RequestedAt)
                .ToListAsync();

            return View(requests);
        }

        [HttpPost]
        public async Task<IActionResult> AcceptRequest(int id)
        {
            var request = await _context.LaundryRequests.FindAsync(id);
            if (request == null || request.Status != RequestStatus.Pending)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            request.Status = RequestStatus.Accepted;
            request.HandledById = user?.Id;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Requests));
        }

        [HttpPost]
        public async Task<IActionResult> DeclineRequest(int id, string reason)
        {
            var request = await _context.LaundryRequests.FindAsync(id);
            if (request == null || request.Status != RequestStatus.Pending)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            request.Status = RequestStatus.Declined;
            request.HandledById = user?.Id;
            request.DeclineReason = reason;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Requests));
        }

        [HttpPost]
        public async Task<IActionResult> AssignRobot(int requestId, string robotName)
        {
            var request = await _context.LaundryRequests.FindAsync(requestId);
            var robot = await _robotService.GetRobotAsync(robotName);

            if (request == null || robot == null || !robot.CanAcceptRequests || robot.IsOffline)
            {
                return BadRequest();
            }

            request.AssignedRobotName = robotName;
            request.Status = RequestStatus.InProgress;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Requests));
        }

        public async Task<IActionResult> Robots()
        {
            var robots = await _robotService.GetAllRobotsAsync();
            return View(robots);
        }

        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users.ToListAsync();
            var userViewModels = new List<object>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userViewModels.Add(new
                {
                    User = user,
                    Roles = roles
                });
            }

            ViewData["Users"] = userViewModels;
            return View();
        }

        public async Task<IActionResult> Settings()
        {
            var settings = await _context.LaundrySettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new LaundrySettings();
                _context.LaundrySettings.Add(settings);
                await _context.SaveChangesAsync();
            }

            // Get first available robot for camera feed
            var robots = await _robotService.GetAllRobotsAsync();
            var firstRobot = robots.FirstOrDefault();
            ViewData["RobotName"] = firstRobot?.Name;

            return View(settings);
        }

        [HttpPost]
        public async Task<IActionResult> Settings(LaundrySettings model)
        {
            if (ModelState.IsValid)
            {
                var settings = await _context.LaundrySettings.FirstOrDefaultAsync();
                if (settings == null)
                {
                    _context.LaundrySettings.Add(model);
                }
                else
                {
                    settings.RatePerKg = model.RatePerKg;
                    settings.CompanyName = model.CompanyName;
                    settings.CompanyAddress = model.CompanyAddress;
                    settings.CompanyPhone = model.CompanyPhone;
                    settings.CompanyEmail = model.CompanyEmail;
                    settings.CompanyWebsite = model.CompanyWebsite;
                    settings.CompanyDescription = model.CompanyDescription;
                    settings.FacebookUrl = model.FacebookUrl;
                    settings.TwitterUrl = model.TwitterUrl;
                    settings.InstagramUrl = model.InstagramUrl;
                    settings.OperatingHours = model.OperatingHours;
                    settings.MaxWeightPerRequest = model.MaxWeightPerRequest;
                    settings.MinWeightPerRequest = model.MinWeightPerRequest;
                    settings.AutoAcceptRequests = model.AutoAcceptRequests;
                    settings.DetectionMode = model.DetectionMode;
                    settings.LineFollowColorR = model.LineFollowColorR;
                    settings.LineFollowColorG = model.LineFollowColorG;
                    settings.LineFollowColorB = model.LineFollowColorB;
                    settings.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "Settings updated successfully.";
            }

            return View(model);
        }
    }
}