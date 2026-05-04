using System.Collections.Generic;
using System.Threading.Tasks;
using APIViewWeb.Helpers;
using APIViewWeb.LeanModels;
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
    ///     Get all permission groups
    /// </summary>
    [HttpGet("groups")]
    public async Task<ActionResult> GetAllGroups()
    {
        IEnumerable<GroupPermissionsModel> groups = await _permissionsManager.GetAllGroupsAsync();
        return new LeanJsonResult(groups, StatusCodes.Status200OK);
    }
}
