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
        private readonly UserPreferenceCache _userPreferenceCache;

        public UserProfileController(ILogger<AuthController> logger, IUserProfileManager userProfileManager,
            UserPreferenceCache userPreferenceCache)
        {
            _logger = logger;
            _userProfileManager = userProfileManager;
            _userPreferenceCache = userPreferenceCache;
        }

        [HttpGet]
        public async Task<ActionResult<UserProfileModel>> GetUserPreference()
        {
            var userProfile = await _userProfileManager.TryGetUserProfileAsync(User);
            var preference = await _userPreferenceCache.GetUserPreferences(User);
            if (preference != null)
            {
                userProfile.Preferences = preference;
            }
            return new LeanJsonResult(userProfile, StatusCodes.Status200OK);
        }

        [HttpPut("preference", Name = "UpdateUserPreference")]
        public ActionResult UpdateUserPreference([FromBody] UserPreferenceModel userPreference)
        {
            _userPreferenceCache.UpdateUserPreference(userPreference, User);
            return Ok();
        }

        [HttpPut(Name = "UpdateUserProfile")]
        public ActionResult UpdateUserProfile([FromBody] UserProfileModel userProfile)
        {
            _userProfileManager.UpdateUserProfile(User, userProfile.Email, userProfile.Languages, userProfile.Preferences);
            return Ok();
        }
    }
}
