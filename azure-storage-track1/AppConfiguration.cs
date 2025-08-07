using Microsoft.Extensions.Configuration;

namespace azure_storage_track1
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

            _tenantId = configuration["tenant"] ?? string.Empty;
            _subscriptionId = configuration["subscription"] ?? string.Empty;
            _resourceNamePrefix = configuration["resource-prefix"] ?? string.Empty;
            _location = configuration["location"] ?? string.Empty;
        }

        private String _tenantId;
        private String _subscriptionId;
        private String _resourceNamePrefix;
        private String _location;

        public String TenantId => _tenantId;
        public String SubscriptionId => _subscriptionId;
        public String ResourceNamePrefix => _resourceNamePrefix;
        public String Location => _location;
    }
}
