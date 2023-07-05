using AstraDB.Token.Rotation.Services;

namespace AstraDB.Token.Rotation.Producer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var keyVaultService = new KeyVaultService();
            var kafkaService = new KafkaService(keyVaultService);
            //kafkaService.ProduceMessages();
            kafkaService.ExpireTokens();
        }
    }
}