// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class ApproverRequirementHandler : IAuthorizationHandler
    {
        //Remove Praveen and Pavel from approver once initial testing is completed on production instance.
        private readonly string[] approvers;

        public ApproverRequirementHandler(IConfiguration configuration)
        {
            var approverConfig = configuration["approvers"];
            if (!string.IsNullOrEmpty(approverConfig))
            {
                approvers = approverConfig.Split(",");
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