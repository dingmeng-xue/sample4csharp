using Azure.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace azure_storage_track2
{
    internal class AppTokenCredential : DefaultAzureCredential
    {
        public AppTokenCredential() : base(new DefaultAzureCredentialOptions()
        {
            TenantId = AppConfiguration.Instance.TenantId
        })
        {
        }
    }
}
