// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

namespace APIViewWeb
{
    public class CommentOwnerRequirementHandler : IAuthorizationHandler
    {
        private readonly IConfiguration _configuration;
        public CommentOwnerRequirementHandler(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public Task HandleAsync(AuthorizationHandlerContext context)
        {
            foreach (var requirement in context.Requirements)
            {
                if (requirement is CommentOwnerRequirement)
                {
                    var creator = ((CommentItemModel)context.Resource).CreatedBy;
                    var approvers = _configuration["Approvers"].Split(',');
                    var loggedInUserName = context.User.GetGitHubLogin();
                    if (creator == loggedInUserName ||
                        (approvers.Contains(loggedInUserName) && creator == ApiViewConstants.AzureSdkBotName))
                    {
                        context.Succeed(requirement);
                    }
                }
            }
            return Task.CompletedTask;
        }
    }
}
