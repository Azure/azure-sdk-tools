using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using APIViewWeb.Models;
using System.Threading.Tasks;

namespace APIViewWeb.Pages.Assemblies
{
    public class ProfileModel : PageModel
    {
        private readonly UserProfileManager _manager;

        public UserProfileModel userProfile;
        public ProfileModel(UserProfileManager manager)
        {
            _manager = manager;
        }

        public async Task OnGetAsync()
        {
            UserProfileModel profile = await this._manager.tryGetUserProfileAsync(User);

            userProfile = profile;
        }
    }
}
