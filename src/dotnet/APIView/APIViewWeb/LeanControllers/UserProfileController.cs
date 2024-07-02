using System.Threading.Tasks;
using APIViewWeb.Managers;
using APIViewWeb.Models;
using APIViewWeb.Repositories;
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
            return userProfile;
        }

        [Route("preference")]
        [HttpPut]
        public ActionResult UpdateUserPreference([FromBody] UserPreferenceModel userPreference)
        {
            _userPreferenceCache.UpdateUserPreference(userPreference, User);
            return Ok();
        }
    }
}
