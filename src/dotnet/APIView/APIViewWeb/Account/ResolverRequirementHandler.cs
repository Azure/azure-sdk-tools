// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Managers.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class ResolverRequirementHandler : ApproverRequirementHandler, IAuthorizationHandler
    {
        public ResolverRequirementHandler(IPermissionsManager permissionsManager) : base(permissionsManager) {}

        public new async Task HandleAsync(AuthorizationHandlerContext context)
        {
            foreach (var requirement in context.Requirements)
            {
                if (requirement is ResolverRequirement)
                {
                    Models.CommentThreadModel comments = (Models.CommentThreadModel)context.Resource;
                    var loggedInUserName = context.User.GetGitHubLogin();
                    
                    if (string.Equals(loggedInUserName, comments.Comments.First().CreatedBy, System.StringComparison.OrdinalIgnoreCase))
                    {
                        context.Succeed(requirement);
                        continue;
                    }
                    
                    if (!string.IsNullOrEmpty(loggedInUserName))
                    {
                        var permissions = await _permissionsManager.GetEffectivePermissionsAsync(loggedInUserName);
                        if (permissions.IsLanguageApprover)
                        {
                            context.Succeed(requirement);
                        }
                    }
                }
            }
        }
    }
}
