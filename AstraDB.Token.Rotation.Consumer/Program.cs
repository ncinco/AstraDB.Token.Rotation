using AstraDB.Token.Rotation.Services;

namespace AstraDB.Token.Rotation.Consumer
{
    public class Program
    {
        public async static Task Main(string[] args)
        {
            var keyVaultService = new KeyVaultService();
            var TokenRotationService = new TokenRotationService(keyVaultService);
            await TokenRotationService.ConsumeMessagesAsync();
        }
    }
}