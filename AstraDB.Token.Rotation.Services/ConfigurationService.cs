using Microsoft.Extensions.Configuration;

namespace AstraDB.Token.Rotation.Services
{
    public class ConfigurationService: IConfigurationService
    {
        private readonly IConfigurationRoot _configurationRoot;

        public ConfigurationService()
        {
            _configurationRoot = new ConfigurationBuilder()
                .AddJsonFile("./appsettings.json")
                .Build();
        }

        public T GetValue<T>(string keyName)
        {
#pragma warning disable CS8603 // Possible null reference return.
            return _configurationRoot
                .GetValue<T>(keyName);
#pragma warning restore CS8603 // Possible null reference return.
        }

        public T GetConfig<T>(string sectionName)
        {
            var config = _configurationRoot
                .GetSection(sectionName)
                .Get<T>();

            return config;
        }        
    }
}