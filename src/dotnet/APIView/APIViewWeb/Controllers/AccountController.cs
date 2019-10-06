using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        [HttpGet]
        public async Task<IActionResult> Login(string returnUrl = "/")
        {
            await HttpContext.SignOutAsync();
            return Challenge(new AuthenticationProperties() { RedirectUri = returnUrl }, "GitHub");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return RedirectToPage("/Unauthorized");
        }
    }
}
