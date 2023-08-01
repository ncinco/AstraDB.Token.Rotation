namespace AstraDB.Token.Rotation.Configuration
{
    public class KafkaConfig
    {
        public const string ConsumerGroup = "$Default";

        public const string BrokerList = "pkc-56d1g.eastus.azure.confluent.cloud:9092";
        public const string Topic = "token-rotation";

        public class Consumer
        {
            public const string OAuthClientId = "c148bf65-dcb3-491d-92ec-78a68eba06d8";
            public const string OAuthClientSecret = "s6b8Q~gD.FrT8RNC4TlWW7gdvo1ZKHoR0j9uVaKY";
        }

        public class Producer
        {
            public const string OAuthClientId = "3cac0c5b-4612-4e4c-869c-d577cd17dc78";
            public const string OAuthClientSecret = "LXL8Q~w5v11WAkBpHBGYX1ze4I3wpqZhX_g1la1r";
        }
    }
}