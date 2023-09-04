using AstraDB.Token.Rotation.Configuration;
using AstraDB.Token.Rotation.Models;
using Confluent.Kafka;
using Newtonsoft.Json;
using RestSharp;
using System.Text;

namespace AstraDB.Token.Rotation.Services
{
    public class TokenRotationService : ITokenRotationService
    {
        private readonly IKeyVaultService _keyVaultService;
        private readonly IConfluentService _kafkaClientBuilder;
        private readonly RestClient _restClient;
        private IProducer<string, string> _producer;
        private int _messageCounter;

        public TokenRotationService(IKeyVaultService keyVaultService, IConfluentService kafkaClientBuilder, IConfigurationService configurationService)
        {
            _keyVaultService = keyVaultService;
            _kafkaClientBuilder = kafkaClientBuilder;

            var astraDbConfig = configurationService.GetConfig<AstraDbConfig>("AstraDb");

            _restClient = new RestClient(astraDbConfig.Url);
            _restClient.AddDefaultHeader("Content-Type", "application/json");
            _restClient.AddDefaultHeader("Authorization", $"Bearer {astraDbConfig.Token}");
        }

        public async Task TaskProduceDummyMessagesAsync()
        {
            try
            {
                Console.WriteLine("Attempt producing messages.");

                if (_producer == null)
                {
                    _producer = _kafkaClientBuilder.CreateProducer();
                    _messageCounter = 0;
                }

                for (int i = 0; i < 10; i++)
                {
                    _messageCounter++;

                    await _producer.ProduceAsync(_kafkaClientBuilder.TopicName, new Message<string, string> { Key = $"key{_messageCounter}", Value = $"value{_messageCounter}" });

                    Console.WriteLine($"Key: key{_messageCounter} Value: value{_messageCounter}");
                }

                _producer.Flush();
                Console.WriteLine("producer.Flush()");
            }
            catch (Exception ex)
            {
                var error = new StringBuilder();
                error.AppendLine(ex.Message);
                error.AppendLine(ex.ToString());

                Console.WriteLine(error.ToString());
            }
        }

        public async Task TaskConsumeDummyMessagesAsync()
        {
            using (var consumer = _kafkaClientBuilder.CreateConsumer())
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

                consumer.Subscribe(_kafkaClientBuilder.TopicName);

                Console.WriteLine("Consuming messages from topic: " + _kafkaClientBuilder.TopicName + ", broker(s): " + _kafkaClientBuilder.ConsumerBootstrapServers);

                while (true)
                {
                    try
                    {
                        var msg = consumer.Consume(cts.Token);
                        //var message = JsonConvert.DeserializeObject<EventStreamTokenRotationMessage>(msg.Message.Value);
                        //Console.WriteLine($"Received: '{msg.Message.Value}'");

                        Console.WriteLine($"Received: '{msg.Message.Value}'");
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

        public async Task ProduceMessagesAsync()
        {
            using (var producer = _kafkaClientBuilder.CreateProducer())
            {
                Console.WriteLine("Attempting to fetch to AstraDB Tokens...");
                var astraTokensResponse = await _restClient.ExecuteGetAsync<AstraTokensResponse>(new RestRequest("v2/clientIdSecrets"));
                Console.WriteLine($"Succeeded fetching AstraDB Tokens. Token count: {astraTokensResponse.Data.Clients.Count}");

                // just exit but unlikely
                if (astraTokensResponse == null) return;

                Console.WriteLine("Attempting to fetch to Key Vault Secrets...");
                var keyVaultSecrets = await _keyVaultService
                    .GetPropertiesOfSecretsAsync();
                Console.WriteLine($"Succeeded fetching Key Vault Secrets. Total secrets: {keyVaultSecrets.Count}");

                foreach (var secret in keyVaultSecrets)
                {
                    try
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

                                await producer.ProduceAsync(_kafkaClientBuilder.TopicName, new Message<string, string> { Key = key, Value = messagePayloadJson });

                                Console.WriteLine($"Message {key} sent (value: '{messagePayloadJson}')");
                            }
                            else
                            {
                                Console.WriteLine($"Can't find token from astradb with client id of '{clientId}'. Secret with '{seedClientId}-AccessToken' and '{seedClientId}-AccessToken' are orphaned.");
                            }
                        }
                    }
                    catch (ProduceException<string, string> e)
                    {
                        Console.WriteLine($"Produce error: {e.Error.Reason}");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Error: {e.Message}");
                    }
                }

                producer.Flush();
            }
        }

        public async Task ConsumeMessagesAsync()
        {
            using (var consumer = _kafkaClientBuilder.CreateConsumer())
            {
                CancellationTokenSource cts = new CancellationTokenSource();
                Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

                consumer.Subscribe(_kafkaClientBuilder.TopicName);

                Console.WriteLine("Consuming messages from topic: " + _kafkaClientBuilder.TopicName + ", broker(s): " + _kafkaClientBuilder.ConsumerBootstrapServers);

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
            var keyVaultSecrets = await _keyVaultService
                .GetPropertiesOfSecretsAsync();
            Console.WriteLine($"Succeeded fetching Key Vault Secrets. Total secrets: {keyVaultSecrets.Count}");

            // process just the rotating status with name contains "-AccessToken" since they come in pairs
            foreach (var secret in keyVaultSecrets
                .Where(x => string.Compare(x.Tags[KeyVaultTags.Status], KeyVaultStatus.Active) == 0
                && x.Name.Contains("-AccessToken")))
            {
                try
                {
                    var theSecret = await _keyVaultService.GetSecretAsync(secret.Name);
                    var previousVersion = _keyVaultService.GetPreviousVersion(theSecret, KeyVaultStatus.Rotating);

                    // delete old token and expire previous version
                    if (previousVersion != null)
                    {
                        var previousClientId = previousVersion.Tags[KeyVaultTags.ClientId];

                        Console.WriteLine($"Attempting to revoke old astradb token '{previousClientId}'");
                        var revokeTokenRequest = new RestRequest($"v2/clientIdSecrets/{previousClientId}");
                        var astraRevokeTokenResponse = await _restClient.DeleteAsync(revokeTokenRequest);
                        Console.WriteLine($"Succeeded revoking old astradb token. '{previousClientId}'. Code: {astraRevokeTokenResponse.StatusCode} Content: {astraRevokeTokenResponse.Content}");

                        // if seed_clientId is missing for whatever reason, at least the token was already deleted.
                        // I don't know why it happened before
                        var seedClientId = theSecret.Properties.Tags[KeyVaultTags.SeedClientId];
                        Console.WriteLine($"Attempting expiring old key vault version. ({previousClientId})");
                        await _keyVaultService.ExpirePreviousVersionsAsyc($"{seedClientId}-AccessToken");
                        await _keyVaultService.ExpirePreviousVersionsAsyc($"{seedClientId}-ClientSecret");
                        Console.WriteLine($"Succeeded expiring old key vault version. ({previousClientId})");
                    }
                    else
                    {
                        Console.WriteLine($"No previous version found for secret '{theSecret.Name}'. Potentially new secret without old version.");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error: {e.Message}");
                }
            }
        }
    }
}