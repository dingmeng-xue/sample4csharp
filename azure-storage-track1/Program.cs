using azure_storage_track1;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.ResourceManager.Models;
using Microsoft.Rest;
using Microsoft.Rest.Azure;

Console.WriteLine($"Login to Azure tenant={AppConfiguration.Instance.TenantId} ...");

var credentials = new TokenCredentials(new TokenCredentialProvider());

SubscriptionClient subscriptionClient = new SubscriptionClient(credentials);
Subscription sub = subscriptionClient.Subscriptions.Get(AppConfiguration.Instance.SubscriptionId);

ResourceManagementClient resourceClient = new ResourceManagementClient(credentials);
resourceClient.SubscriptionId = AppConfiguration.Instance.SubscriptionId;
Console.WriteLine($"Selected subscription {sub.DisplayName}({AppConfiguration.Instance.SubscriptionId})");

var rgName = AppConfiguration.Instance.ResourceGroupName;
var location = AppConfiguration.Instance.Location;

Console.WriteLine($"Creating resource group {rgName} ...");
ResourceGroup? rg = null;
try
{
    rg = resourceClient.ResourceGroups.Get(rgName);
} catch(CloudException e) {
    if(e?.Body?.Code != "ResourceGroupNotFound")
    {
        throw;
    }
}
if (rg != null)
{
    Console.Error.WriteLine($"Resource group {rgName} exists");
    return;
}

rg = resourceClient.ResourceGroups.CreateOrUpdate(rgName, new ResourceGroup { 
    Location = location
});
Console.WriteLine($"Created resource group {rgName}");

Console.WriteLine($"Deleting resource group {rgName} ...");
resourceClient.ResourceGroups.Delete(rg.Name);
Console.WriteLine($"Deleted resource group {rgName}");