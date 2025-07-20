using Microsoft.Extensions.Configuration;

namespace azure_storage_track2
{
    public sealed class AppConfiguration
    {
        private static readonly Lazy<AppConfiguration> lazy = new Lazy<AppConfiguration>(() => new AppConfiguration());

        public static AppConfiguration Instance { get { return lazy.Value; } }

        private AppConfiguration()
        {
            IConfigurationRoot? configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("AppConfiguration.json")
                .Build();

            _tenantId = configuration["tenant"];
            _subscriptionId = configuration["subscription"];
            _resouceGroupName = configuration["resource-group"];
            _location = configuration["location"];
        }

        private String? _tenantId;
        private String? _subscriptionId;
        private String? _resouceGroupName;
        private String? _location;

        public String? TenantId => _tenantId;
        public String? SubscriptionId => _subscriptionId;
        public String? ResourceGroupName => _resouceGroupName;
        public String? Location => _location;
    }
}
