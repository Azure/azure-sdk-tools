using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Azure.Sdk.Tools.WebhookRouter.Functions
{
    public class RouteWebhookFunction
    {
        [FunctionName("route")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "route/{route}")] HttpRequest req,
            ILogger log,
            string route)
        {
            return new OkResult();
        }
    }
}
