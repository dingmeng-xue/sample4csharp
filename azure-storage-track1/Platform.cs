using Azure;
using Azure.Core;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.ResourceManager.Models;
using Microsoft.Rest;
using Microsoft.Rest.Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace azure_storage_track1
{
    public class Platform
    {

        private AppConfiguration _config;

        private string rgName;
        private string kvName;
        private string storageName;
        private string location;

        public Platform(AppConfiguration configuration)
        {
            _config = configuration;
            rgName = AppConfiguration.Instance.ResourceNamePrefix + "-rg";
            kvName = AppConfiguration.Instance.ResourceNamePrefix + "-kv";
            storageName = AppConfiguration.Instance.ResourceNamePrefix + "sa";
            location = AppConfiguration.Instance.Location;
        }
        public void Initialize()
        {
            Console.WriteLine($"Login to Azure tenant={AppConfiguration.Instance.TenantId} ...");

            var credentials = new TokenCredentials(new TokenCredentialProvider());

            SubscriptionClient subscriptionClient = new SubscriptionClient(credentials);
            Subscription sub = subscriptionClient.Subscriptions.Get(AppConfiguration.Instance.SubscriptionId);

            ResourceManagementClient resourceClient = new ResourceManagementClient(credentials);
            resourceClient.SubscriptionId = AppConfiguration.Instance.SubscriptionId;
            Console.WriteLine($"Selected subscription {sub.DisplayName}({AppConfiguration.Instance.SubscriptionId})");

            ResourceGroup rg = initializeResourceGroup(resourceClient);
        }

        private ResourceGroup initializeResourceGroup(ResourceManagementClient client)
        {
            Console.WriteLine($"Checking resource group {rgName} ...");
            ResourceGroup? rg = null;
            try
            {
                return client.ResourceGroups.Get(rgName);
            }
            catch (CloudException e)
            {
                if (e?.Body?.Code != "ResourceGroupNotFound")
                {
                    throw;
                }
            }

            if (rg == null)
            {
                Console.WriteLine($"Cannot find resource group {rgName}. Creating ...");
                rg = client.ResourceGroups.CreateOrUpdate(rgName, new ResourceGroup
                {
                    Location = location
                });
                Console.WriteLine($"Created resource group {rgName}");
            }
            return rg;
        }
    }
}
