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
using Marketplace.SaaS.Accelerator.Services.Utilities;

namespace Marketplace.SaaS.Accelerator.Services.Services;
public class AzureSubService : IAzureSubService
{
    private readonly ArmClient _armClient;
    private readonly SecretClient _secretClient;
    private readonly SaaSClientLogger<AzureSubService> _saaSClientLogger;

    public AzureSubService(ArmClient client, SecretClient sClient, SaaSClientLogger<AzureSubService> logger)
    {
        _armClient = client;
        _secretClient = sClient;
        _saaSClientLogger = logger;
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
            bool secretAlreadyCreated = false;
            Response<StorageAccountResource>? storageAcctRes = null;

            try
            {
                storageAcctRes = await resourceGroup.GetStorageAccountAsync(saAcctName);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _saaSClientLogger.Warn($"Storage account {saAcctName} does not exist in RG {resourceGroup.Data.Name}.");
            }
            

            if (storageAcctRes != null && storageAcctRes.Value != null)
            {
                _saaSClientLogger.Info($"Storage account for tenant {tenantName} already exsits!!");
                var currSecret = await this._secretClient.GetSecretAsync($"DBConnString-{tenantId}");
                if (currSecret != null && currSecret.Value != null)
                {
                    secretAlreadyCreated = true;
                    _saaSClientLogger.Info($"Storage account AKV secret for tenant {tenantName} already exsits!! - {currSecret.Value.Name}");
                }
            }
            else
            {
                _saaSClientLogger.Info($"Creating a new storage account for tenant {tenantName}");

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

                storageAcctRes = await resourceGroup.GetStorageAccountAsync(saAcctName);

                _saaSClientLogger.Info($"Completed creating a new storage account for tenant {tenantName}");
            }

            if (!secretAlreadyCreated)
            {
                await foreach (var item in storageAcctRes.Value.GetKeysAsync())
                {
                    var saConnStr = $"DefaultEndpointsProtocol=https;AccountName={saAcctName};AccountKey={item.Value};EndpointSuffix=core.windows.net";
                    KeyVaultSecret secret = this._secretClient.SetSecret($"DBConnString-{tenantId}", saConnStr);
                    _saaSClientLogger.Info($"Created a new storage account AKV secret for tenant {tenantName} - {secret.Name}");
                    break;  //just need to save 1 key to the AKV
                }
            }
            
            return true;
        }

        return false;
    }
}
