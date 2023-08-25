using Azure.Identity;
using Confluent.Kafka;

namespace AstraDB.Token.Rotation.Services
{
    public class TokenRefreshHandler : ITokenRefreshHandler
    {
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

            Console.WriteLine($"ExpiresOn: {token.ExpiresOn}");
            Console.WriteLine($"Token: {token.Token}");
            Console.WriteLine(Environment.NewLine);

            if (!string.IsNullOrWhiteSpace(token.Token))
            {
                var expirationTicks = DateTime.Now.Ticks + token.ExpiresOn.Ticks;
                ClientExtensions.OAuthBearerSetToken(client, token.Token, expirationTicks, PrincipalName);
            }
        }
    }
}