using AstraDB.Token.Rotation.Models;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Confluent.Kafka;
using Newtonsoft.Json;
using RestSharp;

namespace AstraDB.Token.Rotation.Consumer
{
    public class TokenRotationService
    {
        private const string BrokerList = "cluster.playground.cdkt.io:9092";
        private const string Topic = "token-key-rotation";

        private ClientSecretCredential _credential;
        private SecretClient _keyVaultSecretClient;
        private const string KeyVaultUrl = "https://kv-astradb-astra.vault.azure.net/";
        private const string TenantId = "a5e8ce79-b0ec-41a2-a51c-aee927f1d808";
        private const string ClientId = "8b281262-415c-4a6c-91b9-246de71c17a9";
        private const string ClientSecret = "3tt8Q~xnvgt~kDmPdGlMoLxzmo8oC7Nf9OSlAcWy";

        private RestClient _restClient;
        private const string DevOpsApiUrl = "https://api.astra.datastax.com";
        private const string DevOpsnApiToken = "AstraCS:JidKALteKigmDImudJcimeZP:5593ab3ad44fd6cdc20f4be849132fe4812a76a51433c1daa4d4f55958903635"

        public void Start()
        {
            Console.WriteLine("Hello, World!");

            _credential = new ClientSecretCredential(TenantId, ClientId, ClientSecret);
            _keyVaultSecretClient = new SecretClient(new Uri(KeyVaultUrl), _credential);

            _restClient = new RestClient(DevOpsApiUrl);
            _restClient.AddDefaultHeader("Content-Type", "application/json");
            _restClient.AddDefaultHeader("Authorization", $"Bearer {DevOpsnApiToken}");

            var config = new ConsumerConfig
            {
                BootstrapServers = BrokerList,
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SocketTimeoutMs = 60000,                //this corresponds to the Consumer config `request.timeout.ms`
                SessionTimeoutMs = 30000,

                SaslMechanism = SaslMechanism.Plain,
                SaslUsername = "6tLgq4vZ2i75SbVx3KQbzN",
                SaslPassword = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJodHRwczovL2F1dGguY29uZHVrdG9yLmlvIiwic291cmNlQXBwbGljYXRpb24iOiJhZG1pbiIsInVzZXJNYWlsIjpudWxsLCJwYXlsb2FkIjp7InZhbGlkRm9yVXNlcm5hbWUiOiI2dExncTR2WjJpNzVTYlZ4M0tRYnpOIiwib3JnYW5pemF0aW9uSWQiOjc0MzMxLCJ1c2VySWQiOjg2NDMxLCJmb3JFeHBpcmF0aW9uQ2hlY2siOiJlYzU0ZWJiZi05M2QxLTQwMTItOWJlMi1iOGU0NDAyOGIwYzEifX0.NYc9bjulgrcGuJmSRGmLwFG7dU8RXDSqa-Kovqla_Zg",
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

                        NewSecretVersion($"{message.SeedClientId}-AccessToken", "rotating", astraNewTokenResponse.ClientId, astraNewTokenResponse.GeneratedOn, astraNewTokenResponse.Token);
                        NewSecretVersion($"{message.SeedClientId}-ClientSecret", "rotating", astraNewTokenResponse.ClientId, astraNewTokenResponse.GeneratedOn, astraNewTokenResponse.Secret);

                        Console.WriteLine($"Attempting to revoke new AstraDB Token '{astraNewTokenResponse.ClientId}'");
                        var revokeTokenRequest = new RestRequest($"v2/clientIdSecrets/{astraNewTokenResponse.ClientId}");
                        revokeTokenRequest.AddBody(jsonPayload, contentType: "application/json");
                        var astraRevokeTokenResponse = _restClient.Delete(revokeTokenRequest);
                        Console.WriteLine($"Succeeded revoking AstraDB Token. '{message.ClientId}'");

                        UpdateSecret($"{message.SeedClientId}-AccessToken", "active", astraNewTokenResponse.ClientId, astraNewTokenResponse.GeneratedOn);
                        UpdateSecret($"{message.SeedClientId}-ClientSecret", "active", astraNewTokenResponse.ClientId, astraNewTokenResponse.GeneratedOn);

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

        private void NewSecretVersion(string secretName, string secretStatus, string clientId, string generatedOn, string secretValue = "")
        {
            var theSecret = _keyVaultSecretClient.GetSecret(secretName).Value;

            if (theSecret == null)
            {
                Console.WriteLine($"Can't find secret named {secretName}. Potential bug.");
                return;
            }

            // copy tags
            var tags = theSecret.Properties.Tags;

            Console.WriteLine($"Creating new secret version: {theSecret.Name} {tags}");
            // update the secret, this will create new version WITHOUT the tags
            _keyVaultSecretClient.SetSecret(secretName, secretValue);
            // get the latest version
            theSecret = _keyVaultSecretClient.GetSecret(secretName);

            Console.WriteLine($"Updating secret tags: {theSecret.Name} {tags}");
            tags["clientId"] = clientId;
            tags["status"] = secretStatus;
            tags["generatedOn"] = generatedOn;

            theSecret.Properties.ContentType = "text/plain";
            theSecret.Properties.NotBefore = DateTime.UtcNow;
            theSecret.Properties.ExpiresOn = DateTime.UtcNow.AddHours(24);

            theSecret.Properties.Tags.Clear();
            foreach (var tag in tags)
            {
                theSecret.Properties.Tags.Add(tag);
            }

            _keyVaultSecretClient.UpdateSecretProperties(theSecret.Properties);
        }

        private void UpdateSecret(string secretName, string secretStatus, string clientId, string generatedOn)
        {
            var theSecret = _keyVaultSecretClient.GetSecret(secretName).Value;

            if (theSecret == null)
            {
                Console.WriteLine($"Can't find secret named {secretName}. Potential bug.");
                return;
            }

            // copy tags
            var tags = theSecret.Properties.Tags;

            Console.WriteLine($"Updating secret tags: {theSecret.Name} {tags}");
            tags["clientId"] = clientId;
            tags["status"] = secretStatus;
            tags["generatedOn"] = generatedOn;

            theSecret.Properties.ContentType = "text/plain";
            theSecret.Properties.NotBefore = DateTime.UtcNow;
            theSecret.Properties.ExpiresOn = DateTime.UtcNow.AddHours(24);

            theSecret.Properties.Tags.Clear();
            foreach (var tag in tags)
            {
                theSecret.Properties.Tags.Add(tag);
            }

            _keyVaultSecretClient.UpdateSecretProperties(theSecret.Properties);
        }
    }
}