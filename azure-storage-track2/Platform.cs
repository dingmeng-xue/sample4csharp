using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Authorization;
using Azure.ResourceManager.Authorization.Models;
using Azure.ResourceManager.KeyVault;
using Azure.ResourceManager.KeyVault.Models;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Storage;
using Azure.ResourceManager.Storage.Models;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

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

        private StorageAccountResource storageAccountResource;
        private KeyVaultResource keyVaultResource;

        public void Initialize() {
            Console.WriteLine($"Login to Azure tenant={AppConfiguration.Instance.TenantId} ...");

            ArmClient client = new ArmClient(new AppTokenCredential(), AppConfiguration.Instance.SubscriptionId);

            SubscriptionResource sub = client.GetSubscriptions().Get(AppConfiguration.Instance.SubscriptionId);
            Console.WriteLine($"Selected subscription {sub.Data.DisplayName}({AppConfiguration.Instance.SubscriptionId})");

            ResourceGroupResource rg = initializeResourceGroup(client.GetDefaultSubscription());
            storageAccountResource = initializeStorageAccount(rg);
            keyVaultResource = initializeKeyVault(rg);
        }

        private ResourceGroupResource initializeResourceGroup(SubscriptionResource subscription)
        {
            Console.WriteLine($"Checking resource group {rgName} ...");
            ResourceGroupResource? rg = null;
            try
            {
                rg = subscription.GetResourceGroup(rgName);
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
                rg = subscription.GetResourceGroups().CreateOrUpdate(WaitUntil.Completed, rgName, resourceGroupData).Value;
                Console.WriteLine($"Created resource group {rgName}");
            }
            return rg;
        }

        private StorageAccountResource initializeStorageAccount(ResourceGroupResource rg)
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

        private KeyVaultResource initializeKeyVault(ResourceGroupResource rg)
        {
            Console.WriteLine($"Checking key vault {kvName} ...");
            KeyVaultResource? keyvault = null;
            try
            {
                keyvault = rg.GetKeyVault(kvName).Value;
            }
            catch (RequestFailedException e)
            {
                if (e?.ErrorCode != "ResourceNotFound")
                {
                    throw;
                }
            }

            if (keyvault == null)
            {
                Console.WriteLine($"Cannot find Azure key vault {kvName}. Creating ...");
                var vaultProperties = new KeyVaultProperties(Guid.Parse(AppConfiguration.Instance.TenantId), new KeyVaultSku(KeyVaultSkuFamily.A, KeyVaultSkuName.Standard));
                vaultProperties.EnableRbacAuthorization = true;
                vaultProperties.EnableSoftDelete = false;
                KeyVaultCreateOrUpdateContent parameters = new KeyVaultCreateOrUpdateContent(location, vaultProperties);
                keyvault = rg.GetKeyVaults().CreateOrUpdate(WaitUntil.Completed, kvName, parameters).Value;
                Console.WriteLine($"Created Azure key vault {kvName}");

                RoleAssignmentCreateOrUpdateContent roleAssignment = new RoleAssignmentCreateOrUpdateContent(
                    new ResourceIdentifier("/providers/Microsoft.Authorization/roleDefinitions/00482a5a-887f-4fb3-b363-3b7fe8e74483"), 
                    Guid.Parse(AppTokenCredential.GetCurrentPrincipalOid()));
                keyvault.GetRoleAssignments().CreateOrUpdate(WaitUntil.Completed, Guid.NewGuid().ToString(), roleAssignment);

                Console.WriteLine($"Assigned Key Vault Secrets User role to current principal in key vault {kvName}");

                Thread.Sleep(10000);
            }

            return keyvault;
        }

        public Cabinet CreateCabinet(String name)
        {
            var key = storageAccountResource.GetKeys().First().Value;

            StorageSharedKeyCredential credential = new StorageSharedKeyCredential(storageName, key);

            var blobServiceClient = new BlobServiceClient(
                new Uri($"https://{storageName}.blob.core.windows.net"), credential);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(name);
            containerClient.CreateIfNotExists();
            var a = containerClient.CanGenerateSasUri;

            BlobContainerSasPermissions permissions = BlobContainerSasPermissions.Read;
            DateTimeOffset expiresOn = DateTimeOffset.UtcNow.AddHours(+1);
            BlobSasBuilder sasBuilder = new BlobSasBuilder(permissions, expiresOn)
            {
                BlobContainerName = name,
            };
            sasBuilder.ToSasQueryParameters(new StorageSharedKeyCredential(storageName, key));

            containerClient.GenerateSasUri(sasBuilder);

            var uri = containerClient.GenerateSasUri(BlobContainerSasPermissions.Read, DateTimeOffset.Now.AddDays(1));


            var secretClient = new SecretClient(new Uri($"https://{kvName}.vault.azure.net"), new AppTokenCredential());
            secretClient.SetSecret(name, uri.ToString());

            var cabinet = new Cabinet();
            cabinet.Name = name;
            cabinet.AccessUri = uri.ToString();

            return cabinet;
        }

        public void DeleteCabinet(String name) {
            var blobServiceClient = new BlobServiceClient(
                new Uri($"https://{storageName}.blob.core.windows.net"), new AppTokenCredential());
            blobServiceClient.DeleteBlobContainer(name);
        }

        public Cabinet GetCabinet(String name)
        {
            var blobServiceClient = new BlobServiceClient(
                new Uri($"https://{storageName}.blob.core.windows.net"), new AppTokenCredential());
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(name);

            if(!containerClient.Exists())
            {
                return null;
            }

            var secretClient = new SecretClient(new Uri($"https://{kvName}.vault.azure.net"), new AppTokenCredential());
            KeyVaultSecret secret = secretClient.GetSecret(name).Value;
            var cabinet = new Cabinet();
            cabinet.Name = name;
            cabinet.AccessUri = secret.Value;
            return cabinet;
        }
    }
}
