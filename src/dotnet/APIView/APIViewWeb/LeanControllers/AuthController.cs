using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.Managers;
using APIViewWeb.Managers.Interfaces;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.LeanControllers
{
    public class AuthController : BaseApiController
    {
        private readonly ILogger<AuthController> _logger;
        private readonly IUserProfileManager _userProfileManager;

        public AuthController(ILogger<AuthController> logger, IUserProfileManager userProfileManager)
        {
            _logger = logger;
            _userProfileManager = userProfileManager;
        }

        [HttpGet]
        public ActionResult IsLogged()
        {
            var result = new
            {
                IsLoggedIn = true
            };
            return new LeanJsonResult(result, StatusCodes.Status200OK);
        }

        [Route("AppVersion")]
        [HttpGet]
        public ActionResult AppVersion()
        {
            var result = new
            {
                Hash = Startup.VersionHash
            };
            return new LeanJsonResult(result, StatusCodes.Status200OK);
        }

        [Route("profile")]
        [HttpGet]
        public async Task<ActionResult<UserProfileModel>> GetUserPreference()
        {
            return await _userProfileManager.TryGetUserProfileAsync(User);
        }
    }
}
