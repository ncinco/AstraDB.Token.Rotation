using AstraDB.Token.Rotation.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AstraDB.Token.Rotation.Consumer
{
    public class Program
    {
        public async static Task Main(string[] args)
        {
            var builder = await Startup.ConfigureServices();

            await builder
                .Services
                .GetService<ITokenRotationService>()
                .TaskConsumeDummyMessagesAsync();
                //.ConsumeMessagesAsync();
        }
    }
}