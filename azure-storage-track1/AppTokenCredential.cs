using Azure.Core;
using Azure.Identity;
using Microsoft.Rest;
using System.Net.Http.Headers;

namespace azure_storage_track1
{
    internal class AppTokenCredential : TokenCredentials
    {
        public AppTokenCredential() : base(new TokenCredentialProvider())
        {

        }
    }

    internal class TokenCredentialProvider : ITokenProvider
    {
        private readonly TokenCredential tokenCredential;

        public TokenCredential TokenCredential => tokenCredential;

        private string[] scopes;

        public TokenCredentialProvider()
        {
            tokenCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions()
            {
                TenantId = AppConfiguration.Instance.TenantId
            });
            scopes = new string[] { "https://management.core.windows.net/.default" };
        }

        public TokenCredentialProvider(TokenCredential tokenCredential, string[] scopes)
        {
            this.tokenCredential = tokenCredential;
            this.scopes = scopes;
        }

        public TokenCredentialProvider(string[] scopes)
        {
            tokenCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions()
            {
                TenantId = AppConfiguration.Instance.TenantId
            });
            this.scopes = scopes;
        }

        public async Task<AuthenticationHeaderValue> GetAuthenticationHeaderAsync(CancellationToken cancellationToken)
        {
            var accessToken = await tokenCredential.GetTokenAsync(new TokenRequestContext(scopes), cancellationToken);
            return new AuthenticationHeaderValue("Bearer", accessToken.Token);
        }

        public string GetCurrentPrincipalOid()
        {
            var token = this.tokenCredential.GetToken(new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/.default" }), new CancellationToken());
            var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(token.Token);
            return jwt.Claims.First(c => c.Type == "oid").Value;
        }
    }
}
