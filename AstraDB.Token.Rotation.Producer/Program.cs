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

           //var versions = kafkaService
           //     .GetVersions("cTfGrnObigDGnNHRXbPFYfKK-AccessToken")
           //     .OrderByDescending(x => x.CreatedOn)
           //     .ToList();
           // versions.ForEach(version =>
           // {
           //     Console.WriteLine($"version.Version: {version.Version} version.CreatedOn: {version.CreatedOn}" );
           // });

            
        }
    }
}