using Azure.Sdk.Tools.TestProxy.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Azure.Sdk.Tools.TestProxy.Sanitizers
{
    public class ReplaceRequestSubscriptionId : StripRequestUri
    {
        public ReplaceRequestSubscriptionId() : base(@"/(subscriptions)/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", "00000000-0000-0000-0000-000000000000") { }
    }
}
