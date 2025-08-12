using Azure.Identity;

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

        public static string GetCurrentPrincipalOid()
        {
            var token = new AppTokenCredential().GetToken(new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/.default" }));
            var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(token.Token);
            return jwt.Claims.First(c => c.Type == "oid").Value;
        }
    }
}
