using AstraDB.Token.Rotation.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AstraDB.Token.Rotation.Consumer
{
    public static class Startup
    {
        public async static Task<IHost> ConfigureServices()
        {
            var builder = Host.CreateDefaultBuilder()
                .ConfigureServices((cxt, services) =>
                {
                    services.AddTransient<IConfigurationService, IConfigurationService>();
                    services.AddTransient<IKeyVaultService, KeyVaultService>();
                    services.AddTransient<ITokenRotationService, TokenRotationService>();
                    services.AddTransient<IConfluentService, ConfluentService>();
                    services.AddTransient<ITokenRefreshHandler, TokenRefreshHandler>();
                }).Build();

            return builder;
        }
    }
}