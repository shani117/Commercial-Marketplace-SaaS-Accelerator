using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Marketplace.SaaS.Accelerator.Services.Contracts;

public interface IGraphApiOperations
{
    Task<dynamic> GetUserInformation(string accessToken);
    Task<string> GetPhotoAsBase64Async(string accessToken);
    Task<List<JObject>> GetAppRoleAssignedToForSpn(string accessToken, string spnAppId);

    Task<IDictionary<string, string>> EnumerateTenantsIdAndNameAccessibleByUser(IEnumerable<string> tenantIds, Func<string, Task<string>> getTokenForTenant);
}