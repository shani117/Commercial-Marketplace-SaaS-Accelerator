using Marketplace.SaaS.Accelerator.DataAccess.Entities;
using Marketplace.SaaS.Accelerator.Services.Contracts;
using Marketplace.SaaS.Accelerator.Services.Models;
using Marketplace.SaaS.Accelerator.Services.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Marketplace.SaaS.Accelerator.CustomerSite.Controllers;

[Authorize]
public class GraphController : BaseController
{
    readonly ITokenAcquisition tokenAcquisition;
    private readonly IGraphApiOperations graphApiOperations;
    private ServicePrincipal cionSysSpn;

    public GraphController(ITokenAcquisition tokenAcquisition, IGraphApiOperations graphApiOperations)
    {
        this.tokenAcquisition = tokenAcquisition;
        this.graphApiOperations = graphApiOperations;
    }

    public IActionResult Index()
    {
        return View();
    }

    [AuthorizeForScopes(Scopes = new[] { GraphConstants.ScopeUserRead })]
    public async Task<IActionResult> Profile()
    {
        var accessToken =
            await tokenAcquisition.GetAccessTokenForUserAsync(new[] { GraphConstants.ScopeUserRead });

        var me = await graphApiOperations.GetUserInformation(accessToken);
        var photo = await graphApiOperations.GetPhotoAsBase64Async(accessToken);

        ViewData["Me"] = JObject.FromObject(me);
        ViewData["Photo"] = photo;
        this.TempData["ShowWelcomeScreen"] = "True";

        return View();
    }

    [AuthorizeForScopes(Scopes = new[] { GraphConstants.UserReadBasicAll, GraphConstants.AppReadAll, GraphConstants.AppRoleRWAll })]
    public async Task<IActionResult> AppRoleMgmt()
    {
        GraphPageModel pgModel = new GraphPageModel();
        List<SpnAssignment> spnAssignments = new List<SpnAssignment>();

        var accessToken =
            await tokenAcquisition.GetAccessTokenForUserAsync(new[] { GraphConstants.UserReadBasicAll, GraphConstants.AppReadAll, GraphConstants.AppRoleRWAll });

        cionSysSpn = await graphApiOperations.GetCionSysSPNFromTenant(accessToken, "c37a71d2-b811-4bfc-a52b-04d209f3e98c");

        if (cionSysSpn != null)
        {
            var assignments = await graphApiOperations.GetAppRoleAssignedToForSpn(accessToken, "c37a71d2-b811-4bfc-a52b-04d209f3e98c"); //for the CionSys SPN
            var lstAssignments = JsonConvert.DeserializeObject<AppRoleAssignment[]>(JsonConvert.SerializeObject(assignments));

            foreach (var assignment in lstAssignments)
            {
                var role = cionSysSpn.AppRoles.Where(a => a.Id == assignment.AppRoleId).FirstOrDefault();
                spnAssignments.Add(new SpnAssignment { RoleAssignment = assignment, AppRoleDescription = role?.Description, AppRoleDisplayName = role?.DisplayName });
            }

            if (ViewBag.AppRoles == null)
            {
                SelectList sl = new SelectList(cionSysSpn.AppRoles, "Id", "DisplayName", null);
                ViewBag.AppRoles = sl;
            }

            pgModel = new GraphPageModel { RoleAssignments = spnAssignments.ToArray(), HasClientConsented = true };
        }

        this.TempData["ShowWelcomeScreen"] = "True";
        return View(pgModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuthorizeForScopes(Scopes = new[] { GraphConstants.UserReadBasicAll, GraphConstants.AppReadAll, GraphConstants.AppRoleRWAll })]
    public async Task<JsonResult> GrantAppRoleToUser([FromBody] AddSpnAssignmentPageModel userAssignment)
    {

        var accessToken =
            await tokenAcquisition.GetAccessTokenForUserAsync(new[] { GraphConstants.UserReadBasicAll, GraphConstants.AppReadAll, GraphConstants.AppRoleRWAll });

        try
        {
            //first get the user ID
            var usrReponse = await graphApiOperations.GetUserInformation(accessToken, userAssignment.UserUpn);
            cionSysSpn = await graphApiOperations.GetCionSysSPNFromTenant(accessToken, "c37a71d2-b811-4bfc-a52b-04d209f3e98c");
            if (usrReponse != null & cionSysSpn != null)
            {
                var usrId = Guid.Parse(usrReponse.Id);
                //now try adding the SPN App role assignment
                var appRoleAssignment = new AppRoleAssignment { PrincipalId = usrId, ResourceId = Guid.Parse(cionSysSpn.Id), AppRoleId = Guid.Parse(userAssignment.AppRoleId) };
                var roleAdded = await graphApiOperations.AddCionSysSPNRoleToUser(accessToken, appRoleAssignment);

                return Json("Role added successfully, refresh the page");
            }
            return Json("User or SPN retrieval failed!!");
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AuthorizeForScopes(Scopes = new[] { GraphConstants.UserReadBasicAll, GraphConstants.AppReadAll, GraphConstants.AppRoleRWAll })]
    public async Task<JsonResult> RemoveAppRoleFromUser([FromBody] RemoveSpnAssignmentPageModel userAssignment)
    {
        var accessToken =
            await tokenAcquisition.GetAccessTokenForUserAsync(new[] { GraphConstants.UserReadBasicAll, GraphConstants.AppReadAll, GraphConstants.AppRoleRWAll });

        try
        {
            var roleAdded = await graphApiOperations.RemoveCionSysSPNRoleFromUser(accessToken, "c37a71d2-b811-4bfc-a52b-04d209f3e98c", userAssignment.AssignmentId);
            return Json("Role removed successfully, refresh the page");
        }
        catch (Exception ex)
        {
            throw new Exception(ex.Message);
        }
    }
}
