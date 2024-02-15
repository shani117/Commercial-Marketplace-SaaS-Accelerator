using Microsoft.Graph.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Marketplace.SaaS.Accelerator.Services.Contracts;

public interface IGraphApiOperations
{
    Task<User> GetUserInformation(string accessToken, string usrId = "");
    Task<string> GetPhotoAsBase64Async(string accessToken);
    Task<List<JObject>> GetAppRoleAssignedToForSpn(string accessToken, string spnAppId);
    Task<ServicePrincipal> GetCionSysSPNFromTenant(string accessToken, string spnAppId);
    Task<bool> AddCionSysSPNRoleToUser(string accessToken, AppRoleAssignment model);
    Task<bool> RemoveCionSysSPNRoleFromUser(string accessToken, string spnAppId, string aId);
    Task<IDictionary<string, string>> EnumerateTenantsIdAndNameAccessibleByUser(IEnumerable<string> tenantIds, Func<string, Task<string>> getTokenForTenant);
}