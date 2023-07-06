namespace AstraDB.Token.Rotation.Configuration
{
    public class KafkaConfig
    {
        public const string ConsumerGroup = "$Default";

        public const string BrokerList = "astradb-token-management.servicebus.windows.net:9093";
        public const string Topic = "token-rotation";

        public const string Username = "$ConnectionString";
        public const string Password = "Endpoint=sb://astradb-token-management.servicebus.windows.net/;SharedAccessKeyName=full-access;SharedAccessKey=DLqAOndS1stg3yej3ekShX3hRhVtuMBKO+AEhEQQTyw=;EntityPath=token-rotation";
    }
}