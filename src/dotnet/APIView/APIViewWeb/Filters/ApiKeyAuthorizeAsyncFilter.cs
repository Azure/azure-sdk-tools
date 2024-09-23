// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace APIViewWeb.Filters
{
    public class ApiKeyAuthorizeAsyncFilter : Attribute, IAsyncAuthorizationFilter
    {
        private static string _apiKeyHeader = "ApiKey";
        private string _azure_sdk_bot = "azure-sdk";
        private HashSet<string> _apiKeyValues = new HashSet<string>();

        public ApiKeyAuthorizeAsyncFilter(IConfiguration configuration)
        {
            var apiKey = configuration[_apiKeyHeader];
            if (!string.IsNullOrEmpty(apiKey))
            {
                _apiKeyValues.UnionWith(apiKey.Split(","));
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            var request = context.HttpContext.Request;
            var hasApiKeyHeader = request.Headers.TryGetValue(_apiKeyHeader, out var apiKeyValue);
            if (hasApiKeyHeader && _apiKeyValues.Contains(apiKeyValue))
            {
                //Adding claim as github login type to keep it uniform across the checks
                var user = new Claim("urn:github:login", _azure_sdk_bot);
                var principal = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim> { user }));
                context.HttpContext.User = principal;
                return;
            }

            context.Result = new UnauthorizedResult();
        }
    }
}
