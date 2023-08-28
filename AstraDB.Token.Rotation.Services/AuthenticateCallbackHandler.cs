using Azure.Identity;
using Confluent.Kafka;

namespace AstraDB.Token.Rotation.Services
{
    public class AuthenticateCallbackHandler : IAuthenticateCallbackHandler
    {
        private const string PrincipalName = "confluent-managed-identity";

        public void Handle(IClient client, string configuration)
        {
            try
            {
                var extentions = new Dictionary<string, string>
                {
                    { "logicalCluster", "lkc-3ng110" },
                    { "identityPoolId", "pool-y6OM" }
                };

                var credential = new DefaultAzureCredential();
                var token = credential.GetToken(new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/" }));

                Console.Write($"Token: {token.Token}");
                Console.Write($"ExpiresOn: {token.ExpiresOn}");
                Console.Write(Environment.NewLine);

                var lifetime = token.ExpiresOn.ToUnixTimeMilliseconds();
                client.OAuthBearerSetToken(token.Token, lifetime, PrincipalName);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}