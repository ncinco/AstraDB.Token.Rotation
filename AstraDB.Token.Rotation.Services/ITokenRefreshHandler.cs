using Confluent.Kafka;

namespace AstraDB.Token.Rotation.Services
{
    public interface ITokenRefreshHandler
    {
        void ProducerCallbackHandler(IProducer<string, string> producer, string tokenValue);

        void ConsumerCallbackHandler(IConsumer<string, string> producer, string tokenValue);
    }
}