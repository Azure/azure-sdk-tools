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
                    var revisionAndReview = context.Resource as Tuple<ReviewRevisionModel, ReviewModel>;
                    var loggedInUser = context.User.GetGitHubLogin();
                    if (revisionAndReview.Item1.Author == loggedInUser ||
                        revisionAndReview.Item2.Author == loggedInUser)
                    {
                        context.Succeed(requirement);
                    }
                }
            }
            return Task.CompletedTask;
        }
    }
}