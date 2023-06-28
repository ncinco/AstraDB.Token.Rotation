using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Kafka;
using Microsoft.Extensions.Logging;

namespace AstraDB.Token.Rotation.Consumer.Function
{
    public class Function1
    {
        // KafkaTrigger sample 
        // Consume the message from "token-rotation" on the LocalBroker.
        // Add `cluster.playground.cdkt.io:9092` and `KafkaPassword` to the local.settings.json
        // For EventHubs
        // "cluster.playground.cdkt.io:9092": "{EVENT_HUBS_NAMESPACE}.servicebus.windows.net:9093"
        // "KafkaPassword":"{EVENT_HUBS_CONNECTION_STRING}
        [FunctionName("Function1")]
        public void Run(
            [KafkaTrigger("cluster.playground.cdkt.io:9092",
                          "token-rotation",
                          Username = "nR8Q6LKpCW3CNZ96n0PU9",
                          Password = "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpc3MiOiJodHRwczovL2F1dGguY29uZHVrdG9yLmlvIiwic291cmNlQXBwbGljYXRpb24iOiJhZG1pbiIsInVzZXJNYWlsIjpudWxsLCJwYXlsb2FkIjp7InZhbGlkRm9yVXNlcm5hbWUiOiJuUjhRNkxLcENXM0NOWjk2bjBQVTkiLCJvcmdhbml6YXRpb25JZCI6NzQzMDEsInVzZXJJZCI6ODY0MzEsImZvckV4cGlyYXRpb25DaGVjayI6IjBhNTFlYWIxLWQ5NjctNGQwNS05MzQ5LTRkYzljNjNkNTgwNiJ9fQ.FENh-OXizfIiLGmjGOKuB1apTQLeyT-JteT2g2RcISU",
                          Protocol = BrokerProtocol.SaslSsl,
                          AuthenticationMode = BrokerAuthenticationMode.Plain,
                          SslCaLocation = "cacert.pem",
                          ConsumerGroup = "$Default")] KafkaEventData<string>[] events,
            ILogger log)
        {
            foreach (KafkaEventData<string> eventData in events)
            {
                log.LogInformation($"C# Kafka trigger function processed a message: {eventData.Value}");
            }
        }
    }
}
