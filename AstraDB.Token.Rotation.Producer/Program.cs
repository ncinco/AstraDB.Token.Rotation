using AstraDB.Token.Rotation.Configuration;
using AstraDB.Token.Rotation.Models;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Confluent.Kafka;
using RestSharp;

namespace AstraDB.Token.Rotation
{
    internal class Program
    {
        static void Main(string[] args)
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

                var credential = new ClientSecretCredential(KeyVault.TenantId, KeyVault.ClientId, KeyVault.ClientSecret);
                var keyVaultSecretClient = new SecretClient(new Uri(KeyVault.KeyVaultUrl), credential);

                Console.WriteLine("Attempting to fetch to Key Vault Secrets...");
                var keyVaultSecrets = keyVaultSecretClient.GetPropertiesOfSecrets();
                Console.WriteLine("Succeeded fetching Key Vault Secrets.");

                foreach (var secret in keyVaultSecrets)
                {
                    var status = secret.Tags["status"];
                    var generatedOn = secret.Tags["generatedOn"];

                    if (string.Compare(status, "active", true) == 0
                        && (DateTime.UtcNow - DateTime.Parse(generatedOn)).Minutes >= 3
                        && secret.Name.Contains("-AccessToken"))
                    {
                        var seedClientId = secret.Tags["seed_clientId"];
                        var clientId = secret.Tags["clientId"];

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

                            var messagePayloadJson = Newtonsoft.Json.JsonConvert.SerializeObject(messagePayload);

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