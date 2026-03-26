using Microsoft.AspNetCore.Mvc;
using APIViewWeb.Repositories;
using System.Threading.Tasks;
using System.Collections.Generic;
using APIViewWeb.DTOs;

namespace APIViewWeb.Controllers
{
    public class UserProfileController : Controller
    {
        private readonly UserProfileCache _userProfileCache;

        public UserProfileController(UserProfileCache userProfileCache)
        {
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

        [HttpPut]
        public async Task<ActionResult> UpdateTheme(string theme = "light-theme")
        {
            var validThemes = new HashSet<string> { "light-theme", "dark-theme", "dark-solarized-theme" };
            if (!validThemes.Contains(theme))
            {
                return BadRequest($"Invalid theme. Valid themes are: {string.Join(", ", validThemes)}");
            }

            await _userProfileCache.UpdateUserProfileAsync(userName: User.GetGitHubLogin(), userPreferenceDto: new UserPreferenceDto()
            {
                Theme = theme
            });
            return Ok();
        }
    }
}
