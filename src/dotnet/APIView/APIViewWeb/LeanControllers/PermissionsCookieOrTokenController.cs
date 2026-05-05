using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.Managers.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace APIViewWeb.LeanControllers;

[ApiController]
[Authorize("RequireTokenOrCookieAuthentication")]
[Route("api/permissions")]
public class PermissionsCookieOrTokenController : ControllerBase
{
    private readonly IPermissionsManager _permissionsManager;

    public PermissionsCookieOrTokenController(IPermissionsManager permissionsManager)
    {
        _permissionsManager = permissionsManager;
    }


    /// <summary>
    ///     Get the list of approvers for a specific language
    /// </summary>
    /// <param name="language">The programming language</param>
    /// <returns>List of usernames who can approve reviews for the specified language, sorted alphabetically</returns>
    [HttpGet("approvers/{language}")]
    public async Task<ActionResult<IEnumerable<string>>> GetApproversForLanguage(string language)
    {
        HashSet<string> approvers = await _permissionsManager.GetApproversForLanguageAsync(language);
        List<string> sortedApprovers = approvers.Where(a => !string.IsNullOrWhiteSpace(a))
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList();
        return new LeanJsonResult(sortedApprovers, StatusCodes.Status200OK);
    }

    /// <summary>
    ///     Get the list of admin usernames for contact information
    /// </summary>
    /// <returns>List of usernames who have admin permissions</returns>
    [HttpGet("admins")]
    public async Task<ActionResult<IEnumerable<string>>> GetAdminUsernames()
    {
        IEnumerable<string> admins = await _permissionsManager.GetAdminUsernamesAsync();
        return new LeanJsonResult(admins, StatusCodes.Status200OK);
    }
}
