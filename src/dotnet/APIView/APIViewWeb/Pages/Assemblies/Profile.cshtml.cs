using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using System.Threading.Tasks;

namespace APIViewWeb.Pages.Assemblies
{
    public class ProfileModel : PageModel
    {
        private readonly UserProfileManager _manager;
        public readonly UserPreferenceCache _preferenceCache;

        public UserProfileModel userProfile;
        public ProfileModel(UserProfileManager manager, UserPreferenceCache preferenceCache)
        {
            _manager = manager;
            _preferenceCache = preferenceCache;
        }

        public async Task<IActionResult> OnGetAsync(string UserName)
        {
            UserProfileModel profile;
            if(UserName == null || User.GetGitHubLogin().Equals(UserName))
            {
                profile = await this._manager.tryGetUserProfileAsync(User);
            }
            else
            {
                profile = await this._manager.tryGetUserProfileByNameAsync(UserName);
                // Default/original behaviour case - send them to the github page if the user profile doesn't exist yet. (Useful until everyone is up to date)
                if (profile.Email == null)
                {
                    return Redirect("https://github.com/" + UserName);
                }
            }

            userProfile = profile;
            return Page();
        }
    }
}
