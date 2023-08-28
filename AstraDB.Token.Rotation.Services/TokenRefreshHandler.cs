using Azure.Identity;
using Confluent.Kafka;

namespace AstraDB.Token.Rotation.Services
{
    public class TokenRefreshHandler : ITokenRefreshHandler
    {
        private const string PrincipalName = "confluent-managed-identity";

        public void ProducerCallbackHandler(IProducer<string, string> producer, string configuration)
        {
            Handle(producer, configuration);
        }

        public void ConsumerCallbackHandler(IConsumer<string, string> producer, string configuration)
        {
            Handle(producer, configuration);
        }

        private void Handle(IClient client, string configuration)
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

                if (!string.IsNullOrWhiteSpace(token.Token))
                {
                    var lifetime = token.ExpiresOn.ToUnixTimeMilliseconds();

                    client.OAuthBearerSetToken(token.Token, lifetime, PrincipalName, extentions);
                }
            }
            catch(Exception ex)
            {
                var errorString = ex.ToString();

                Console.WriteLine(errorString);
                client.OAuthBearerSetTokenFailure(errorString);
            }
        }
    }
}