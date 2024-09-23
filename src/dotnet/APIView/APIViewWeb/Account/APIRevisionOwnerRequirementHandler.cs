// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class APIRevisionOwnerRequirementHandler : IAuthorizationHandler
    {
        public Task HandleAsync(AuthorizationHandlerContext context)
        {
            foreach (var requirement in context.Requirements)
            {
                if (requirement is RevisionOwnerRequirement)
                {
                    var revision = context.Resource as APIRevisionListItemModel;
                    var loggedInUser = context.User.GetGitHubLogin();
                    if (revision.CreatedBy == loggedInUser)
                    {
                        context.Succeed(requirement);
                    }
                }
            }
            return Task.CompletedTask;
        }
    }
}
