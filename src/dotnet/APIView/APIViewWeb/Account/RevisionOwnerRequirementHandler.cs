// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class RevisionOwnerRequirementHandler : IAuthorizationHandler
    {
        public Task HandleAsync(AuthorizationHandlerContext context)
        {
            foreach (var requirement in context.Requirements)
            {
                if (requirement is RevisionOwnerRequirement)
                {
                    var revision = context.Resource as ReviewRevisionModel;
                    var loggedInUser = context.User.GetGitHubLogin();
                    if (revision.Author == loggedInUser ||
                        revision.Review.Author == loggedInUser)
                    {
                        context.Succeed(requirement);
                    }
                }
            }
            return Task.CompletedTask;
        }
    }
}