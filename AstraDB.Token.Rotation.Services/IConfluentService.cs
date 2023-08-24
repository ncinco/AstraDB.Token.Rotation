using Confluent.Kafka;

namespace AstraDB.Token.Rotation.Services
{
    public interface IConfluentService
    {
        string TopicName { get; }

        string ProducerBootstrapServers { get; }

        string ConsumerBootstrapServers { get; }

        IProducer<string, string> CreateProducer();

        IConsumer<string, string> CreateConsumer();
    }
}