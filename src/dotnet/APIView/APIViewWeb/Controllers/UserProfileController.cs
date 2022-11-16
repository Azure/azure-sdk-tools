using Microsoft.AspNetCore.Mvc;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace APIViewWeb.Controllers
{
    public class UserProfileController : Controller
    {
        private readonly UserProfileManager _userProfileManager;
        private readonly UserPreferenceCache _userPreferenceCache;

        public UserProfileController(UserProfileManager userProfileManager, UserPreferenceCache userPreferenceCache)
        {
            _userProfileManager = userProfileManager;
            _userPreferenceCache = userPreferenceCache;
        }

        [HttpPut]
        public ActionResult UpdateReviewPageSettings(bool? hideLineNumbers = null, bool? hideLeftNavigation = null, bool? showHiddenApis = null)
        {
            _userPreferenceCache.UpdateUserPreference(new UserPreferenceModel()
            {
                HideLeftNavigation = hideLeftNavigation,
                HideLineNumbers = hideLineNumbers,
                ShowHiddenApis = showHiddenApis
            }, User);
            return Ok();
        }

        [HttpPost]
        public async Task<ActionResult> Update(string email, string[] languages, string theme="light-theme")
        {
            UserProfileModel profile = await _userProfileManager.tryGetUserProfileAsync(User);
            UserPreferenceModel preference = await _userPreferenceCache.GetUserPreferences(User);

            preference.Theme = theme;

            HashSet<string> Languages = new HashSet<string>(languages);
            preference.ApprovedLanguages = Languages;

            if(profile.UserName == null)
            {
                await _userProfileManager.createUserProfileAsync(User, email, Languages, preference);
            } else
            {
                await _userProfileManager.updateUserProfile(User, email, Languages, preference);
            }
            this._userPreferenceCache.UpdateUserPreference(preference, User);

            return RedirectToPage("/Assemblies/Index");
        }
    }
}
