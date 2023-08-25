using AstraDB.Token.Rotation.Services;
using Quartz;

namespace AstraDB.Token.Rotation.Producer
{
    public class TokenRotationJob : IJob
    {
        private readonly ITokenRotationService _tokenRotationService;

        public TokenRotationJob(ITokenRotationService tokenRotationService)
        {
            _tokenRotationService = tokenRotationService;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            await _tokenRotationService.TaskProduceDummyMessagesAsync();
            //await _tokenRotationService.TaskConsumeDummyMessagesAsync();

            //await _tokenRotationService.ProduceMessagesAsync();
            //await _tokenRotationService.ExpireTokensAsync();
        }
    }
}