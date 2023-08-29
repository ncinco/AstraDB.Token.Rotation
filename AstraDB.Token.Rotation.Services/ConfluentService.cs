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

        public IProducer<string, string> CreateProducer()
        {
            var config = _configurationService
                .GetConfig<ProducerConfig>("Producer");

            var handler = _createAuthenticateCallbackHandler(config);

            var producer = new ProducerBuilder<string, string>(config)
                .SetOAuthBearerTokenRefreshHandler(handler.Handle)
                .Build();

            return producer;
        }

        public IConsumer<string, string> CreateConsumer()
        {
            var config = _configurationService
                .GetConfig<ConsumerConfig>("Consumer");

            var handler = _createAuthenticateCallbackHandler(config);

            var consumer = new ConsumerBuilder<string, string>(config)
                .SetOAuthBearerTokenRefreshHandler(handler.Handle)
                .Build();

            return consumer;
        }

        private AuthenticateCallbackHandler _createAuthenticateCallbackHandler(ClientConfig clientConfig)
        {
            var extensions = clientConfig.SaslOauthbearerExtensions;

            if (extensions == null)
                throw new Exception("Missing SaslOauthbearerExtensions configuration.");

            var logicalCluster = string.Empty;
            var identityPoolId = string.Empty;
            var extensionsArray = extensions.Split(',');

            foreach (var extension in extensionsArray)
            {
                if (extension.StartsWith("logicalCluster"))
                {
                    logicalCluster = extension.Substring("logicalCluster".Length + 1);
                }

                if (extension.StartsWith("identityPoolId"))
                {
                    identityPoolId = extension.Substring("identityPoolId".Length + 1);
                }
            }

            var handler = new AuthenticateCallbackHandler("confluent-managed-identity", logicalCluster, identityPoolId);

            return handler;
        }
    }
}