using Confluent.Kafka;
using Microsoft.Extensions.Configuration;

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

    public class ConfluentService : IConfluentService
    {
        private readonly IConfigurationRoot _configurationRoot;

        public string TopicName
        {
            get
            {
#pragma warning disable CS8603 // Possible null reference return.
                return _configurationRoot
                .GetValue<string>("TopicName");
#pragma warning restore CS8603 // Possible null reference return.
            }
        }

        public string ProducerBootstrapServers
        {
            get
            {
                var config = _configurationRoot
                    .GetSection("Producer")
                    .Get<ProducerConfig>();

                return config.BootstrapServers;
            }
        }

        public string ConsumerBootstrapServers
        {
            get
            {
                var config = _configurationRoot
                    .GetSection("Consumer")
                    .Get<ConsumerConfig>();

                return config.BootstrapServers;
            }
        }

        public ConfluentService()
        {
            _configurationRoot = new ConfigurationBuilder()
                .AddJsonFile("./appsettings.json")
                .Build();
        }

        public IProducer<K, V> CreateProducer<K, V>()
        {
            var config = _configurationRoot
                .GetSection("Producer")
                .Get<ProducerConfig>();

            var producer = new ProducerBuilder<K, V>(config)
                .Build();

            return producer;
        }

        public IConsumer<K, V> CreateConsumer<K, V>()
        {
            var config = _configurationRoot
                .GetSection("Consumer")
                .Get<ConsumerConfig>();

            var producer = new ConsumerBuilder<K, V>(config)
                .Build();

            return producer;
        }
    }
}