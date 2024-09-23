using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace APIViewWeb.LeanControllers
{
    [Authorize("RequireOrganization")]
    [ApiController]
    [Route("api/[controller]")]
    public class BaseApiController : ControllerBase
    {
    }
}
