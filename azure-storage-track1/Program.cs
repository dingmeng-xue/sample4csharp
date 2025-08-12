using azure_storage_track1;

var platform = new Platform(AppConfiguration.Instance);
platform.Initialize();

var cabinet = platform.CreateCabinet("cabinet1");
cabinet = platform.GetCabinet("cabinet1");
Console.WriteLine($"Cabinet {cabinet.Name} exists with access URI: {cabinet.AccessUri}");

cabinet = platform.CreateCabinet("cabinet2");
cabinet = platform.GetCabinet("cabinet2");
Console.WriteLine($"Cabinet {cabinet.Name} exists with access URI: {cabinet.AccessUri}");