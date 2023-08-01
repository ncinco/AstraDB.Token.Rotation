using Confluent.Kafka;

namespace AstraDB.Token.Rotation.Services
{
    public interface IConfluentService
    {
        string TopicName { get; }

        string ProducerBootstrapServers { get; }

        string ConsumerBootstrapServers { get; }

        IProducer<K, V> CreateProducer<K, V>();

        IConsumer<K, V> CreateConsumer<K, V>();
    }
}