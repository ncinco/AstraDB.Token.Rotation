namespace AstraDB.Token.Rotation.Consumer
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var keyVaultService = new KeyVaultService();
            new TokenRotationService(keyVaultService).Start();
        }
    }
}