using Marketplace.SaaS.Accelerator.Services.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Marketplace.SaaS.Accelerator.CustomerSite.Controllers;

[Authorize]
public class GraphController : Controller
{
    readonly ITokenAcquisition tokenAcquisition;
    private readonly IGraphApiOperations graphApiOperations;

    public GraphController(ITokenAcquisition tokenAcquisition,
                          IGraphApiOperations graphApiOperations)
    {
        this.tokenAcquisition = tokenAcquisition;
        this.graphApiOperations = graphApiOperations;
    }

    public IActionResult Index()
    {
        return View();
    }

    [AuthorizeForScopes(Scopes = new[] { Services.Utilities.GraphConstants.ScopeUserRead })]
    public async Task<IActionResult> Profile()
    {
        var accessToken =
            await tokenAcquisition.GetAccessTokenForUserAsync(new[] { Services.Utilities.GraphConstants.ScopeUserRead });

        var me = await graphApiOperations.GetUserInformation(accessToken);
        var photo = await graphApiOperations.GetPhotoAsBase64Async(accessToken);

        ViewData["Me"] = me;
        ViewData["Photo"] = photo;
        this.TempData["ShowWelcomeScreen"] = "True";

        return View();
    }

    [AuthorizeForScopes(Scopes = new[] { Services.Utilities.GraphConstants.AppReadAll })]
    public async Task<IActionResult> AppRoleMgmt()
    {
        var accessToken =
            await tokenAcquisition.GetAccessTokenForUserAsync(new[] { Services.Utilities.GraphConstants.AppReadAll });

        var assignments = await graphApiOperations.GetAppRoleAssignedToForSpn(accessToken, "c37a71d2-b811-4bfc-a52b-04d209f3e98c"); //for the CionSys SPN

        var lstAssignments = JsonConvert.DeserializeObject<AppRoleAssignment[]>(JsonConvert.SerializeObject(assignments));

        this.TempData["ShowWelcomeScreen"] = "True";
        return View(lstAssignments);
    }

}
