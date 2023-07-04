using AstraDB.Token.Rotation.Configuration;
using AstraDB.Token.Rotation.Consumer;
using AstraDB.Token.Rotation.Models;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Confluent.Kafka;
using Newtonsoft.Json;
using RestSharp;

namespace AstraDB.Token.Rotation.Producer
{

    public interface IKafkaService
    {
        void ProduceMessages();

        void ExpireTokens();
    }

    public class KafkaService
    {
        private RestClient _restClient;
        private readonly IKeyVaultService _keyVaultService;
        private readonly ClientSecretCredential _credential;
        private readonly SecretClient _keyVaultSecretClient;

        public KafkaService(IKeyVaultService keyVaultService)
        {
            _keyVaultService = keyVaultService;

            _credential = new ClientSecretCredential(KeyVault.TenantId, KeyVault.ClientId, KeyVault.ClientSecret);
            _keyVaultSecretClient = new SecretClient(new Uri(KeyVault.KeyVaultUrl), _credential);
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
                var restClient = new RestClient(DevOpsApi.Url);
                restClient.AddDefaultHeader("Content-Type", "application/json");
                restClient.AddDefaultHeader("Authorization", $"Bearer {DevOpsApi.Token}");

                Console.WriteLine("Attempting to fetch to AstraDB Tokens...");
                var astraTokensResponse = restClient.ExecuteGet<AstraTokensResponse>(new RestRequest("v2/clientIdSecrets")).Data;
                Console.WriteLine("Succeeded fetching AstraDB Tokens.");

                // just exit but unlikely
                if (astraTokensResponse == null) return;

                Console.WriteLine("Attempting to fetch to Key Vault Secrets...");
                var keyVaultSecrets = _keyVaultSecretClient
                    .GetPropertiesOfSecrets()
                    .ToList();
                Console.WriteLine("Succeeded fetching Key Vault Secrets.");

                foreach (var secret in keyVaultSecrets)
                {
                    var status = secret.Tags[KeyVaultTags.Status];
                    var generatedOn = secret.Tags[KeyVaultTags.GeneratedOn];

                    if (string.Compare(status, KeyVaultTagStatus.Active, true) == 0
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

        public void ExpireTokens()
        {
            _restClient = new RestClient(DevOpsApi.Url);
            _restClient.AddDefaultHeader("Content-Type", "application/json");
            _restClient.AddDefaultHeader("Authorization", $"Bearer {DevOpsApi.Token}");

            Console.WriteLine("Attempting to fetch to Key Vault Secrets...");
            var keyVaultSecrets = _keyVaultSecretClient
                .GetPropertiesOfSecrets()
                .ToList();
            Console.WriteLine("Succeeded fetching Key Vault Secrets.");

            // process just the rotating status with name contains "-AccessToken" since they come in pairs
            foreach (var secret in keyVaultSecrets
                .Where(x => string.Compare(x.Tags[KeyVaultTags.Status], KeyVaultTagStatus.Active) == 0
                && x.Name.Contains("-AccessToken")))
            {
                // only delete token and expire previous version when status rotating
                if (HasRotatingVersion(secret.Name))
                {
                    var seedClientId = secret.Tags[KeyVaultTags.SeedClientId];
                    var clientId = secret.Tags[KeyVaultTags.ClientId];

                    Console.WriteLine($"Attempting to revoke old astradb token '{clientId}'");
                    var revokeTokenRequest = new RestRequest($"v2/clientIdSecrets/{clientId}");
                    var astraRevokeTokenResponse = _restClient.Delete(revokeTokenRequest);
                    Console.WriteLine($"Succeeded revoking old astradb token. '{clientId}'");

                    Console.WriteLine($"Attempting expiring old key vault version. ({clientId})");
                    _keyVaultService.ExpirePreviousVersion($"{seedClientId}-AccessToken", clientId);
                    _keyVaultService.ExpirePreviousVersion($"{seedClientId}-ClientSecret", clientId);
                    Console.WriteLine($"Succeeded to create new key kault version. ({clientId})");
                }
            }
        }

        private bool HasRotatingVersion(string secret)
        {
            var versions = _keyVaultSecretClient.GetPropertiesOfSecretVersions(secret);
            var rotatingVersion = versions.FirstOrDefault(x => x.Tags[KeyVaultTags.Status] ==  KeyVaultTagStatus.Rotating);

            return (rotatingVersion != null);
        }
    }
}