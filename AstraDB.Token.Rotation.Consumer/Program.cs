using AstraDB.Token.Rotation.Models;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Confluent.Kafka;
using Newtonsoft.Json;
using RestSharp;

namespace AstraDB.Token.Rotation.Consumer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");

            var brokerList = "cluster.playground.cdkt.io:9092";
            var topic = "token-key-rotation";

            var config = new ConsumerConfig
            {
                BootstrapServers = brokerList,
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SocketTimeoutMs = 60000,                //this corresponds to the Consumer config `request.timeout.ms`
                SessionTimeoutMs = 30000,

                SaslMechanism = SaslMechanism.Plain,
                SaslUsername = "nR8Q6LKpCW3CNZ96n0PU9",
                SaslPassword = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJodHRwczovL2F1dGguY29uZHVrdG9yLmlvIiwic291cmNlQXBwbGljYXRpb24iOiJhZG1pbiIsInVzZXJNYWlsIjpudWxsLCJwYXlsb2FkIjp7InZhbGlkRm9yVXNlcm5hbWUiOiJuUjhRNkxLcENXM0NOWjk2bjBQVTkiLCJvcmdhbml6YXRpb25JZCI6NzQzMDEsInVzZXJJZCI6ODY0MzEsImZvckV4cGlyYXRpb25DaGVjayI6IjBhNTFlYWIxLWQ5NjctNGQwNS05MzQ5LTRkYzljNjNkNTgwNiJ9fQ.FENh-OXizfIiLGmjGOKuB1apTQLeyT-JteT2g2RcISU",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                GroupId = "$Default",
                BrokerVersionFallback = "1.0.0",
            };

            using (var consumer = new ConsumerBuilder<long, string>(config).SetKeyDeserializer(Deserializers.Int64).SetValueDeserializer(Deserializers.Utf8).Build())
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

                consumer.Subscribe(topic);

                Console.WriteLine("Consuming messages from topic: " + topic + ", broker(s): " + brokerList);

                var restClient = new RestClient("https://api.astra.datastax.com");
                restClient.AddDefaultHeader("Content-Type", "application/json");
                restClient.AddDefaultHeader("Authorization", "Bearer AstraCS:JidKALteKigmDImudJcimeZP:5593ab3ad44fd6cdc20f4be849132fe4812a76a51433c1daa4d4f55958903635");

                while (true)
                {
                    try
                    {
                        var msg = consumer.Consume(cts.Token);
                        var message = JsonConvert.DeserializeObject<EventStreamTokenRotationMessage>(msg.Message.Value);
                        Console.WriteLine($"Received: '{msg.Message.Value}'");

                        Console.WriteLine("Attempting to create new AstraDB Token...");
                        var createTokenRequest = new RestRequest("organizations/roles");
                        createTokenRequest.AddJsonBody(message.Roles);
                        var astraNewTokenResponse = restClient.Post<AstraNewTokenResponse>(createTokenRequest);
                        Console.WriteLine($"Succeeded creating new AstraDB Token. {JsonConvert.SerializeObject(astraNewTokenResponse.ClientId)}");

                        UpdateSecret($"{message.SeedClientId}-AccessToken", "rotating", astraNewTokenResponse.ClientId, astraNewTokenResponse.GeneratedOn, astraNewTokenResponse.Token);
                        UpdateSecret($"{message.SeedClientId}-ClientSecret", "rotating", astraNewTokenResponse.ClientId, astraNewTokenResponse.GeneratedOn, astraNewTokenResponse.Secret);

                        Console.WriteLine("$Attempting to revoke new AstraDB Token '{message.ClientId}'");
                        var revokeTokenRequest = new RestRequest("organizations/roles");
                        revokeTokenRequest.AddJsonBody(message.Roles);
                        var astraRevokeTokenResponse = restClient.Post<AstraRevokeTokenResponse>(revokeTokenRequest);
                        Console.WriteLine($"Succeeded revoking AstraDB Token. '{message.ClientId}'");

                        UpdateSecret($"{message.SeedClientId}-AccessToken", "active", astraNewTokenResponse.ClientId, astraNewTokenResponse.GeneratedOn);
                        UpdateSecret($"{message.SeedClientId}-ClientSecret", "active", astraNewTokenResponse.ClientId, astraNewTokenResponse.GeneratedOn);
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

        static void UpdateSecret(string secretName, string secretStatus, string clientId, string generatedOn, string secretValue = "")
        {
            var credential = new ClientSecretCredential("a5e8ce79-b0ec-41a2-a51c-aee927f1d808", "8b281262-415c-4a6c-91b9-246de71c17a9", "3tt8Q~xnvgt~kDmPdGlMoLxzmo8oC7Nf9OSlAcWy");
            var keyVaultSecretClient = new SecretClient(new Uri("https://kv-astradb-astra.vault.azure.net/"), credential);

            var theSecret = keyVaultSecretClient.GetSecret(secretName);

            if (theSecret == null)
            {
                Console.WriteLine($"Can't find secret named {secretName}. Potential bug.");
                return;
            }

            // cache the tags
            var tags = theSecret.Value.Properties.Tags;

            if (!string.IsNullOrWhiteSpace(secretValue))
            {
                Console.WriteLine($"Creating new secret version: {theSecret.Value.Name} {tags}");
                // update the secret, this will create new version WITHOUT the tags
                keyVaultSecretClient.SetSecret(secretName, secretValue);
                // get the latest version
                theSecret = keyVaultSecretClient.GetSecret(secretName);
            }

            Console.WriteLine($"Updating secret tags: {theSecret.Value.Name} {tags}");
            tags["clientId"] = clientId;
            tags["status"] = secretStatus;
            tags["generatedOn"] = generatedOn;

            var secretProperties = new SecretProperties(secretName)
            {
                ContentType = "text/plain",
                NotBefore = DateTime.Now,
                ExpiresOn = DateTime.Now.AddHours(24)
            };

            foreach (var item in tags)
            {
                secretProperties.Tags.Add(item);
            }

            keyVaultSecretClient.UpdateSecretProperties(secretProperties);
        }
    }
}