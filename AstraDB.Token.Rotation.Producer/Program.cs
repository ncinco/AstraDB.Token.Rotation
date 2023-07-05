using AstraDB.Token.Rotation.Services;

namespace AstraDB.Token.Rotation.Producer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var keyVaultService = new KeyVaultService();
            var TokenRotationService = new TokenRotationService(keyVaultService);
            TokenRotationService.ProduceMessages();
            TokenRotationService.ExpireTokens();
        }
    }
}