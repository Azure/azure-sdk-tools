// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb;

public class CommentOwnerRequirementHandler : IAuthorizationHandler
{
    private readonly IPermissionsManager _permissionsManager;

    public CommentOwnerRequirementHandler(IPermissionsManager permissionsManager)
    {
        _permissionsManager = permissionsManager;
    }

    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        foreach (IAuthorizationRequirement requirement in context.Requirements)
        {
            if (requirement is CommentOwnerRequirement)
            {
                string creator = ((CommentItemModel)context.Resource)?.CreatedBy;
                string loggedInUserName = context.User.GetGitHubLogin();

                // User can edit their own comments
                if (creator == loggedInUserName)
                {
                    context.Succeed(requirement);
                    continue;
                }

                if (creator == ApiViewConstants.AzureSdkBotName && !string.IsNullOrEmpty(loggedInUserName))
                {
                    EffectivePermissions permissions =
                        await _permissionsManager.GetEffectivePermissionsAsync(loggedInUserName);
                    if (permissions.IsLanguageApprover)
                    {
                        context.Succeed(requirement);
                    }
                }
            }
        }
    }
}
