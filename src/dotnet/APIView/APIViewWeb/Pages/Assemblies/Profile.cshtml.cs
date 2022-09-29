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

        public async Task OnGetAsync()
        {
            UserProfileModel profile = await this._manager.tryGetUserProfileAsync(User);
            if(profile.UserName == null)
            {
                profile.UserName = User.GetGitHubLogin();
                string.Join(",", profile.Languages);
            }

            userProfile = profile;
        }
    }
}
