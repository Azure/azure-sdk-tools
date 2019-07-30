using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages
{
    public class UnauthorizedModel : PageModel
    {
        public IActionResult OnGet()
        {
            if (User.Identity.IsAuthenticated)
                return RedirectToPage("./Assemblies/Index");
            return Page();
        }
    }
}