using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace APIViewWeb.Pages
{
    public class LoginModel : PageModel
    {
        [BindProperty(SupportsGet = true, Name = "returnurl")]
        public string ReturnUrl { get; set; } = "/";

        public IActionResult OnGetAsync()
        {
            if (User.Identity.IsAuthenticated)
                return Redirect(ReturnUrl);

            return Page();
        }
    }
}