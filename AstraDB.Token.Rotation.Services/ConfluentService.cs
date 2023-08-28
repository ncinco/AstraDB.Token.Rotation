using Confluent.Kafka;

namespace AstraDB.Token.Rotation.Services
{
    public class ConfluentService : IConfluentService
    {
        private readonly IConfigurationService _configurationService;
        private readonly IAuthenticateCallbackHandler _authenticateCallbackHandler;

        public ConfluentService(IConfigurationService configurationService, IAuthenticateCallbackHandler authenticateCallbackHandler)
        {
            _configurationService = configurationService;
            _authenticateCallbackHandler = authenticateCallbackHandler;
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

        public IProducer<string, string> CreateProducer()
        {
            var config = _configurationService
                .GetConfig<ProducerConfig>("Producer");

            var producerBuilder = new ProducerBuilder<string, string>(config);

            var producer = producerBuilder
                .SetOAuthBearerTokenRefreshHandler(_authenticateCallbackHandler.Handle)
                .Build();

            return producer;
        }

        public IConsumer<string, string> CreateConsumer()
        {
            var config = _configurationService
                .GetConfig<ConsumerConfig>("Consumer");

            var consumer = new ConsumerBuilder<string, string>(config)
                .SetOAuthBearerTokenRefreshHandler(_authenticateCallbackHandler.Handle)
                .Build();

            return consumer;
        }
    }
}