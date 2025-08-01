using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.KeyVault;
using Azure.ResourceManager.KeyVault.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Azure.Security.KeyVault.Secrets;
using azure_storage_track2;

Console.WriteLine($"Login to Azure tenant={AppConfiguration.Instance.TenantId} ...");

ArmClient client = new ArmClient(new DefaultAzureCredential(new DefaultAzureCredentialOptions()
{
    TenantId = AppConfiguration.Instance.TenantId
}), AppConfiguration.Instance.SubscriptionId);

SubscriptionResource sub = client.GetSubscriptions().Get(AppConfiguration.Instance.SubscriptionId);
Console.WriteLine($"Selected subscription {sub.Data.DisplayName}({AppConfiguration.Instance.SubscriptionId})");

var rgName = AppConfiguration.Instance.ResourceNamePrefix + "-rg";
var kvName = AppConfiguration.Instance.ResourceNamePrefix + "-kv";
var storageName = AppConfiguration.Instance.ResourceNamePrefix + "sa";
var location = AppConfiguration.Instance.Location;

Console.WriteLine($"Checking resource group {rgName} ...");
ResourceGroupResource? rg = null;
try
{
    rg = client.GetDefaultSubscription().GetResourceGroup(rgName);
}
catch (RequestFailedException e)
{
    if (e?.ErrorCode != "ResourceGroupNotFound")
    {
        throw;
    }
}

if (rg == null)
{
    Console.WriteLine($"Cannot find resource group {rgName}. Creating ...");
    ResourceGroupData resourceGroupData = new ResourceGroupData(new AzureLocation(location));
    client.GetDefaultSubscription().GetResourceGroups().CreateOrUpdate(WaitUntil.Completed, rgName, resourceGroupData);
    Console.WriteLine($"Created resource group {rgName}");
}

Console.WriteLine($"Checking keyvault {kvName} ...");
KeyVaultResource? kv = null;
try
{
    kv = rg.GetKeyVault(kvName).Value;
}
catch (RequestFailedException e)
{
    if (e?.ErrorCode != "ResourceNotFound")
    {
        throw;
    }
}

if (kv == null)
{
    Console.WriteLine($"Cannot find Azure keyvault {kvName}. Creating ...");
    var vaultProperties = new KeyVaultProperties(Guid.Parse(AppConfiguration.Instance.TenantId), new KeyVaultSku(KeyVaultSkuFamily.A, KeyVaultSkuName.Standard));
    vaultProperties.EnableRbacAuthorization = true;
    vaultProperties.EnableSoftDelete = false;
    KeyVaultCreateOrUpdateContent parameters = new KeyVaultCreateOrUpdateContent(location, vaultProperties);
    rg.GetKeyVaults().CreateOrUpdate(WaitUntil.Completed, kvName, parameters);
    Console.WriteLine($"Created Azure keyvault {kvName}");
}

Console.WriteLine($"Checking storage account {storageName} ...");
StorageAccountResource? storage = null;
try
{
    storage = rg.GetStorageAccount(storageName).Value;
}
catch (RequestFailedException e)
{
    if (e?.ErrorCode != "ResourceNotFound")
    {
        throw;
    }
}
if (storage == null)
{
    Console.WriteLine($"Cannot find Azure storage account {storageName}. Creating ...");
    var StorageAccountCreateParameters = new StorageAccountCreateOrUpdateContent(new StorageSku(StorageSkuName.StandardLrs), StorageKind.StorageV2, location);
    rg.GetStorageAccounts().CreateOrUpdate(WaitUntil.Completed, storageName, StorageAccountCreateParameters);
    Console.WriteLine($"Created Azure storage account {storageName}");
}

var keys = storage.GetKeys().ToList();
var key = keys[0].Value;

var secretClient = new SecretClient(new Uri($"https://{kvName}.vault.azure.net"), new DefaultAzureCredential());
secretClient.SetSecret("storage-key", key);
Console.WriteLine($"Wrote the key of Azure storage account {storageName} into Keyvault.");

// Console.WriteLine($"Deleting resource group {rgName} ...");
// client.GetDefaultSubscription().GetResourceGroup(rgName).Value.Delete(Azure.WaitUntil.Completed);
// Console.WriteLine($"Deleted resource group {rgName}");
