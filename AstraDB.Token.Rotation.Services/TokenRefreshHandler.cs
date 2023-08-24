using Azure.Identity;
using Confluent.Kafka;

namespace AstraDB.Token.Rotation.Services
{
    public class TokenRefreshHandler : ITokenRefreshHandler
    {
        // 1 hour
        private const int LifeTimeInMs = 3600000;
        private const string PrincipalName = "confluent-managed-identity";

        public void ProducerCallbackHandler(IProducer<string, string> producer, string tokenValue)
        {
            Handle(producer, tokenValue);
        }

        public void ConsumerCallbackHandler(IConsumer<string, string> producer, string tokenValue)
        {
            Handle(producer, tokenValue);
        }

        private void Handle(IClient client, string tokenValue)
        {
            var credential = new DefaultAzureCredential();
            var token = credential.GetToken(new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/" }));

            Console.Write($"Token: {token.Token}");
            Console.Write($"ExpiresOn: {token.ExpiresOn}");

            if (!string.IsNullOrWhiteSpace(token.Token))
            {
                var expirationTicks = DateTime.Now.Ticks + token.ExpiresOn.Ticks;
                ClientExtensions.OAuthBearerSetToken(client, token.Token, expirationTicks, PrincipalName);
            }
        }
    }
}