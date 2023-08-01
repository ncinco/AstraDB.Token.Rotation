namespace AstraDB.Token.Rotation.Services
{
    public interface IConfigurationService
    {
        T GetValue<T>(string keyName);

        T GetConfig<T>(string sectionName);
    }
}
