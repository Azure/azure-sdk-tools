using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace APIViewWeb.Pages
{
    public class UnauthorizedModel : PageModel
    {
        private readonly IAuthorizationService _authorizationService;
        public OrganizationOptions Options { get; }

        [BindProperty(SupportsGet = true, Name = "returnurl")]
        public string ReturnUrl { get; set; } = "/";

        public UnauthorizedModel(IOptions<OrganizationOptions> options, IAuthorizationService authorizationService)
        {
            _authorizationService = authorizationService;
            Options = options.Value;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var authorizationResult =
                await _authorizationService.AuthorizeAsync(User, null, Startup.RequireOrganizationPolicy);

            if (authorizationResult.Succeeded)
                return Redirect(ReturnUrl);

            return Page();
        }
    }
}