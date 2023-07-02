using AstraDB.Token.Rotation.Configuration;
using AstraDB.Token.Rotation.Models;
using Confluent.Kafka;
using Newtonsoft.Json;
using RestSharp;

namespace AstraDB.Token.Rotation.Consumer
{
    public interface ITokenRotationService
    {
        void Start();
    }

    public class TokenRotationService : ITokenRotationService
    {
        private RestClient _restClient;
        private readonly IKeyVaultService _keyVaultService;

        public TokenRotationService(IKeyVaultService keyVaultService)
        {
            _keyVaultService = keyVaultService;
        }

        public void Start()
        {
            _restClient = new RestClient(DevOpsApi.Url);
            _restClient.AddDefaultHeader("Content-Type", "application/json");
            _restClient.AddDefaultHeader("Authorization", $"Bearer {DevOpsApi.Token}");

            var config = new ConsumerConfig
            {
                BootstrapServers = Kafka.BrokerList,
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SocketTimeoutMs = 60000,
                SessionTimeoutMs = 30000,

                SaslMechanism = SaslMechanism.Plain,
                SaslUsername = Kafka.Username,
                SaslPassword = Kafka.Password,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                GroupId = "$Default",
                BrokerVersionFallback = "1.0.0",
            };

            using (var consumer = new ConsumerBuilder<long, string>(config).SetKeyDeserializer(Deserializers.Int64).SetValueDeserializer(Deserializers.Utf8).Build())
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

                consumer.Subscribe(Kafka.Topic);

                Console.WriteLine("Consuming messages from topic: " + Kafka.Topic + ", broker(s): " + Kafka.BrokerList);

                while (true)
                {
                    try
                    {
                        var msg = consumer.Consume(cts.Token);
                        var message = JsonConvert.DeserializeObject<EventStreamTokenRotationMessage>(msg.Message.Value);
                        Console.WriteLine($"Received: '{msg.Message.Value}'");

                        Console.WriteLine("Attempting to create new AstraDB Token...");
                        var createTokenRequest = new RestRequest("v2/clientIdSecrets");

                        var jsonPayload = @"{""roles"": " + JsonConvert.SerializeObject(message.Roles) + "}";
                        createTokenRequest.AddBody(jsonPayload, contentType: "application/json");

                        var response = _restClient.Post(createTokenRequest);
                        var astraNewTokenResponse = JsonConvert.DeserializeObject<AstraNewTokenResponse>(response.Content);

                        Console.WriteLine($"Succeeded creating new AstraDB Token. {JsonConvert.SerializeObject(astraNewTokenResponse.ClientId)}");

                        _keyVaultService.NewVersion($"{message.SeedClientId}-AccessToken", "active", astraNewTokenResponse.ClientId, astraNewTokenResponse.GeneratedOn, astraNewTokenResponse.Token);
                        _keyVaultService.NewVersion($"{message.SeedClientId}-ClientSecret", "active", astraNewTokenResponse.ClientId, astraNewTokenResponse.GeneratedOn, astraNewTokenResponse.Secret);

                        Console.WriteLine($"Attempting to revoke AstraDB Token '{message.ClientId}'");
                        var revokeTokenRequest = new RestRequest($"v2/clientIdSecrets/{message.ClientId}");
                        revokeTokenRequest.AddBody(jsonPayload, contentType: "application/json");
                        var astraRevokeTokenResponse = _restClient.Delete(revokeTokenRequest);
                        Console.WriteLine($"Succeeded revoking AstraDB Token. '{message.ClientId}'");

                        _keyVaultService.ExpirePreviousVersion($"{message.SeedClientId}-AccessToken");
                        _keyVaultService.ExpirePreviousVersion($"{message.SeedClientId}-ClientSecret");

                        consumer.Commit();
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
    }
}