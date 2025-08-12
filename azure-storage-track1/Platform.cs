using Microsoft.Azure.KeyVault;
using Microsoft.Azure.Management.Authorization;
using Microsoft.Azure.Management.KeyVault;
using Microsoft.Azure.Management.KeyVault.Models;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.Management.ResourceManager.Models;
using Microsoft.Azure.Management.Storage;
using Microsoft.Azure.Management.Storage.Models;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Rest;
using Microsoft.Rest.Azure;
using System.Threading.Tasks;
using static Microsoft.Azure.KeyVault.KeyVaultClient;
using AuthorizationModels = Microsoft.Azure.Management.Authorization.Models;
using KeyVaultModels = Microsoft.Azure.Management.KeyVault.Models;
using StorageModels = Microsoft.Azure.Management.Storage.Models;

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

            SubscriptionClient subscriptionClient = new SubscriptionClient(new AppTokenCredential());

            Subscription sub = subscriptionClient.Subscriptions.Get(AppConfiguration.Instance.SubscriptionId);
            ResourceManagementClient resourceClient = new ResourceManagementClient(new AppTokenCredential());
            resourceClient.SubscriptionId = AppConfiguration.Instance.SubscriptionId;
            Console.WriteLine($"Selected subscription {sub.DisplayName}({AppConfiguration.Instance.SubscriptionId})");

            ResourceGroup rg = initializeResourceGroup(resourceClient);
            storageAccount = initializeStorageAccount(rg);
            keyVault = initializeKeyVault(rg);

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

        private StorageAccount initializeStorageAccount(ResourceGroup rg)
        {
            Console.WriteLine($"Checking storage account {storageName} ...");

            var storageClient = new StorageManagementClient(new AppTokenCredential())
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

        private Vault initializeKeyVault(ResourceGroup rg)
        {
            Console.WriteLine($"Checking key vault {kvName} ...");

            var keyVaultClient = new KeyVaultManagementClient(new AppTokenCredential())
            {
                SubscriptionId = AppConfiguration.Instance.SubscriptionId
            };

            Vault keyvault = null;
            try
            {
                keyvault = keyVaultClient.Vaults.Get(rg.Name, kvName);
                Console.WriteLine($"Key vault {kvName} already exists.");
            }
            catch (CloudException e)
            {
                if (e?.Body?.Code != "ResourceNotFound")
                {
                    throw;
                }
            }

            if (keyvault == null)
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

                keyvault = keyVaultClient.Vaults.CreateOrUpdate(rg.Name, kvName, parameters);
                Console.WriteLine($"Created key vault {kvName}.");

                AuthorizationManagementClient authClient = new AuthorizationManagementClient(new AppTokenCredential());
                var roleAssignmentParameters = new AuthorizationModels.RoleAssignmentCreateParameters
                {
                    RoleDefinitionId = "/providers/Microsoft.Authorization/roleDefinitions/00482a5a-887f-4fb3-b363-3b7fe8e74483",
                    PrincipalId = new TokenCredentialProvider().GetCurrentPrincipalOid()
                };
                authClient.RoleAssignments.Create(
                    keyvault.Id,
                    Guid.NewGuid().ToString(),
                    roleAssignmentParameters);

            }
            return keyvault;
        }

        public Cabinet CreateCabinet(String name)
        {
            var storageClient = new StorageManagementClient(new AppTokenCredential())
            {
                SubscriptionId = AppConfiguration.Instance.SubscriptionId
            };

            var key = storageClient.StorageAccounts.ListKeys(rgName, storageName).Keys.First();

            CloudBlobClient client = new CloudBlobClient(
                new Uri($"https://{storageName}.blob.core.windows.net/"),
                new StorageCredentials(storageName, key.Value));

            CloudBlobContainer container = client.GetContainerReference(name);
            container.CreateIfNotExistsAsync();

            var uri = container.GetSharedAccessSignature(new SharedAccessBlobPolicy
            {
                Permissions = SharedAccessBlobPermissions.Read,
                SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddDays(1)
            });

            var callback = new AuthenticationCallback(async (authority, resource, scope) =>
            {
                var credential = new TokenCredentialProvider().TokenCredential.GetToken(
                    new Azure.Core.TokenRequestContext(new[] { "https://vault.azure.net/.default" }),
                    new CancellationToken());
                return credential.Token;
            });
            KeyVaultClient keyVaultClient = new KeyVaultClient(callback);
            var secret = keyVaultClient.SetSecretAsync($"https://{kvName}.vault.azure.net", name, uri);
            var cabinet = new Cabinet();
            cabinet.Name = name;
            cabinet.AccessUri = uri;

            return cabinet;
        }

        public Cabinet? GetCabinet(String name)
        {
            var storageClient = new StorageManagementClient(new AppTokenCredential())
            {
                SubscriptionId = AppConfiguration.Instance.SubscriptionId
            };

            var key = storageClient.StorageAccounts.ListKeys(rgName, storageName).Keys.First();

            CloudBlobClient client = new CloudBlobClient(
                new Uri($"https://{storageName}.blob.core.windows.net/"),
                new StorageCredentials(storageName, key.Value));

            CloudBlobContainer container = client.GetContainerReference(name);
            if(!container.Exists())
            {
                return null;
            }

            var callback = new AuthenticationCallback(async (authority, resource, scope) =>
            {
                var credential = new TokenCredentialProvider().TokenCredential.GetToken(
                    new Azure.Core.TokenRequestContext(new[] { "https://vault.azure.net/.default" }),
                    new CancellationToken());
                return credential.Token;
            });
            KeyVaultClient keyVaultClient = new KeyVaultClient(callback);
            var secret = keyVaultClient.GetSecretAsync($"https://{kvName}.vault.azure.net", name).Result;
            if (secret == null)
            {
                throw new Exception($"Cabinet {name} not found.");
            }
            var cabinet = new Cabinet();
            cabinet.Name = name;
            cabinet.AccessUri = secret.Value;
            return cabinet;
        }
    }
}
