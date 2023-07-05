using AstraDB.Token.Rotation.Configuration;
using AstraDB.Token.Rotation.Models;
using Confluent.Kafka;
using Newtonsoft.Json;
using RestSharp;

namespace AstraDB.Token.Rotation.Services
{
    public interface IKafkaService
    {
        void ProduceMessages();        
    }

    public class KafkaService
    {
        private readonly IKeyVaultService _keyVaultService;
        private readonly RestClient _restClient;

        public KafkaService(IKeyVaultService keyVaultService)
        {
            _keyVaultService = keyVaultService;

            _restClient = new RestClient(DevOpsApi.Url);
            _restClient.AddDefaultHeader("Content-Type", "application/json");
            _restClient.AddDefaultHeader("Authorization", $"Bearer {DevOpsApi.Token}");
        }

        public void ProduceMessages()
        {
            var config = new ProducerConfig
            {
                BootstrapServers = Kafka.BrokerList,
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SaslMechanism = SaslMechanism.Plain,
                SaslUsername = Kafka.Username,
                SaslPassword = Kafka.Password
            };

            using (var producer = new ProducerBuilder<long, string>(config).SetKeySerializer(Serializers.Int64).SetValueSerializer(Serializers.Utf8).Build())
            {
                Console.WriteLine("Attempting to fetch to AstraDB Tokens...");
                var astraTokensResponse = _restClient.ExecuteGet<AstraTokensResponse>(new RestRequest("v2/clientIdSecrets")).Data;
                Console.WriteLine("Succeeded fetching AstraDB Tokens.");

                // just exit but unlikely
                if (astraTokensResponse == null) return;

                Console.WriteLine("Attempting to fetch to Key Vault Secrets...");
                var keyVaultSecrets = _keyVaultService
                    .GetPropertiesOfSecrets();
                Console.WriteLine("Succeeded fetching Key Vault Secrets.");

                foreach (var secret in keyVaultSecrets)
                {
                    var status = secret.Tags[KeyVaultTags.Status];
                    var generatedOn = secret.Tags[KeyVaultTags.GeneratedOn];

                    if (string.Compare(status, KeyVaultStatus.Active, true) == 0
                        && (DateTime.UtcNow - DateTime.Parse(generatedOn)).Minutes >= 3
                        && secret.Name.Contains("-AccessToken"))
                    {
                        var seedClientId = secret.Tags[KeyVaultTags.SeedClientId];
                        var clientId = secret.Tags[KeyVaultTags.ClientId];

                        Console.WriteLine($"Trying to rotate {seedClientId}-AccessToken and {seedClientId}-ClientSecret");

                        // find matching astradb token
                        var theAstraDbToken = astraTokensResponse.Clients.FirstOrDefault(x => string.Compare(x.ClientId, clientId, true) == 0);

                        if (theAstraDbToken != null)
                        {
                            var key = DateTime.UtcNow.Ticks;
                            var messagePayload = new EventStreamTokenRotationMessage
                            {
                                SeedClientId = seedClientId,
                                ClientId = clientId,
                                Roles = theAstraDbToken.Roles
                            };

                            var messagePayloadJson = JsonConvert.SerializeObject(messagePayload);

                            producer.Produce(Kafka.Topic, new Message<long, string> { Key = key, Value = messagePayloadJson });

                            Console.WriteLine($"Message {key} sent (value: '{messagePayloadJson}')");
                        }
                    }
                }

                producer.Flush();
            }
        }
    }
}