using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace APIViewWeb.Pages
{
    public class UnauthorizedModel : PageModel
    {
        public OrganizationOptions Options { get; }

        [BindProperty(SupportsGet = true)]
        public string ReturnUrl { get; private set; }

        public UnauthorizedModel(IOptions<OrganizationOptions> options)
        {
            Options = options.Value;
        }

        public IActionResult OnGet()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToPage("./Assemblies/Index");
            }
            ReturnUrl = Request.Query["returnurl"];
            return Page();
        }
    }
}