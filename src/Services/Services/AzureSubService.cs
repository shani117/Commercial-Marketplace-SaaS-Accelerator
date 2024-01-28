using Azure.Core;
using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Marketplace.SaaS.Accelerator.Services.Contracts;
using System;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.IO;
using Azure.ResourceManager.Resources.Models;
using Azure.ResourceManager.Storage;
using Azure.Security.KeyVault.Secrets;
using System.Reflection;
using System.Reflection.Metadata;

namespace Marketplace.SaaS.Accelerator.Services.Services;
public class AzureSubService : IAzureSubService
{
    private readonly ArmClient _armClient;
    private readonly SecretClient _secretClient;

    public AzureSubService(ArmClient client, SecretClient sClient)
    {
        _armClient = client;
        _secretClient = sClient;
    }

    public async Task<bool> InitializeTenantStorageAndAkv(string tenantName, string tenantId)
    {
        ResourceIdentifier? _resourceGroupId = null;
        var deploymentName = $"{tenantName}-{tenantId}";

        var subscription = await this._armClient.GetDefaultSubscriptionAsync();

        if (subscription != null) 
        {
            var rgLro = await subscription.GetResourceGroups().CreateOrUpdateAsync(WaitUntil.Completed, tenantName, new ResourceGroupData(AzureLocation.WestUS));
            var resourceGroup = rgLro.Value;
            _resourceGroupId = resourceGroup.Id;
            var saAcctName = $"{tenantName}storacct";

            var templateContent = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Asset", "template.json")).TrimEnd();
            var deploymentContent = new ArmDeploymentContent(new ArmDeploymentProperties(ArmDeploymentMode.Incremental)
            {
                Template = BinaryData.FromString(templateContent),
                Parameters = BinaryData.FromObjectAsJson(new
                {
                    storageAccounts_name = new { value = saAcctName },
                    storageAccounts_location = new { value = "westus3" },
                    storageAccounts_tidTag = new { value = tenantId }
                })
            });

            //create the storage account
            var armDeplRes = await resourceGroup.GetArmDeployments().CreateOrUpdateAsync(WaitUntil.Completed, deploymentName, deploymentContent);

            var storageAcctRes = await resourceGroup.GetStorageAccountAsync(saAcctName);
            

            await foreach (var item in storageAcctRes.Value.GetKeysAsync())
            {
                var saConnStr = $"DefaultEndpointsProtocol=https;AccountName={saAcctName};AccountKey={item.Value};EndpointSuffix=core.windows.net";
                KeyVaultSecret secret = this._secretClient.SetSecret($"DBConnString-{tenantId}", saConnStr);
                break;  //just need to save 1 key to the AKV
            }

            return true;
        }

        return false;
    }
}
