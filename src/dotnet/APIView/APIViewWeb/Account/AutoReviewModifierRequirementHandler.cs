// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class AutoReviewModifierRequirementHandler : IAuthorizationHandler
    {
        private static string _autoReviewOwner = "azure-sdk";
        public Task HandleAsync(AuthorizationHandlerContext context)
        {
            if (context.User != null)
            {
                var loggedInUser = context.User.GetGitHubLogin();
                foreach (var requirement in context.Requirements)
                {
                    if (requirement is AutoReviewModifierRequirement)
                    {
                        var apiRevision = ((APIRevisionListItemModel)context.Resource);
                        // If review is auto created by bot then ensure logged in user is bot and review owner is bot
                        if (apiRevision.APIRevisionType != APIRevisionType.Automatic || (loggedInUser == _autoReviewOwner && apiRevision.CreatedBy == _autoReviewOwner))
                        {
                            context.Succeed(requirement);
                        }
                    }
                }
            }            
            return Task.CompletedTask;
        }
    }
}
