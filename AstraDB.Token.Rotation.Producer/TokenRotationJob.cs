using AstraDB.Token.Rotation.Services;
using Quartz;

namespace AstraDB.Token.Rotation.Producer
{
    public class TokenRotationJob : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var keyVaultService = new KeyVaultService();
            var TokenRotationService = new TokenRotationService(keyVaultService);
            await TokenRotationService.ProduceMessagesAsync();
            await TokenRotationService.ExpireTokensAsync();
        }
    }
}