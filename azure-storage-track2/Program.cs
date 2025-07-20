using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using azure_storage_track2;

Console.WriteLine($"Login to Azure tenant={AppConfiguration.Instance.TenantId} ...");

ArmClient client = new ArmClient(new DefaultAzureCredential(new DefaultAzureCredentialOptions()
{
    TenantId = AppConfiguration.Instance.TenantId
}), AppConfiguration.Instance.SubscriptionId);

SubscriptionResource sub = client.GetSubscriptions().Get(AppConfiguration.Instance.SubscriptionId);
Console.WriteLine($"Selected subscription {sub.Data.DisplayName}({AppConfiguration.Instance.SubscriptionId})");

var rgName = AppConfiguration.Instance.ResourceGroupName;
var location = AppConfiguration.Instance.Location;

Console.WriteLine($"Creating resource group {rgName} ...");
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

if (rg != null)
{
    Console.Error.WriteLine($"Resource group {rgName} exists");
    return;
}

ResourceGroupData resourceGroupData = new ResourceGroupData(new AzureLocation(location));
client.GetDefaultSubscription().GetResourceGroups().CreateOrUpdate(WaitUntil.Completed, rgName, resourceGroupData);
Console.WriteLine($"Created resource group {rgName}");

Console.WriteLine($"Deleting resource group {rgName} ...");
client.GetDefaultSubscription().GetResourceGroup(rgName).Value.Delete(Azure.WaitUntil.Completed);
Console.WriteLine($"Deleted resource group {rgName}");
