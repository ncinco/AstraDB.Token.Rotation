using AstraDB.Token.Rotation.Models;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
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
        private const string BrokerList = "cluster.playground.cdkt.io:9092";
        private const string Topic = "token-key-rotation";

        private const string DevOpsApiUrl = "https://api.astra.datastax.com";
        private const string DevOpsnApiToken = "AstraCS:JidKALteKigmDImudJcimeZP:5593ab3ad44fd6cdc20f4be849132fe4812a76a51433c1daa4d4f55958903635";

        private const string KafkaUsername = "6tLgq4vZ2i75SbVx3KQbzN";
        private const string KafkaPassword = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJodHRwczovL2F1dGguY29uZHVrdG9yLmlvIiwic291cmNlQXBwbGljYXRpb24iOiJhZG1pbiIsInVzZXJNYWlsIjpudWxsLCJwYXlsb2FkIjp7InZhbGlkRm9yVXNlcm5hbWUiOiI2dExncTR2WjJpNzVTYlZ4M0tRYnpOIiwib3JnYW5pemF0aW9uSWQiOjc0MzMxLCJ1c2VySWQiOjg2NDMxLCJmb3JFeHBpcmF0aW9uQ2hlY2siOiJlYzU0ZWJiZi05M2QxLTQwMTItOWJlMi1iOGU0NDAyOGIwYzEifX0.NYc9bjulgrcGuJmSRGmLwFG7dU8RXDSqa-Kovqla_Zg";

        private RestClient _restClient;
        private readonly IKeyVaultService _keyVaultService;

        public TokenRotationService(IKeyVaultService keyVaultService)
        {
            _keyVaultService = keyVaultService;
        }

        public void Start()
        {
            Console.WriteLine("Hello, World!");

            _restClient = new RestClient(DevOpsApiUrl);
            _restClient.AddDefaultHeader("Content-Type", "application/json");
            _restClient.AddDefaultHeader("Authorization", $"Bearer {DevOpsnApiToken}");

            var config = new ConsumerConfig
            {
                BootstrapServers = BrokerList,
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SocketTimeoutMs = 60000,
                SessionTimeoutMs = 30000,

                SaslMechanism = SaslMechanism.Plain,
                SaslUsername = KafkaUsername,
                SaslPassword = KafkaPassword,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                GroupId = "$Default",
                BrokerVersionFallback = "1.0.0",
            };

            using (var consumer = new ConsumerBuilder<long, string>(config).SetKeyDeserializer(Deserializers.Int64).SetValueDeserializer(Deserializers.Utf8).Build())
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

                consumer.Subscribe(Topic);

                Console.WriteLine("Consuming messages from topic: " + Topic + ", broker(s): " + BrokerList);

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