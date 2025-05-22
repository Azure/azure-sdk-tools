using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using System.Threading.Tasks;
using APIViewWeb.Managers;

namespace APIViewWeb.Pages.Assemblies
{
    public class ProfileModel : PageModel
    {
        private readonly IUserProfileManager _manager;
        public readonly UserProfileCache _userProfileCache;

        public UserProfileModel userProfile;
        public ProfileModel(IUserProfileManager manager, UserProfileCache userProfileCache)
        {
            _manager = manager;
            _userProfileCache = userProfileCache;
        }

        public IActionResult OnGetAsync(string UserName)
        {
            var spaUrl = "https://spa." + Request.Host.ToString() + $"/profile/{UserName}";
            return Redirect(spaUrl);
        }
    }
}
