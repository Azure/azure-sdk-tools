// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class ApproverRequirementHandler : IAuthorizationHandler
    {
        protected HashSet<string> approvers = new HashSet<string>();

        public ApproverRequirementHandler(IConfiguration configuration)
        {
            var approverConfig = configuration["approvers"];
            if (!string.IsNullOrEmpty(approverConfig))
            {
                foreach (var id in approverConfig.Split(","))
                {
                    approvers.Add(id);
                }
            }
        }

        public Task HandleAsync(AuthorizationHandlerContext context)
        {
            foreach (var requirement in context.Requirements)
            {
                if (requirement is ApproverRequirement)
                {
                    if (approvers != null && approvers.Contains(context.User.GetGitHubLogin()))
                    {
                        context.Succeed(requirement);
                    }
                }
            }
            return Task.CompletedTask;
        }
    }
}
