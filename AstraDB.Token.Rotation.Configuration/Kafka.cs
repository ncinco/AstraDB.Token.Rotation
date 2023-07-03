namespace AstraDB.Token.Rotation.Configuration
{
    public class Kafka
    {
        public const string ConsumerGroup = "$Default";

        public const string BrokerList = "token-management.servicebus.windows.net:9093";
        public const string Topic = "token-rotation";

        public const string Username = "$ConnectionString";
        public const string Password = "Endpoint=sb://token-management.servicebus.windows.net/;SharedAccessKeyName=write-only-policy;SharedAccessKey=t5Dy/diyni2LD5URr8j2aWddhx+JWNrID+AEhPINaIk=;EntityPath=token-rotation";
    }
}