using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using APIViewWeb.Repositories;
using APIViewWeb.Models;

//using System.Security.Claims;
//using APIViewWeb.Managers;


namespace APIViewWeb.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {   //inject dependency to call method - when to use Interface and when not?
        //private readonly UserProfileManager _userProfileManager;

        private readonly UserPreferenceCache _preferenceCache;
        public AccountController(UserPreferenceCache preferenceCache, /*UserProfileManager userProfileManager*/)
        {
            _preferenceCache = preferenceCache;
            //_userProfileManager = userProfileManager;
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

        //[HttpGet]
        //public async Task<IActionResult> LoginCallback()
        //{
        //    // Get the authenticated user
        //    var user = HttpContext.User;

        //    // Call the UpdateMicrosoftEmailInUserProfile method to update the user's Microsoft email
        //    await _userProfileManager.UpdateMicrosoftEmailInUserProfile(user);

        //    // Redirect to the desired page after authentication
        //    return RedirectToAction("Index", "Home");
        //}
    }
}
