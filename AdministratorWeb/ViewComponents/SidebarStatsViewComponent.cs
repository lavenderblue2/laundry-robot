using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdministratorWeb.Data;
using AdministratorWeb.Services;

namespace AdministratorWeb.ViewComponents
{
    public class SidebarStatsViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        private readonly IRobotManagementService _robotService;

        public SidebarStatsViewComponent(ApplicationDbContext context, IRobotManagementService robotService)
        {
            _context = context;
            _robotService = robotService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var model = new SidebarStatsViewModel
            {
                TotalRequests = await _context.LaundryRequests.CountAsync(),
                OnlineRobots = await GetOnlineRobotsCountAsync(),
                TotalRobots = await GetTotalRobotsCountAsync()
            };

            return View(model);
        }

        private async Task<int> GetOnlineRobotsCountAsync()
        {
            try
            {
                var robots = await _robotService.GetAllRobotsAsync();
                return robots.Count(r => !r.IsOffline && r.IsActive);
            }
            catch
            {
                return 0;
            }
        }

        private async Task<int> GetTotalRobotsCountAsync()
        {
            try
            {
                var robots = await _robotService.GetAllRobotsAsync();
                return robots.Count;
            }
            catch
            {
                return 0;
            }
        }
    }

    public class SidebarStatsViewModel
    {
        public int TotalRequests { get; set; }
        public int OnlineRobots { get; set; }
        public int TotalRobots { get; set; }
    }
}