using System;
using System.Collections.Generic;
using System.Linq;
using APIViewWeb.Repositories;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Threading.Tasks;
using Octokit;
using MongoDB.Driver;

namespace APIViewWeb.Helpers
{
    public class ManagerHelpers
    {
        public static async Task AssertApprover<T>(ClaimsPrincipal user, T model, IAuthorizationService authorizationService)
        {
            var result = await authorizationService.AuthorizeAsync(
                user,
                model,
                new[] { ApproverRequirement.Instance });
            if (!result.Succeeded)
            {
                throw new AuthorizationFailedException();
            }
        }
    }
}
