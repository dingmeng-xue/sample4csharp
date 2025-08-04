using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.KeyVault;
using Azure.ResourceManager.KeyVault.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Microsoft.Identity.Client.Extensions.Msal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace azure_storage_track2
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

        private ArmClient client;

        private StorageAccountResource storageAccountResource;
        private KeyVaultResource keyVaultResource;

        public void Initialize() {
            Console.WriteLine($"Login to Azure tenant={AppConfiguration.Instance.TenantId} ...");

            client = new ArmClient(new DefaultAzureCredential(new DefaultAzureCredentialOptions()
            {
                TenantId = AppConfiguration.Instance.TenantId
            }), AppConfiguration.Instance.SubscriptionId);

            SubscriptionResource sub = client.GetSubscriptions().Get(AppConfiguration.Instance.SubscriptionId);
            Console.WriteLine($"Selected subscription {sub.Data.DisplayName}({AppConfiguration.Instance.SubscriptionId})");

            ResourceGroupResource rg = initializeResourceGroup(client);
            storageAccountResource = initializeStorageAccount(client, rg);
            keyVaultResource = initializeKeyVault(client, rg);
        }

        private ResourceGroupResource initializeResourceGroup(ArmClient client)
        {
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
                rg = client.GetDefaultSubscription().GetResourceGroups().CreateOrUpdate(WaitUntil.Completed, rgName, resourceGroupData).Value;
                Console.WriteLine($"Created resource group {rgName}");
            }
            return rg;
        }

        private StorageAccountResource initializeStorageAccount(ArmClient client, ResourceGroupResource rg)
        {
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
                storage = rg.GetStorageAccounts().CreateOrUpdate(WaitUntil.Completed, storageName, StorageAccountCreateParameters).Value;
                Console.WriteLine($"Created Azure storage account {storageName}");
            }
            return storage;
        }

        private KeyVaultResource initializeKeyVault(ArmClient client, ResourceGroupResource rg)
        {
            Console.WriteLine($"Checking key vault {kvName} ...");
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
                Console.WriteLine($"Cannot find Azure key vault {kvName}. Creating ...");
                var vaultProperties = new KeyVaultProperties(Guid.Parse(AppConfiguration.Instance.TenantId), new KeyVaultSku(KeyVaultSkuFamily.A, KeyVaultSkuName.Standard));
                vaultProperties.EnableRbacAuthorization = true;
                vaultProperties.EnableSoftDelete = false;
                KeyVaultCreateOrUpdateContent parameters = new KeyVaultCreateOrUpdateContent(location, vaultProperties);
                kv = rg.GetKeyVaults().CreateOrUpdate(WaitUntil.Completed, kvName, parameters).Value;
                Console.WriteLine($"Created Azure key vault {kvName}");
            }
            return kv;
        }

        public Cabinet CreateCabinet(String name)
        {
            return null;
        }
    }
}
