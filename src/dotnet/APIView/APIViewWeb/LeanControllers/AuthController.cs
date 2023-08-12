using Microsoft.AspNetCore.Mvc;
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
            return Ok(true);
        }
    }
}
