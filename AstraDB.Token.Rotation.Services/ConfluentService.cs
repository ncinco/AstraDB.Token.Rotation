using Confluent.Kafka;

namespace AstraDB.Token.Rotation.Services
{
    public class ConfluentService : IConfluentService
    {
        private readonly IConfigurationService _configurationService;

        public ConfluentService(IConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }

        public string TopicName
        {
            get
            {
                return _configurationService
                .GetValue<string>("TopicName");
            }
        }

        public string ProducerBootstrapServers
        {
            get
            {
                var config = _configurationService
                    .GetConfig<ProducerConfig>("Producer");

                return config.BootstrapServers;
            }
        }

        public string ConsumerBootstrapServers
        {
            get
            {
                var config = _configurationService
                    .GetConfig<ConsumerConfig>("Consumer");

                return config.BootstrapServers;
            }
        }

        public IProducer<K, V> CreateProducer<K, V>()
        {
            var config = _configurationService
                .GetConfig<ProducerConfig>("Producer");

            var producer = new ProducerBuilder<K, V>(config)
                .Build();

            return producer;
        }

        public IConsumer<K, V> CreateConsumer<K, V>()
        {
            var config = _configurationService
                .GetConfig<ConsumerConfig>("Consumer");

            var producer = new ConsumerBuilder<K, V>(config)
                .Build();

            return producer;
        }
    }
}