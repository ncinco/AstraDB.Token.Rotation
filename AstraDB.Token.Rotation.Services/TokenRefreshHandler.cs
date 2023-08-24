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
            var token = credential.GetToken(new Azure.Core.TokenRequestContext(new[] { "api://8821947d-dffa-42de-8426-bf531171750d/.default" }));
            //var token = credential.GetToken(new Azure.Core.TokenRequestContext(new[] { "https://management.azure.com/" }));

            if (!string.IsNullOrWhiteSpace(token.Token))
            {
                ClientExtensions.OAuthBearerSetToken(client, token.Token, LifeTimeInMs, PrincipalName);
            }
        }
    }
}