using Confluent.Kafka;

namespace AstraDB.Token.Rotation.Services
{
    public interface ITokenRefreshHandler
    {
        void ProducerCallbackHandler(IProducer<string, string> producer, string configuration);

        void ConsumerCallbackHandler(IConsumer<string, string> producer, string configuration);
    }
}