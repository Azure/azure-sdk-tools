using Microsoft.AspNetCore.Mvc;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using System.Threading.Tasks;
using System.Collections.Generic;
using APIViewWeb.Managers;
using APIViewWeb.DTOs;

namespace APIViewWeb.Controllers
{
    public class UserProfileController : Controller
    {
        private readonly IUserProfileManager _userProfileManager;
        private readonly UserProfileCache _userProfileCache;

        public UserProfileController(IUserProfileManager userProfileManager, UserProfileCache userProfileCache)
        {
            _userProfileManager = userProfileManager;
            _userProfileCache = userProfileCache;
        }

        [HttpPut]
        public async Task<ActionResult> UpdateReviewPageSettings(bool? hideLineNumbers = null, bool? hideLeftNavigation = null, 
            bool? showHiddenApis = null, bool? hideReviewPageOptions = null, bool? hideIndexPageOptions = null, 
            bool? showComments = null, bool? showSystemComments = null)
        {
            await _userProfileCache.UpdateUserProfileAsync(userName: User.GetGitHubLogin(), userPreferenceDto: new UserPreferenceDto()
            {
                HideLeftNavigation = hideLeftNavigation,
                HideLineNumbers = hideLineNumbers,
                ShowHiddenApis = showHiddenApis,
                HideReviewPageOptions = hideReviewPageOptions,
                HideIndexPageOptions = hideIndexPageOptions,
                ShowComments = showComments,
                ShowSystemComments = showSystemComments
            });
            return Ok();
        }

        /// <summary>
        /// Update the user profile and preference properties
        /// </summary>
        /// <param name="email">This is the main email used for notifications</param>
        /// <param name="languages">The languages that the user has selected to approve</param>
        /// <param name="theme">The app theme</param>
        /// <param name="useBetaIndexPage">If to use the beta index page</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<ActionResult> Update(string email, string[] languages, string theme="light-theme", bool useBetaIndexPage=false)
        {
            await _userProfileCache.UpdateUserProfileAsync(userName: User.GetGitHubLogin(), email: email, new UserPreferenceDto()
            {
                Theme = theme,
                UseBetaIndexPage = useBetaIndexPage,
                ApprovedLanguages = new HashSet<string>(languages)
            });
            return RedirectToPage("/Assemblies/Index");
        }
    }
}
