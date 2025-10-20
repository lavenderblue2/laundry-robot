using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdministratorWeb.Data;
using AdministratorWeb.Models;

namespace AdministratorWeb.ViewComponents
{
    public class CompanyInfoViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public CompanyInfoViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var settings = await _context.LaundrySettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new LaundrySettings
                {
                    CompanyName = "Jopart Laundry",
                    CompanyDescription = "Advanced robotic room tracking and navigation system"
                };
            }

            return View(settings);
        }
    }
}
