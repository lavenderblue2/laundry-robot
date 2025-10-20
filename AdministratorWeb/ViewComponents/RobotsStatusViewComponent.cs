using Microsoft.AspNetCore.Mvc;
using AdministratorWeb.Services;

namespace AdministratorWeb.ViewComponents
{
    public class RobotsStatusViewComponent : ViewComponent
    {
        private readonly IRobotManagementService _robotService;

        public RobotsStatusViewComponent(IRobotManagementService robotService)
        {
            _robotService = robotService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            try
            {
                var robots = await _robotService.GetAllRobotsAsync();
                var onlineCount = robots.Count(r => !r.IsOffline && r.IsActive);
                return Content($"{onlineCount} online");
            }
            catch
            {
                return Content("0 online");
            }
        }
    }
}