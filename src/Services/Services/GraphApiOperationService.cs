using Azure;
using Marketplace.SaaS.Accelerator.Services.Configurations;
using Marketplace.SaaS.Accelerator.Services.Contracts;
using Marketplace.SaaS.Accelerator.Services.Models;
using Marketplace.SaaS.Accelerator.Services.Utilities;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Marketplace.SaaS.Accelerator.Services.Services;

public class GraphApiOperationService : IGraphApiOperations
{
    private readonly HttpClient httpClient;
    private readonly GraphApiOptions webOptions;

    public GraphApiOperationService(HttpClient httpClient, IOptions<GraphApiOptions> webOptionValue)
    {
        this.httpClient = httpClient;
        webOptions = webOptionValue.Value;
    }

    public async Task<dynamic> GetUserInformation(string accessToken)
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(GraphConstants.BearerAuthorizationScheme,
                                          accessToken);
        var response = await httpClient.GetAsync($"{webOptions.GraphApiUrl}/beta/me");
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            dynamic me = JsonConvert.DeserializeObject(content);

            return me;
        }

        throw new
            HttpRequestException($"Invalid status code in the HttpResponseMessage: {response.StatusCode}.");
    }

    public async Task<string> GetPhotoAsBase64Async(string accessToken)
    {
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(GraphConstants.BearerAuthorizationScheme,
                                          accessToken);

        var response = await httpClient.GetAsync($"{webOptions.GraphApiUrl}/beta/me/photo/$value");
        if (response.StatusCode == HttpStatusCode.OK)
        {
            byte[] photo = await response.Content.ReadAsByteArrayAsync();
            string photoBase64 = Convert.ToBase64String(photo);

            return photoBase64;
        }
        else
        {
            return null;
        }
    }

    public async Task<List<JObject>> GetAppRoleAssignedToForSpn(string accessToken, string spnAppId)
    {
        httpClient.DefaultRequestHeaders.Authorization =
           new AuthenticationHeaderValue(GraphConstants.BearerAuthorizationScheme,
                                         accessToken);
        var response = await httpClient.GetAsync($"{webOptions.GraphApiUrl}/beta/servicePrincipals(appId='{spnAppId}')/appRoleAssignedTo");
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            var assignments = JsonConvert.DeserializeObject<EntityResponse>(content);

            return assignments?.Value;
        }

        throw new
            HttpRequestException($"Invalid status code in the HttpResponseMessage: {response.StatusCode}.");
    }

    public async Task<IDictionary<string, string>> EnumerateTenantsIdAndNameAccessibleByUser(IEnumerable<string> tenantIds, Func<string, Task<string>> getTokenForTenant)
    {
        Dictionary<string, string> tenantInfo = new Dictionary<string, string>();
        foreach (string tenantId in tenantIds)
        {
            string displayName;
            try
            {
                string accessToken = await getTokenForTenant(tenantId);
                httpClient.DefaultRequestHeaders.Remove("Authorization");
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var httpResult = await httpClient.GetAsync(GraphTenantInfoUrl);
                var json = await httpResult.Content.ReadAsStringAsync();
                OrganizationResult organizationResult = JsonConvert.DeserializeObject<OrganizationResult>(json);
                displayName = organizationResult.value.First().displayName;
            }
            catch
            {
                displayName = "you need to sign-in (or have the admin consent for the app) in that tenant";
            }

            tenantInfo.Add(tenantId, displayName);
        }
        return tenantInfo;
    }

    // Use the graph to get information (name) for a tenant 
    // See https://docs.microsoft.com/en-us/graph/api/organization-get?view=graph-rest-beta
    protected string GraphTenantInfoUrl { get; } = "https://graph.microsoft.com/beta/organization";
}

/// <summary>
/// Result for a call to graph/organizations.
/// </summary>
class OrganizationResult
{
    public Organization[] value { get; set; }
}

/// <summary>
/// We are only interested in the organization display name
/// </summary>
class Organization
{
    public string displayName { get; set; }
}