using Octokit;
using System;
using System.Collections.Generic;
using System.Text;

namespace Azure.Sdk.Tools.CheckEnforcer.Handlers
{
    public class HandlerContext<T> where T: ActivityPayload
    {
        public HandlerContext(T payload, GitHubClient client)
        {
            this.Payload = payload;
            this.Client = client;
        }

        public T Payload { get; private set; }
        public GitHubClient Client { get; private set; }
    }
}
