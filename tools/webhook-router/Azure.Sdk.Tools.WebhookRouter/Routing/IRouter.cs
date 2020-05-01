using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.WebhookRouter.Routing
{
    public interface IRouter
    {
        Task RouteAsync(Rule rule, Payload payload);
        Task<Rule> GetRuleAsync(Guid routeId);
    }
}
