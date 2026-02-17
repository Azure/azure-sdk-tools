using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System;
using APIViewWeb.Helpers;
using APIViewWeb.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace APIViewWeb.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IOAuthEnvironmentService _oauthEnvService;
        private readonly IAuthTokenService _authTokenService;

        public AccountController(
            IWebHostEnvironment env,
            IOAuthEnvironmentService oauthEnvService,
            IAuthTokenService authTokenService)
        {
            _environment = env;
            _oauthEnvService = oauthEnvService;
            _authTokenService = authTokenService;
        }

        [HttpGet]
        public async Task<IActionResult> Login(string returnUrl = "/", string returnHost = null)
        {
            await HttpContext.SignOutAsync();

            var currentHost = Request.Host.Value;

            // If we're on a vanity domain, redirect to canonical host to perform OAuth
            if (_oauthEnvService.IsVanityDomain(currentHost))
            {
                var canonicalHost = _oauthEnvService.GetCanonicalHost();
                var scheme = Request.Scheme;
                var redirectUrl = $"{scheme}://{canonicalHost}/Account/Login?returnHost={Uri.EscapeDataString(currentHost)}&returnUrl={Uri.EscapeDataString(returnUrl)}";
                return Redirect(redirectUrl);
            }

            // Validate returnUrl for security
            if (!Url.IsLocalUrl(returnUrl))
            {
                string[] origins = (_environment.IsDevelopment()) 
                    ? URlHelpers.GetAllowedStagingOrigins() 
                    : URlHelpers.GetAllowedProdOrigins();
                
                if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var returnUri))
                {
                    if (!origins.Contains(returnUri.GetLeftPart(UriPartial.Authority)))
                    {
                        returnUrl = "/";
                    }
                }
                else
                {
                    returnUrl = "/";
                }
            }

            // Store returnHost in authentication properties if we need to redirect back after OAuth
            var authProps = new AuthenticationProperties();
            
            if (!string.IsNullOrEmpty(returnHost) && _oauthEnvService.IsVanityDomain(returnHost))
            {
                // We'll redirect back to the vanity domain after successful OAuth
                // Use a special internal redirect URI that our OAuth handler will intercept
                authProps.RedirectUri = $"/Account/CompleteLogin?returnHost={Uri.EscapeDataString(returnHost)}&returnUrl={Uri.EscapeDataString(returnUrl)}";
            }
            else
            {
                authProps.RedirectUri = returnUrl;
            }

            return Challenge(authProps, "GitHub");
        }

        /// <summary>
        /// Called after OAuth completes on canonical host when user came from a vanity domain.
        /// Creates an encrypted token and redirects back to the vanity domain.
        /// </summary>
        [HttpGet]
        [Authorize]
        public IActionResult CompleteLogin(string returnHost, string returnUrl = "/")
        {
            // Validate returnHost is a known vanity domain
            if (string.IsNullOrEmpty(returnHost) || !_oauthEnvService.IsVanityDomain(returnHost))
            {
                return RedirectToAction("Login");
            }

            // Create encrypted token with user claims
            var token = _authTokenService.CreateToken(User, returnHost, returnUrl);
            
            // Redirect to vanity domain's ExchangeToken endpoint
            var scheme = Request.Scheme;
            var redirectUrl = $"{scheme}://{returnHost}/Account/ExchangeToken?token={Uri.EscapeDataString(token)}";
            
            return Redirect(redirectUrl);
        }

        /// <summary>
        /// Called on vanity domain to exchange the encrypted token for a local auth cookie.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ExchangeToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            var payload = _authTokenService.ValidateToken(token);
            if (payload == null)
            {
                // Invalid or expired token
                return RedirectToPage("/Login");
            }

            // Verify the token was intended for this host
            var currentHost = Request.Host.Value;
            if (!string.Equals(payload.ReturnHost, currentHost, StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToPage("/Login");
            }

            // Build claims identity from token payload
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, payload.UserId ?? ""),
                new Claim(ClaimTypes.Name, payload.UserName ?? ""),
                new Claim(APIView.Identity.ClaimConstants.Login, payload.Login ?? ""),
                new Claim(APIView.Identity.ClaimConstants.Email, payload.Email ?? ""),
                new Claim(APIView.Identity.ClaimConstants.Avatar, payload.Avatar ?? ""),
                new Claim(APIView.Identity.ClaimConstants.Url, payload.Url ?? ""),
                new Claim(APIView.Identity.ClaimConstants.Orgs, payload.Orgs ?? ""),
                new Claim(APIView.Identity.ClaimConstants.Name, payload.UserName ?? "")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // Sign in on the vanity domain
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            return LocalRedirect(payload.ReturnUrl ?? "/");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return RedirectToPage("/Login");
        }
    }
}
