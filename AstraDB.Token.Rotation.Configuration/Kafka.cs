namespace AstraDB.Token.Rotation.Configuration
{
    public class Kafka
    {
        public const string ConsumerGroup = "$Default";

        public const string BrokerList = "token-management.servicebus.windows.net:9093";
        public const string Topic = "token-rotation-fun";

        public const string Username = "$ConnectionString";
        public const string Password = "Endpoint=sb://token-management.servicebus.windows.net/;SharedAccessKeyName=full-access;SharedAccessKey=1p2SoLYlHXddtWkL+tQwqb1ROGkZGRVoB+AEhLTgHMg=;EntityPath=token-rotation-fun";
    }
}