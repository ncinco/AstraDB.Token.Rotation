using AstraDB.Token.Rotation.Consumer;
using AstraDB.Token.Rotation.Producer;

namespace AstraDB.Token.Rotation
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