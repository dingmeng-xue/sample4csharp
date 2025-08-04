using azure_storage_track2;

var platform = new Platform(AppConfiguration.Instance);
platform.Initialize();

var cabinet = platform.CreateCabinet("cabinet1");

//platform.DeleteCabinet("cabinet1");

//var keys = storage.GetKeys().ToList();
//var key = keys[0].Value;

//var secretClient = new SecretClient(new Uri($"https://{kvName}.vault.azure.net"), new DefaultAzureCredential());
//secretClient.SetSecret("storage-key", key);
//Console.WriteLine($"Wrote the key of Azure storage account {storageName} into Keyvault.");

// Console.WriteLine($"Deleting resource group {rgName} ...");
// client.GetDefaultSubscription().GetResourceGroup(rgName).Value.Delete(Azure.WaitUntil.Completed);
// Console.WriteLine($"Deleted resource group {rgName}");
