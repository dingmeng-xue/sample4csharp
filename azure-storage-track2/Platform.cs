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
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using System;

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
            var key = storageAccountResource.GetKeys().First().Value;

            var blobServiceClient = new BlobServiceClient(
                new Uri($"https://{storageName}.blob.core.windows.net"), new AppTokenCredential());
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(name);
            containerClient.CreateIfNotExists();

            BlobContainerSasPermissions permissions = BlobContainerSasPermissions.Read;
            DateTimeOffset expiresOn = DateTimeOffset.UtcNow.AddHours(+1);
            BlobSasBuilder sasBuilder = new BlobSasBuilder(permissions, expiresOn)
            {
                BlobContainerName = name,
            };
            sasBuilder.ToSasQueryParameters(new StorageSharedKeyCredential(storageName, key));

            containerClient.GenerateUserDelegationSasUri(permissions, DateTimeOffset.Now.AddDays(1));
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
