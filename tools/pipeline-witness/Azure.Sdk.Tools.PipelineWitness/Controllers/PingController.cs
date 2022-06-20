using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Azure.Sdk.Tools.PipelineWitness.Controllers;

[ApiController]
[Route("ping")]
public class PingController : ControllerBase
{
    private readonly ILogger<PingController> _logger;

    public PingController(ILogger<PingController> logger)
    {
        _logger = logger;
    }

    [HttpGet(Name = "GetPing")]
    public IActionResult Get()
    {
        return this.Ok();
    }
}
