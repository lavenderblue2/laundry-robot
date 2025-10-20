using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AdministratorWeb.Data;

namespace AdministratorWeb.ViewComponents
{
    public class RequestsCountViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public RequestsCountViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            try
            {
                var count = await _context.LaundryRequests.CountAsync();
                return Content(count.ToString());
            }
            catch
            {
                return Content("0");
            }
        }
    }
}