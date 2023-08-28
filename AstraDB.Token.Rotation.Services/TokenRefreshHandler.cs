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
            var extentions = new Dictionary<string, string>();
            extentions.Add("logicalCluster", "lkc-3ng110");
            extentions.Add("identityPoolId", "pool-y6OM");

            try
            {
                var credential = new DefaultAzureCredential();
                var token = credential.GetToken(new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/" }));

                Console.Write($"Token: {token.Token}");
                Console.Write($"ExpiresOn: {token.ExpiresOn}");
                Console.Write(Environment.NewLine);

                var lifetime = token.ExpiresOn.ToUnixTimeMilliseconds();
                client.OAuthBearerSetToken(token.Token, lifetime, PrincipalName, extentions);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.ToString());
                client.OAuthBearerSetTokenFailure(ex.ToString());
            }
        }
    }
}