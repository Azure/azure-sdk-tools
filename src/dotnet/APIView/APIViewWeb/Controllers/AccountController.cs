using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using APIViewWeb.Repositories;
using APIViewWeb.Models;


namespace APIViewWeb.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly UserPreferenceCache _preferenceCache;
        public AccountController(UserPreferenceCache preferenceCache)
        {
            _preferenceCache = preferenceCache;
        }

        [HttpGet]
        public async Task<IActionResult> Login(string returnUrl = "/")
        {
            await HttpContext.SignOutAsync();
            if (!Url.IsLocalUrl(returnUrl))
            {
                returnUrl = "/";
            }
            return Challenge(new AuthenticationProperties() { RedirectUri = returnUrl }, "GitHub");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return RedirectToPage("/Login");
        }

        [HttpPut]
        [Authorize("RequireOrganization")]
        public ActionResult UpdateSettings(bool? hideLineNumbers = null, bool? hideLeftNavigation = null, string theme = "light-theme")
        {
            _preferenceCache.UpdateUserPreference(new UserPreferenceModel()
            {
                HideLeftNavigation = hideLeftNavigation,
                HideLineNumbers = hideLineNumbers,
                Theme = theme
            }, User);
            return Ok();
        }
    }
}
