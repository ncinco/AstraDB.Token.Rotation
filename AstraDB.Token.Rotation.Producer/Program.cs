using AstraDB.Token.Rotation.Producer;
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
            Console.WriteLine("Hello, World!");

            var brokerList = "cluster.playground.cdkt.io:9092";
            var topic = "token-rotation";

            var config = new ProducerConfig
            {
                BootstrapServers = brokerList,
                SecurityProtocol = SecurityProtocol.SaslSsl,
                SaslMechanism = SaslMechanism.Plain,
                SaslUsername = "nR8Q6LKpCW3CNZ96n0PU9",
                SaslPassword = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJodHRwczovL2F1dGguY29uZHVrdG9yLmlvIiwic291cmNlQXBwbGljYXRpb24iOiJhZG1pbiIsInVzZXJNYWlsIjpudWxsLCJwYXlsb2FkIjp7InZhbGlkRm9yVXNlcm5hbWUiOiJuUjhRNkxLcENXM0NOWjk2bjBQVTkiLCJvcmdhbml6YXRpb25JZCI6NzQzMDEsInVzZXJJZCI6ODY0MzEsImZvckV4cGlyYXRpb25DaGVjayI6IjBhNTFlYWIxLWQ5NjctNGQwNS05MzQ5LTRkYzljNjNkNTgwNiJ9fQ.FENh-OXizfIiLGmjGOKuB1apTQLeyT-JteT2g2RcISU",
            };

            using (var producer = new ProducerBuilder<long, string>(config).SetKeySerializer(Serializers.Int64).SetValueSerializer(Serializers.Utf8).Build())
            {
                Console.WriteLine("Sending 10 messages to topic: " + topic + ", broker(s): " + brokerList);
                for (int x = 0; x < 1000000; x++)
                {
                    var msg = string.Format("Sample message #{0} sent at {1}", x, DateTime.Now.ToString("yyyy-MM-dd_HH:mm:ss.ffff"));
                    producer.Produce(topic, new Message<long, string> { Key = DateTime.UtcNow.Ticks, Value = msg });
                    Console.WriteLine(string.Format("Message {0} sent (value: '{1}')", x, msg));

                    System.Threading.Thread.Sleep(100);
                }
            }

            //var restClient = new RestClient("https://api.astra.datastax.com");
            //restClient.AddDefaultHeader("Content-Type", "application/json");
            //restClient.AddDefaultHeader("Authorization", "Bearer AstraCS:JidKALteKigmDImudJcimeZP:5593ab3ad44fd6cdc20f4be849132fe4812a76a51433c1daa4d4f55958903635");
            //var astraTokensResponse = restClient.ExecuteGet<AstraTokensResponse>(new RestRequest("v2/clientIdSecrets")).Data;

            //var credential = new ClientSecretCredential("a5e8ce79-b0ec-41a2-a51c-aee927f1d808", "8b281262-415c-4a6c-91b9-246de71c17a9", "3tt8Q~xnvgt~kDmPdGlMoLxzmo8oC7Nf9OSlAcWy");
            //var keyVaultSecretClient = new SecretClient(new Uri("https://kv-astradb-astra.vault.azure.net/"), credential);

            //var secrets = keyVaultSecretClient.GetPropertiesOfSecrets();

            //foreach (var secret in secrets)
            //{
            //    Console.WriteLine(secret.Name);

            //    var status = secret.Tags["status"];
            //    var generatedOn = secret.Tags["generatedOn"];

            //    if (string.Compare(status, "active", true) == 0
            //        && (DateTime.Now - DateTime.Parse(generatedOn)).Minutes >= 3
            //        && secret.Name.Contains("-AccessToken"))
            //    {
            //        var seedClientId = secret.Tags["seed_clientId"];
            //        var clientId = secret.Tags["clientId"];

            //        var theSecret = astraTokensResponse.Clients.FirstOrDefault(x => string.Compare(x.ClientId, clientId, true) == 0);

            //        if (theSecret != null)
            //        {
            //            var createTokenRequest = new RestRequest("organizations/roles");
            //            createTokenRequest.AddJsonBody(theSecret.Roles);
            //            //var astraNewTokenResponse = restClient.Post<AstraNewTokenResponse>(createTokenRequest);


            //            var accessToken = keyVaultSecretClient.GetSecret($"{seedClientId}-AccessToken").Value;
            //            var clientSecret = keyVaultSecretClient.GetSecret($"{seedClientId}-ClientSecret").Value;


            //            var revokeTokenRequest = new RestRequest("organizations/roles");
            //            revokeTokenRequest.AddJsonBody(theSecret.Roles);
            //            //var astraRevokeTokenResponse = restClient.Post<AstraRevokeTokenResponse>(revokeTokenRequest);
            //        }
            //    }
            //}
        }
    }
}