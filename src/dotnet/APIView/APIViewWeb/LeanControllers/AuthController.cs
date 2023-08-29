using APIViewWeb.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using Microsoft.Extensions.Logging;

namespace APIViewWeb.LeanControllers
{
    public class AuthController : BaseApiController
    {
        private readonly ILogger<AuthController> _logger;

        public AuthController(ILogger<AuthController> logger)
        {
            _logger = logger;
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

        [HttpGet]
        [Route("AppVersion")]
        public ActionResult AppVersion()
        {
            var result = new
            {
                Hash = Startup.VersionHash
            };
            return new LeanJsonResult(result, StatusCodes.Status200OK);
        }
    }
}
