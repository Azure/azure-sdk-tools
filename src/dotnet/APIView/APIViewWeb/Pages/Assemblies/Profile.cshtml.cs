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

        public async Task<IActionResult> OnGetAsync(string UserName)
        {
            UserProfileModel profile;
            if(UserName == null || User.GetGitHubLogin().Equals(UserName))
            {
                profile = await this._manager.TryGetUserProfileAsync(User);
            }
            else
            {
                profile = await this._manager.TryGetUserProfileByNameAsync(UserName);
            }

            userProfile = profile;
            return Page();
        }
    }
}
