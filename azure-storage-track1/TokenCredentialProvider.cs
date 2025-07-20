using Azure.Core;
using Azure.Identity;
using Microsoft.Rest;
using System.Net.Http.Headers;

namespace azure_storage_track1
{
    internal class TokenCredentialProvider : ITokenProvider
    {
        private readonly TokenCredential tokenCredential;

        private string[] scopes;

        public TokenCredentialProvider()
        {
            tokenCredential = new DefaultAzureCredential(new DefaultAzureCredentialOptions() {
                TenantId = AppConfiguration.Instance.TenantId
            });
            scopes = new string[] { "https://management.core.windows.net/.default" };
        }

        public TokenCredentialProvider(TokenCredential tokenCredential, string[] scopes)
        {
            this.tokenCredential = tokenCredential;
            this.scopes = scopes;
        }

        public async Task<AuthenticationHeaderValue> GetAuthenticationHeaderAsync(CancellationToken cancellationToken)
        {
            var accessToken = await tokenCredential.GetTokenAsync(new TokenRequestContext(scopes), cancellationToken);
            return new AuthenticationHeaderValue("Bearer", accessToken.Token);
        }
    }
}
