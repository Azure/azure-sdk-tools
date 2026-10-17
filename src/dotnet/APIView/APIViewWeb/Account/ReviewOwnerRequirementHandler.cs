// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class ReviewOwnerRequirementHandler : IAuthorizationHandler
    {
        public Task HandleAsync(AuthorizationHandlerContext context)
        {
            foreach (var requirement in context.Requirements)
            {
                if (requirement is ReviewOwnerRequirement)
                {
                    if (((ReviewListItemModel)context.Resource).CreatedBy == context.User.GetGitHubLogin())
                    {
                        context.Succeed(requirement);
                    }
                }
            }
            return Task.CompletedTask;
        }
    }
}
