// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Models;
using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class CommentOwnerRequirementHandler : IAuthorizationHandler
    {
        public Task HandleAsync(AuthorizationHandlerContext context)
        {
            foreach (var requirement in context.Requirements)
            {
                if (requirement is CommentOwnerRequirement)
                {
                    if (((CommentItemModel)context.Resource).CreatedBy == context.User.GetGitHubLogin())
                    {
                        context.Succeed(requirement);
                    }
                }
            }
            return Task.CompletedTask;
        }
    }
}
