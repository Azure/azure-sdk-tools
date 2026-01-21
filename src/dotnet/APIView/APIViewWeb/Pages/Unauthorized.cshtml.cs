using System;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace APIViewWeb.Pages
{
    public class UnauthorizedModel : PageModel
    {
        private readonly IAuthorizationService _authorizationService;
        private readonly IWebHostEnvironment _environment;
        public OrganizationOptions Options { get; }

        [BindProperty(SupportsGet = true, Name = "returnurl")]
        public string ReturnUrl { get; set; } = "/";

        public UnauthorizedModel(IOptions<OrganizationOptions> options, IAuthorizationService authorizationService, IWebHostEnvironment environment)
        {
            _authorizationService = authorizationService;
            _environment = environment;
            Options = options.Value;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var authorizationResult =
                await _authorizationService.AuthorizeAsync(User, null, Startup.RequireOrganizationPolicy);

            if (authorizationResult.Succeeded)
            {
                // Validate return URL to prevent open redirect vulnerability
                if (!Url.IsLocalUrl(ReturnUrl))
                {
                    string[] origins = (_environment.IsDevelopment()) ? URlHelpers.GetAllowedStagingOrigins() : URlHelpers.GetAllowedProdOrigins();
                    
                    if (!Uri.TryCreate(ReturnUrl, UriKind.Absolute, out Uri returnUri) || 
                        !origins.Contains(returnUri.GetLeftPart(UriPartial.Authority)))
                    {
                        ReturnUrl = "/";
                    }
                }
                return Redirect(ReturnUrl);
            }

            return Page();
        }
    }
}