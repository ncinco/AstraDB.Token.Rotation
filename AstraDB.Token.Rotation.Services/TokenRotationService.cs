using AstraDB.Token.Rotation.Configuration;
using AstraDB.Token.Rotation.Models;
using Confluent.Kafka;
using Newtonsoft.Json;
using RestSharp;

namespace AstraDB.Token.Rotation.Services
{
    public class TokenRotationService : ITokenRotationService
    {
        private readonly IKeyVaultService _keyVaultService;
        private readonly RestClient _restClient;

        public TokenRotationService(IKeyVaultService keyVaultService)
        {
            _keyVaultService = keyVaultService;

            _restClient = new RestClient(DevOpsApiConfig.Url);
            _restClient.AddDefaultHeader("Content-Type", "application/json");
            _restClient.AddDefaultHeader("Authorization", $"Bearer {DevOpsApiConfig.Token}");
        }

        public async Task ProduceMessagesAsync()
        {
            var config = new ProducerConfig
            {
                BootstrapServers = KafkaConfig.BrokerList,
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SaslMechanism = SaslMechanism.Plain,
                SaslUsername = KafkaConfig.Username,
                SaslPassword = KafkaConfig.Password
            };

            using (var producer = new ProducerBuilder<string, string>(config).SetKeySerializer(Serializers.Utf8).SetValueSerializer(Serializers.Utf8).Build())
            {
                Console.WriteLine("Attempting to fetch to AstraDB Tokens...");
                var astraTokensResponse = await _restClient.ExecuteGetAsync<AstraTokensResponse>(new RestRequest("v2/clientIdSecrets"));
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
                        && (DateTime.UtcNow - DateTime.Parse(generatedOn).ToUniversalTime()).Minutes >= 3
                        && secret.Name.Contains("-AccessToken"))
                    {
                        var seedClientId = secret.Tags[KeyVaultTags.SeedClientId];
                        var clientId = secret.Tags[KeyVaultTags.ClientId];

                        Console.WriteLine($"Trying to rotate {seedClientId}-AccessToken and {seedClientId}-ClientSecret");

                        // find matching astradb token
                        var theAstraDbToken = astraTokensResponse.Data.Clients.FirstOrDefault(x => string.Compare(x.ClientId, clientId, true) == 0);

                        if (theAstraDbToken != null)
                        {
                            var key = clientId;
                            var messagePayload = new EventStreamTokenRotationMessage
                            {
                                SeedClientId = seedClientId,
                                ClientId = clientId,
                                Roles = theAstraDbToken.Roles
                            };

                            var messagePayloadJson = JsonConvert.SerializeObject(messagePayload);

                            await producer.ProduceAsync(KafkaConfig.Topic, new Message<string, string> { Key = key, Value = messagePayloadJson });

                            Console.WriteLine($"Message {key} sent (value: '{messagePayloadJson}')");
                        }
                    }
                }

                producer.Flush();
            }
        }

        public async Task ConsumeMessagesAsync()
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = KafkaConfig.BrokerList,
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SocketTimeoutMs = 60000,
                SessionTimeoutMs = 30000,

                SaslMechanism = SaslMechanism.Plain,
                SaslUsername = KafkaConfig.Username,
                SaslPassword = KafkaConfig.Password,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                GroupId = KafkaConfig.ConsumerGroup,
                BrokerVersionFallback = "1.0.0",
            };

            using (var consumer = new ConsumerBuilder<string, string>(config).SetKeyDeserializer(Deserializers.Utf8).SetValueDeserializer(Deserializers.Utf8).Build())
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

                consumer.Subscribe(KafkaConfig.Topic);

                Console.WriteLine("Consuming messages from topic: " + KafkaConfig.Topic + ", broker(s): " + KafkaConfig.BrokerList);

                while (true)
                {
                    try
                    {
                        var msg = consumer.Consume(cts.Token);
                        var message = JsonConvert.DeserializeObject<EventStreamTokenRotationMessage>(msg.Message.Value);
                        Console.WriteLine($"Received: '{msg.Message.Value}'");

                        Console.WriteLine("Attempting to create new astradb token...");
                        var createTokenRequest = new RestRequest("v2/clientIdSecrets");
                        var jsonPayload = @"{""roles"": " + JsonConvert.SerializeObject(message.Roles) + "}";
                        createTokenRequest.AddBody(jsonPayload, contentType: "application/json");
                        var response = await _restClient.PostAsync(createTokenRequest);
                        var astraNewTokenResponse = JsonConvert.DeserializeObject<AstraNewTokenResponse>(response.Content);
                        Console.WriteLine($"Succeeded creating new astradb token. {JsonConvert.SerializeObject(astraNewTokenResponse.ClientId)}");

                        Console.WriteLine("Attempting to create new key vault version with new astradb token...");
                        await _keyVaultService.NewVersionAsync($"{message.SeedClientId}-AccessToken", KeyVaultStatus.Active, astraNewTokenResponse.ClientId, astraNewTokenResponse.GeneratedOn, astraNewTokenResponse.Token);
                        await _keyVaultService.NewVersionAsync($"{message.SeedClientId}-ClientSecret", KeyVaultStatus.Active, astraNewTokenResponse.ClientId, astraNewTokenResponse.GeneratedOn, astraNewTokenResponse.Secret);
                        Console.WriteLine("Succeeded creating new key vault version...");

                        Console.WriteLine("Attempting to set previous key vault version to rotating status...");
                        await _keyVaultService.SetPerviousVersionToRotatingAsync($"{message.SeedClientId}-AccessToken");
                        await _keyVaultService.SetPerviousVersionToRotatingAsync($"{message.SeedClientId}-ClientSecret");
                        Console.WriteLine("Succeeded setting previous key vault version to rotating status...");
                    }
                    catch (ConsumeException e)
                    {
                        Console.WriteLine($"Consume error: {e.Error.Reason}");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error: {e.Message}");
                    }
                }
            }
        }

        public async Task ExpireTokensAsync()
        {
            Console.WriteLine("Attempting to fetch to Key Vault Secrets...");
            var keyVaultSecrets = _keyVaultService
                .GetPropertiesOfSecrets();
            Console.WriteLine("Succeeded fetching Key Vault Secrets.");

            // process just the rotating status with name contains "-AccessToken" since they come in pairs
            foreach (var secret in keyVaultSecrets
                .Where(x => string.Compare(x.Tags[KeyVaultTags.Status], KeyVaultStatus.Active) == 0
                && x.Name.Contains("-AccessToken")))
            {
                var theSecret = await _keyVaultService.GetSecretAsync(secret.Name);
                var previousVersion = _keyVaultService.GetPreviousVersion(theSecret, KeyVaultStatus.Rotating);

                // delete old token and expire previous version
                if (previousVersion != null)
                {
                    var seedClientId = theSecret.Properties.Tags[KeyVaultTags.SeedClientId];
                    var previousClientId = previousVersion.Tags[KeyVaultTags.ClientId];

                    Console.WriteLine($"Attempting to revoke old astradb token '{previousClientId}'");
                    var revokeTokenRequest = new RestRequest($"v2/clientIdSecrets/{previousClientId}");
                    var astraRevokeTokenResponse = await _restClient.DeleteAsync(revokeTokenRequest);
                    Console.WriteLine($"Succeeded revoking old astradb token. '{previousClientId}'");

                    Console.WriteLine($"Attempting expiring old key vault version. ({previousClientId})");
                    await _keyVaultService.ExpirePreviousVersionsAsyc($"{seedClientId}-AccessToken");
                    await _keyVaultService.ExpirePreviousVersionsAsyc($"{seedClientId}-ClientSecret");
                    Console.WriteLine($"Succeeded expiring old key vault version. ({previousClientId})");
                }
            }
        }
    }
}