// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class ResolverRequirementHandler : ApproverRequirementHandler, IAuthorizationHandler
    {
        public ResolverRequirementHandler(IConfiguration configuration) : base(configuration) {}

        public new Task HandleAsync(AuthorizationHandlerContext context)
        {
            foreach (var requirement in context.Requirements)
            {
                if (requirement is ResolverRequirement)
                {
                    Models.CommentThreadModel comments = (Models.CommentThreadModel)context.Resource;
                    if (approvers != null && approvers.Contains(context.User.GetGitHubLogin()) || context.User.GetGitHubLogin().Equals(comments.Comments.First().CreatedBy))
                    {
                        context.Succeed(requirement);
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}
