using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APIViewWeb.LeanControllers
{
    public class UserProfileController : BaseApiController
    {
        private readonly UserProfileCache _userProfileCache;

        public UserProfileController(UserProfileCache userProfileCache)
        {
            _userProfileCache = userProfileCache;
        }

        [HttpGet]
        public async Task<ActionResult<UserProfileModel>> GetUserPreference([FromQuery]string userName = null)
        {
            userName = userName ?? User.GetGitHubLogin();
            try
            {
                var userProfile = await _userProfileCache.GetUserProfileAsync(userName, createIfNotExist: false);
                return new LeanJsonResult(userProfile, StatusCodes.Status200OK);
            }
            catch
            {
                return new LeanJsonResult(null, StatusCodes.Status404NotFound);
            }
        }

        [HttpPut("preference", Name = "UpdateUserPreference")]
        public async Task<ActionResult> UpdateUserPreference([FromBody] UserPreferenceModel userPreference)
        {
            if (User.GetGitHubLogin() != userPreference.UserName)
            {
                return Forbid();
            }
            await _userProfileCache.UpdateUserProfileAsync(userName: User.GetGitHubLogin(), userPreferenceModel: userPreference);
            return Ok();
        }

        [HttpPut(Name = "UpdateUserProfile")]
        public async Task<ActionResult> UpdateUserProfile([FromBody] UserProfileModel userProfile)
        {
            if (User.GetGitHubLogin() != userProfile.UserName)
            {
                return Forbid();
            }
            await _userProfileCache.UpdateUserProfileAsync(userName: User.GetGitHubLogin(), email: userProfile.Email, userPreferenceModel: userProfile.Preferences);
            return Ok();
        }
    }
}
