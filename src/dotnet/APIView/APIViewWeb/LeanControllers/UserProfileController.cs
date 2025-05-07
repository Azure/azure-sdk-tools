using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.LeanControllers
{
    public class UserProfileController : BaseApiController
    {
        private readonly ILogger<AuthController> _logger;
        private readonly IUserProfileManager _userProfileManager;
        private readonly UserProfileCache _userProfileCache;

        public UserProfileController(ILogger<AuthController> logger, IUserProfileManager userProfileManager, UserProfileCache userProfileCache)
        {
            _logger = logger;
            _userProfileManager = userProfileManager;
            _userProfileCache = userProfileCache;
        }

        [HttpGet]
        public async Task<ActionResult<UserProfileModel>> GetUserPreference()
        {
            var userProfile = await _userProfileCache.GetUserProfileAsync(User.GetGitHubLogin());
            return new LeanJsonResult(userProfile, StatusCodes.Status200OK);
        }

        [HttpPut("preference", Name = "UpdateUserPreference")]
        public async Task<ActionResult> UpdateUserPreference([FromBody] UserPreferenceModel userPreference)
        {
            await _userProfileCache.UpdateUserProfileAsync(userName: User.GetGitHubLogin(), userPreferenceModel: userPreference);
            return Ok();
        }

        [HttpPut(Name = "UpdateUserProfile")]
        public async Task<ActionResult> UpdateUserProfile([FromBody] UserProfileModel userProfile)
        {
            await _userProfileCache.UpdateUserProfileAsync(userName: User.GetGitHubLogin(), email: userProfile.Email, userPreferenceModel: userProfile.Preferences);
            return Ok();
        }
    }
}
