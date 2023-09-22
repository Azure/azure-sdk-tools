using Microsoft.AspNetCore.Mvc;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using System.Threading.Tasks;
using System.Collections.Generic;
using APIViewWeb.Managers;

namespace APIViewWeb.Controllers
{
    public class UserProfileController : Controller
    {
        private readonly IUserProfileManager _userProfileManager;
        private readonly UserPreferenceCache _userPreferenceCache;

        public UserProfileController(IUserProfileManager userProfileManager, UserPreferenceCache userPreferenceCache)
        {
            _userProfileManager = userProfileManager;
            _userPreferenceCache = userPreferenceCache;
        }

        [HttpPut]
        public ActionResult UpdateReviewPageSettings(bool? hideLineNumbers = null, bool? hideLeftNavigation = null, 
            bool? showHiddenApis = null, bool? hideReviewPageOptions = null, bool? hideIndexPageOptions = null, 
            bool? showComments = null, bool? showSystemComments = null)
        {
            _userPreferenceCache.UpdateUserPreference(new UserPreferenceModel()
            {
                HideLeftNavigation = hideLeftNavigation,
                HideLineNumbers = hideLineNumbers,
                ShowHiddenApis = showHiddenApis,
                HideReviewPageOptions = hideReviewPageOptions,
                HideIndexPageOptions = hideIndexPageOptions,
                ShowComments = showComments,
                ShowSystemComments = showSystemComments
            }, User);
            return Ok();
        }

        [HttpPost]
        public async Task<ActionResult> Update(string email, string[] languages, string theme="light-theme")
        {
            UserProfileModel profile = await _userProfileManager.TryGetUserProfileAsync(User);
            UserPreferenceModel preference = await _userPreferenceCache.GetUserPreferences(User);

            preference.Theme = theme;

            HashSet<string> Languages = new HashSet<string>(languages);
            preference.ApprovedLanguages = Languages;

            if(profile.UserName == null)
            {
                await _userProfileManager.CreateUserProfileAsync(User, email, Languages, preference);
            } else
            {
                await _userProfileManager.UpdateUserProfile(User, email, Languages, preference);
            }
            this._userPreferenceCache.UpdateUserPreference(preference, User);

            return RedirectToPage("/Assemblies/Index");
        }
    }
}
