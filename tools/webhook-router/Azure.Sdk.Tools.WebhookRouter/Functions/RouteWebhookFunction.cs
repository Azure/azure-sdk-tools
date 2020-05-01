using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Identity;
using Azure.Data.AppConfiguration;
using Azure.Sdk.Tools.WebhookRouter.Routing;

namespace Azure.Sdk.Tools.WebhookRouter.Functions
{
    public class RouteWebhookFunction
    {
        public RouteWebhookFunction(IRouter router)
        {
            this.router = router;
        }

        private IRouter router;

        [FunctionName("route")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "route/{route}")] HttpRequest req,
            ILogger log,
            Guid route)
        {
            var rule = await router.GetRuleAsync(route);
            var payload = await rule.ParseRequestAsync(req);
            await router.RouteAsync(rule, payload);
            
            return new OkResult();
        }
    }
}
