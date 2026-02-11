// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System.Threading.Tasks;
using APIViewWeb.LeanModels;
using APIViewWeb.Managers.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace APIViewWeb
{
    public class ApproverRequirementHandler : IAuthorizationHandler
    {
        protected readonly IPermissionsManager _permissionsManager;

        public ApproverRequirementHandler(IPermissionsManager permissionsManager)
        {
            _permissionsManager = permissionsManager;
        }

        public async Task HandleAsync(AuthorizationHandlerContext context)
        {
            foreach (var requirement in context.Requirements)
            {
                if (requirement is ApproverRequirement)
                {
                    string userId = context.User.GetGitHubLogin();
                    if (!string.IsNullOrEmpty(userId))
                    {
                        EffectivePermissions permissions = await _permissionsManager.GetEffectivePermissionsAsync(userId);
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
