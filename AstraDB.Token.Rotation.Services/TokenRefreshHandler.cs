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


            if (!string.IsNullOrWhiteSpace(tokenValue))
            {
                ClientExtensions.OAuthBearerSetToken(client, tokenValue, LifeTimeInMs, PrincipalName);
            }
        }
    }
}