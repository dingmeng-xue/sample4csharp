using Microsoft.Azure.Management.KeyVault;
using KeyVaultModels = Microsoft.Azure.Management.KeyVault.Models;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.ResourceManager.Models;
using Microsoft.Azure.Management.Storage;
using Microsoft.Azure.Management.Storage.Models;
using Microsoft.Rest;
using Microsoft.Rest.Azure;
using StorageModels = Microsoft.Azure.Management.Storage.Models;
using Microsoft.Azure.Management.KeyVault.Models;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.Auth;
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

        private StorageAccount storageAccount;
        private Vault keyVault;
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
            storageAccount = initializeStorageAccount(resourceClient, rg);
            keyVault = initializeKeyVault(resourceClient, rg);

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

        private StorageAccount initializeStorageAccount(ResourceManagementClient client, ResourceGroup rg)
        {
            Console.WriteLine($"Checking storage account {storageName} ...");

            // Create StorageManagementClient
            var credentials = new TokenCredentials(new TokenCredentialProvider());
            var storageClient = new StorageManagementClient(credentials)
            {
                SubscriptionId = AppConfiguration.Instance.SubscriptionId
            };

            StorageAccount storageAccount = null;
            try
            {
                storageAccount = storageClient.StorageAccounts.GetProperties(rg.Name, storageName);
                Console.WriteLine($"Storage account {storageName} already exists.");
            }
            catch (CloudException e)
            {
                if (e?.Body?.Code != "ResourceNotFound")
                {
                    throw;
                }
            }

            if (storageAccount == null)
            {
                Console.WriteLine($"Creating storage account {storageName} ...");
                var parameters = new StorageAccountCreateParameters
                {
                    Location = location,
                    Kind = Kind.StorageV2,
                    Sku = new StorageModels.Sku(StorageModels.SkuName.StandardLRS)
                };

                storageAccount = storageClient.StorageAccounts.Create(rg.Name, storageName, parameters);
                Console.WriteLine($"Created storage account {storageName}.");
            }
            return storageAccount;
        }

        private Vault initializeKeyVault(ResourceManagementClient client, ResourceGroup rg)
        {
            Console.WriteLine($"Checking key vault {kvName} ...");

            // Create KeyVaultManagementClient
            var credentials = new TokenCredentials(new TokenCredentialProvider());
            var keyVaultClient = new KeyVaultManagementClient(credentials)
            {
                SubscriptionId = AppConfiguration.Instance.SubscriptionId
            };

            Vault vault = null;
            try
            {
                vault = keyVaultClient.Vaults.Get(rg.Name, kvName);
                Console.WriteLine($"Key vault {kvName} already exists.");
            }
            catch (CloudException e)
            {
                if (e?.Body?.Code != "ResourceNotFound")
                {
                    throw;
                }
            }

            if (vault == null)
            {
                Console.WriteLine($"Creating key vault {kvName} ...");
                var parameters = new VaultCreateOrUpdateParameters
                {
                    Location = location,
                    Properties = new VaultProperties
                    {
                        TenantId = Guid.Parse(AppConfiguration.Instance.TenantId),
                        Sku = new KeyVaultModels.Sku(KeyVaultModels.SkuName.Standard),
                        EnableRbacAuthorization = true,
                        EnableSoftDelete = false,
                        EnabledForDeployment = false,
                        EnabledForDiskEncryption = false,
                        EnabledForTemplateDeployment = false
                    }
                };

                vault = keyVaultClient.Vaults.CreateOrUpdate(rg.Name, kvName, parameters);
                Console.WriteLine($"Created key vault {kvName}.");
            }
            return vault;
        }

        public async Task<Cabinet> CreateCabinet(String name)
        {

            CloudBlobClient client = new CloudBlobClient(
                new Uri($"https://{storageName}.blob.core.windows.net/"),
                new StorageCredentials(storageName, storageAccount.PrimaryEndpoints.Blob));

            CloudBlobContainer container = client.GetContainerReference(name);
            await container.CreateIfNotExistsAsync();

            var uri = container.GetSharedAccessSignature(new SharedAccessBlobPolicy
            {
                Permissions = SharedAccessBlobPermissions.Read,
                SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddDays(1)
            });

            var cabinet = new Cabinet();
            cabinet.Name = name;
            cabinet.AccessUri = uri;

            return cabinet;
        }
    }
}
