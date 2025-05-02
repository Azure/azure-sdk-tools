using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Matching;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;

namespace Azure.Sdk.Tools.TestProxy.Common
{
    public sealed record HeaderMatchMetadata(string Name, string ExpectedValue);

    public sealed class HeaderMatchPolicy : IEndpointSelectorPolicy
    {
        public int Order => 0;

        public bool AppliesToEndpoints(IReadOnlyList<Endpoint> endpoints)
            => endpoints.Any(e => e.Metadata.GetMetadata<HeaderMatchMetadata>() is not null);

        public Task ApplyAsync(HttpContext httpContext, CandidateSet candidates)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                var meta = candidates[i].Endpoint.Metadata.GetMetadata<HeaderMatchMetadata>();
                if (meta is null) continue;

                var OK = httpContext.Request.Headers.TryGetValue(meta.Name, out var v)
                         && string.Equals(v, meta.ExpectedValue, StringComparison.OrdinalIgnoreCase);

                if (!OK) candidates.SetValidity(i, false);
            }
            return Task.CompletedTask;
        }
    }
}
