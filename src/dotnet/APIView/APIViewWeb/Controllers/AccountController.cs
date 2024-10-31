using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using APIViewWeb.Repositories;
using APIViewWeb.Models;
using System;
using APIViewWeb.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System.Linq;

namespace APIViewWeb.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        public readonly IWebHostEnvironment _environment;

        public AccountController(IWebHostEnvironment env)
        {
            _environment = env;
        }

        [HttpGet]
        public async Task<IActionResult> Login(string returnUrl = "/")
        {
            await HttpContext.SignOutAsync();
            if (!Url.IsLocalUrl(returnUrl))
            {
                string[] origins = (this._environment.IsDevelopment()) ? URlHelpers.GetAllowedStagingOrigins() : URlHelpers.GetAllowedProdOrigins();
                Uri returnUri = new Uri(returnUrl);

                if (!origins.Contains(returnUri.GetLeftPart(UriPartial.Authority))) {
                    returnUrl = "/";
                }
            }
            return Challenge(new AuthenticationProperties() { RedirectUri = returnUrl }, "GitHub");
        }

        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return RedirectToPage("/Login");
        }
    }
}
