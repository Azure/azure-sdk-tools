using Microsoft.AspNetCore.Mvc;
using APIViewWeb.Models;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace APIViewWeb.Controllers
{
    public class UserProfileController : Controller
    {
        private readonly UserProfileManager _userProfileManager;

        public UserProfileController(UserProfileManager userProfileManager)
        {
            _userProfileManager = userProfileManager;
        }

        [HttpPost]
        public async Task<ActionResult> Update(string email, string[] languages)
        {
            UserProfileModel profile = await _userProfileManager.tryGetUserProfileAsync(User);

            HashSet<string> Languages = new HashSet<string>(languages);
            if(profile.UserName == null)
            {
                await _userProfileManager.createUserProfileAsync(User, email, Languages);
            } else
            {
                await _userProfileManager.updateEmailAsync(User, email);
                await _userProfileManager.updateLanguagesAsync(User, Languages);
            }
            return RedirectToPage("/Assemblies/Index");
        }
    }
}
